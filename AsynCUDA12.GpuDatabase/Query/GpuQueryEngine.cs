using System;
using System.Text;
using AsynCUDA12.Runtime;
using ManagedCuda.BasicTypes;

namespace AsynCUDA12.GpuDatabase
{
    /// <summary>
    /// Translates a structured <see cref="GpuQuery"/> into a pipeline of GPU kernel invocations against
    /// the VRAM-resident tables in a <see cref="TableCatalog"/>. Temporary buffers (mask, partial sums,
    /// pattern data, …) are tracked in a <see cref="QueryContext"/> and freed when the query ends.
    /// Persistent table columns are read with <c>keepBuffer:true</c> so they remain in VRAM.
    /// </summary>
    public sealed class GpuQueryEngine
    {
        private readonly CudaService cuda;
        private readonly TableCatalog catalog;
        private readonly GpuKernelExecutor executor;

        /// <summary>
        /// Initializes a new instance of the <see cref="GpuQueryEngine"/> class.
        /// </summary>
        /// <param name="cuda">The runtime service for memory transfers and freeing.</param>
        /// <param name="catalog">The catalog resolving table/column names to device buffers.</param>
        /// <param name="executor">The multi-pointer kernel executor.</param>
        public GpuQueryEngine(CudaService cuda, TableCatalog catalog, GpuKernelExecutor executor)
        {
            this.cuda = cuda ?? throw new ArgumentNullException(nameof(cuda));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        /// <summary>
        /// Executes the given query and returns its result.
        /// </summary>
        /// <param name="query">The query to execute.</param>
        /// <returns>The <see cref="QueryResult"/>; <see cref="QueryResult.Success"/> is <c>false</c> on failure.</returns>
        public QueryResult Execute(GpuQuery query)
        {
            GpuTable? table = this.catalog.GetTable(query.Table);
            if (table == null)
            {
                return QueryResult.Fail($"Table '{query.Table}' not found.");
            }

            using QueryContext context = new(this.cuda);
            try
            {
                CudaMem? mask = this.BuildMask(table, query, context, out string? maskError);
                if (maskError != null)
                {
                    return QueryResult.Fail(maskError);
                }

                return this.Aggregate(table, query, mask, context);
            }
            catch (Exception ex)
            {
                CudaLogger.Log("GpuQueryEngine: Query execution failed", ex);
                return QueryResult.Fail(ex.Message);
            }
        }

        // ---- Filtering ----

        private CudaMem? BuildMask(GpuTable table, GpuQuery query, QueryContext context, out string? error)
        {
            error = null;
            if (query.Predicate == QueryPredicate.None)
            {
                return null;
            }

            CudaMem? mask = this.cuda.AllocateSingle<byte>(table.RowCount);
            if (mask == null)
            {
                error = "Failed to allocate mask buffer.";
                return null;
            }

            context.Track(mask);

            bool ok = query.Predicate switch
            {
                QueryPredicate.IntEquals or QueryPredicate.IntRange or QueryPredicate.FloatRange
                    => this.RunNumericFilter(table, query, mask, out error),
                QueryPredicate.StringContains or QueryPredicate.StringFuzzy
                    => this.RunStringFilter(table, query, mask, context, out error),
                _ => false
            };

            return ok ? mask : null;
        }

        private bool RunNumericFilter(GpuTable table, GpuQuery query, CudaMem mask, out string? error)
        {
            error = null;
            GpuColumn? column = table.GetColumn(query.PredicateColumn);
            if (column == null)
            {
                error = $"Column '{query.PredicateColumn}' not found.";
                return false;
            }

            CUdeviceptr[] pointers = { column.DevicePointer, mask.DevicePointers[0] };
            (string kernel, object[] scalars) = query.Predicate switch
            {
                QueryPredicate.IntEquals => (KernelNames.FilterIntEquals, new object[] { table.RowCount, (int) query.PredicateMin }),
                QueryPredicate.IntRange => (KernelNames.FilterIntRange, new object[] { table.RowCount, (int) query.PredicateMin, (int) query.PredicateMax }),
                _ => (KernelNames.FilterFloatRange, new object[] { table.RowCount, (float) query.PredicateMin, (float) query.PredicateMax })
            };

            if (!this.executor.Execute(kernel, pointers, scalars, table.RowCount))
            {
                error = $"Filter kernel '{kernel}' failed.";
                return false;
            }

            return true;
        }

        private bool RunStringFilter(GpuTable table, GpuQuery query, CudaMem mask, QueryContext context, out string? error)
        {
            error = null;
            GpuStringColumn? column = table.GetStringColumn(query.PredicateColumn);
            if (column == null)
            {
                error = $"String column '{query.PredicateColumn}' not found.";
                return false;
            }

            byte[] patternBytes = Encoding.UTF8.GetBytes(query.PredicatePattern ?? string.Empty);
            CudaMem? pattern = this.cuda.PushData(patternBytes);
            if (pattern == null)
            {
                error = "Failed to upload search pattern.";
                return false;
            }

            context.Track(pattern);

            CUdeviceptr[] pointers =
            {
                column.ByteData.DevicePointer,
                column.Offsets.DevicePointer,
                column.Lengths.DevicePointer,
                pattern.DevicePointers[0],
                mask.DevicePointers[0]
            };

            (string kernel, object[] scalars) = query.Predicate == QueryPredicate.StringContains
                ? (KernelNames.StringContains, new object[] { table.RowCount, patternBytes.Length })
                : (KernelNames.StringFuzzyLevenshteinLimited, new object[] { table.RowCount, patternBytes.Length, query.FuzzyMaxDistance });

            if (!this.executor.Execute(kernel, pointers, scalars, table.RowCount))
            {
                error = $"String kernel '{kernel}' failed.";
                return false;
            }

            return true;
        }

        // ---- Aggregation ----

        private QueryResult Aggregate(GpuTable table, GpuQuery query, CudaMem? mask, QueryContext context)
        {
            byte[]? maskHost = this.ReadMask(mask);

            return query.Aggregate switch
            {
                QueryAggregate.None => new QueryResult { Success = true, Mask = maskHost, Count = this.CountHost(maskHost) },
                QueryAggregate.Count => new QueryResult { Success = true, Mask = maskHost, Count = this.CountHost(maskHost) ?? table.RowCount },
                QueryAggregate.Sum => this.SumAggregate(table, query, mask, context, maskHost),
                QueryAggregate.Average => this.AverageAggregate(table, query, mask, context, maskHost),
                QueryAggregate.MinMax => this.MinMaxAggregate(table, query, context, maskHost),
                _ => QueryResult.Fail("Unsupported aggregate.")
            };
        }

        private QueryResult SumAggregate(GpuTable table, GpuQuery query, CudaMem? mask, QueryContext context, byte[]? maskHost)
        {
            double? sum = this.ComputeSum(table, query, mask, context, out string? error);
            if (error != null)
            {
                return QueryResult.Fail(error);
            }

            return new QueryResult { Success = true, Mask = maskHost, Count = this.CountHost(maskHost), Value = sum };
        }

        private QueryResult AverageAggregate(GpuTable table, GpuQuery query, CudaMem? mask, QueryContext context, byte[]? maskHost)
        {
            double? sum = this.ComputeSum(table, query, mask, context, out string? error);
            if (error != null)
            {
                return QueryResult.Fail(error);
            }

            int count = this.CountHost(maskHost) ?? table.RowCount;
            double? average = count > 0 ? sum / count : 0.0;
            return new QueryResult { Success = true, Mask = maskHost, Count = count, Value = average };
        }

        private double? ComputeSum(GpuTable table, GpuQuery query, CudaMem? mask, QueryContext context, out string? error)
        {
            error = null;
            GpuColumn? column = table.GetColumn(query.AggregateColumn);
            if (column == null)
            {
                error = $"Aggregate column '{query.AggregateColumn}' not found.";
                return null;
            }

            CudaMem effectiveMask = mask ?? this.AllocateFullMask(table.RowCount, context, out error);
            if (error != null)
            {
                return null;
            }

            CudaMem? result = this.cuda.PushData(new float[] { 0.0f });
            if (result == null)
            {
                error = "Failed to allocate sum result.";
                return null;
            }

            context.Track(result);

            CUdeviceptr[] pointers = { column.DevicePointer, effectiveMask.DevicePointers[0], result.DevicePointers[0] };
            if (!this.executor.Execute(KernelNames.SumFloatByMask, pointers, new object[] { table.RowCount }, table.RowCount))
            {
                error = "Sum kernel failed.";
                return null;
            }

            float[]? scalar = this.cuda.PullData<float>(result.IndexPointer, keepBuffer: true);
            return (scalar != null && scalar.Length > 0) ? scalar[0] : 0.0;
        }

        private QueryResult MinMaxAggregate(GpuTable table, GpuQuery query, QueryContext context, byte[]? maskHost)
        {
            GpuColumn? column = table.GetColumn(query.AggregateColumn);
            if (column == null)
            {
                return QueryResult.Fail($"Aggregate column '{query.AggregateColumn}' not found.");
            }

            CudaMem? outMin = this.cuda.PushData(new float[] { float.MaxValue });
            CudaMem? outMax = this.cuda.PushData(new float[] { float.MinValue });
            if (outMin == null || outMax == null)
            {
                return QueryResult.Fail("Failed to allocate min/max result buffers.");
            }

            context.Track(outMin);
            context.Track(outMax);

            CUdeviceptr[] pointers = { column.DevicePointer, outMin.DevicePointers[0], outMax.DevicePointers[0] };
            if (!this.executor.Execute(KernelNames.MinMaxFloat, pointers, new object[] { table.RowCount }, table.RowCount))
            {
                return QueryResult.Fail("Min/max kernel failed.");
            }

            float[]? minHost = this.cuda.PullData<float>(outMin.IndexPointer, keepBuffer: true);
            float[]? maxHost = this.cuda.PullData<float>(outMax.IndexPointer, keepBuffer: true);
            return new QueryResult
            {
                Success = true,
                Mask = maskHost,
                Min = (minHost != null && minHost.Length > 0) ? minHost[0] : null,
                Max = (maxHost != null && maxHost.Length > 0) ? maxHost[0] : null
            };
        }

        // ---- Helpers ----

        private CudaMem AllocateFullMask(int rowCount, QueryContext context, out string? error)
        {
            error = null;
            byte[] ones = new byte[rowCount];
            Array.Fill(ones, (byte) 1);
            CudaMem? mask = this.cuda.PushData(ones);
            if (mask == null)
            {
                error = "Failed to allocate full selection mask.";
                return null!;
            }

            context.Track(mask);
            return mask;
        }

        private byte[]? ReadMask(CudaMem? mask)
            => mask == null ? null : this.cuda.PullData<byte>(mask.IndexPointer, keepBuffer: true);

        private int? CountHost(byte[]? maskHost)
        {
            if (maskHost == null)
            {
                return null;
            }

            int count = 0;
            foreach (byte value in maskHost)
            {
                if (value != 0)
                {
                    count++;
                }
            }

            return count;
        }
    }
}

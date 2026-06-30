using System;
using System.Text;
using AsynCUDA.OpenCl;

namespace AsynCUDA.ClDatabase
{
    /// <summary>
    /// Translates a structured <see cref="ClQuery"/> into a pipeline of OpenCL kernel invocations against
    /// the device-resident tables in a <see cref="ClTableCatalog"/>. Temporary buffers (mask, partial sums,
    /// pattern data, …) are tracked in a <see cref="ClQueryContext"/> and freed when the query ends.
    /// Persistent table columns are read without freeing so they remain resident on the device.
    /// </summary>
    public sealed class ClQueryEngine
    {
        private readonly OpenClRegister register;
        private readonly ClTableCatalog catalog;
        private readonly ClKernelExecutor executor;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClQueryEngine"/> class.
        /// </summary>
        /// <param name="register">The runtime registry for memory transfers and freeing.</param>
        /// <param name="catalog">The catalog resolving table/column names to device buffers.</param>
        /// <param name="executor">The multi-buffer kernel executor.</param>
        public ClQueryEngine(OpenClRegister register, ClTableCatalog catalog, ClKernelExecutor executor)
        {
            this.register = register ?? throw new ArgumentNullException(nameof(register));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        /// <summary>
        /// Executes the given query and returns its result.
        /// </summary>
        /// <param name="query">The query to execute.</param>
        /// <returns>The <see cref="ClQueryResult"/>; <see cref="ClQueryResult.Success"/> is <c>false</c> on failure.</returns>
        public ClQueryResult Execute(ClQuery query)
        {
            ClTable? table = this.catalog.GetTable(query.Table);
            if (table == null)
            {
                return ClQueryResult.Fail($"Table '{query.Table}' not found.");
            }

            using ClQueryContext context = new(this.register);
            try
            {
                OpenClMem? mask = this.BuildMask(table, query, context, out string? maskError);
                if (maskError != null)
                {
                    return ClQueryResult.Fail(maskError);
                }

                return this.Aggregate(table, query, mask, context);
            }
            catch (Exception ex)
            {
                return ClQueryResult.Fail(ex.Message);
            }
        }

        // ---- Filtering ----

        private OpenClMem? BuildMask(ClTable table, ClQuery query, ClQueryContext context, out string? error)
        {
            error = null;
            if (query.Predicate == ClQueryPredicate.None)
            {
                return null;
            }

            OpenClMem? mask = this.register.AllocateSingle<byte>(table.RowCount);
            if (mask == null)
            {
                error = "Failed to allocate mask buffer.";
                return null;
            }

            context.Track(mask);

            bool ok = query.Predicate switch
            {
                ClQueryPredicate.IntEquals or ClQueryPredicate.IntRange or ClQueryPredicate.FloatRange
                    => this.RunNumericFilter(table, query, mask, out error),
                ClQueryPredicate.StringContains or ClQueryPredicate.StringFuzzy
                    => this.RunStringFilter(table, query, mask, context, out error),
                _ => false
            };

            return ok ? mask : null;
        }

        private bool RunNumericFilter(ClTable table, ClQuery query, OpenClMem mask, out string? error)
        {
            error = null;
            ClColumn? column = table.GetColumn(query.PredicateColumn);
            if (column == null)
            {
                error = $"Column '{query.PredicateColumn}' not found.";
                return false;
            }

            OpenClMem[] buffers = { column.Memory, mask };
            (string kernel, object[] scalars) = query.Predicate switch
            {
                ClQueryPredicate.IntEquals => (ClKernelNames.FilterIntEquals, new object[] { table.RowCount, (int) query.PredicateMin }),
                ClQueryPredicate.IntRange => (ClKernelNames.FilterIntRange, new object[] { table.RowCount, (int) query.PredicateMin, (int) query.PredicateMax }),
                _ => (ClKernelNames.FilterFloatRange, new object[] { table.RowCount, (float) query.PredicateMin, (float) query.PredicateMax })
            };

            if (!this.executor.Execute(kernel, buffers, scalars, table.RowCount))
            {
                error = $"Filter kernel '{kernel}' failed.";
                return false;
            }

            return true;
        }

        private bool RunStringFilter(ClTable table, ClQuery query, OpenClMem mask, ClQueryContext context, out string? error)
        {
            error = null;
            ClStringColumn? column = table.GetStringColumn(query.PredicateColumn);
            if (column == null)
            {
                error = $"String column '{query.PredicateColumn}' not found.";
                return false;
            }

            byte[] patternBytes = Encoding.UTF8.GetBytes(query.PredicatePattern ?? string.Empty);
            OpenClMem? pattern = this.register.PushData(patternBytes);
            if (pattern == null)
            {
                error = "Failed to upload search pattern.";
                return false;
            }

            context.Track(pattern);

            OpenClMem[] buffers =
            {
                column.ByteData.Memory,
                column.Offsets.Memory,
                column.Lengths.Memory,
                pattern,
                mask
            };

            (string kernel, object[] scalars) = query.Predicate == ClQueryPredicate.StringContains
                ? (ClKernelNames.StringContains, new object[] { table.RowCount, patternBytes.Length })
                : (ClKernelNames.StringFuzzyLevenshteinLimited, new object[] { table.RowCount, patternBytes.Length, query.FuzzyMaxDistance });

            if (!this.executor.Execute(kernel, buffers, scalars, table.RowCount))
            {
                error = $"String kernel '{kernel}' failed.";
                return false;
            }

            return true;
        }

        // ---- Aggregation ----

        private ClQueryResult Aggregate(ClTable table, ClQuery query, OpenClMem? mask, ClQueryContext context)
        {
            byte[]? maskHost = this.ReadMask(mask);

            return query.Aggregate switch
            {
                ClQueryAggregate.None => new ClQueryResult { Success = true, Mask = maskHost, Count = this.CountHost(maskHost) },
                ClQueryAggregate.Count => new ClQueryResult { Success = true, Mask = maskHost, Count = this.CountHost(maskHost) ?? table.RowCount },
                ClQueryAggregate.Sum => this.SumAggregate(table, query, mask, context, maskHost),
                ClQueryAggregate.Average => this.AverageAggregate(table, query, mask, context, maskHost),
                ClQueryAggregate.MinMax => this.MinMaxAggregate(table, query, context, maskHost),
                _ => ClQueryResult.Fail("Unsupported aggregate.")
            };
        }

        private ClQueryResult SumAggregate(ClTable table, ClQuery query, OpenClMem? mask, ClQueryContext context, byte[]? maskHost)
        {
            double? sum = this.ComputeSum(table, query, mask, context, out string? error);
            if (error != null)
            {
                return ClQueryResult.Fail(error);
            }

            return new ClQueryResult { Success = true, Mask = maskHost, Count = this.CountHost(maskHost), Value = sum };
        }

        private ClQueryResult AverageAggregate(ClTable table, ClQuery query, OpenClMem? mask, ClQueryContext context, byte[]? maskHost)
        {
            double? sum = this.ComputeSum(table, query, mask, context, out string? error);
            if (error != null)
            {
                return ClQueryResult.Fail(error);
            }

            int count = this.CountHost(maskHost) ?? table.RowCount;
            double? average = count > 0 ? sum / count : 0.0;
            return new ClQueryResult { Success = true, Mask = maskHost, Count = count, Value = average };
        }

        private double? ComputeSum(ClTable table, ClQuery query, OpenClMem? mask, ClQueryContext context, out string? error)
        {
            error = null;
            ClColumn? column = table.GetColumn(query.AggregateColumn);
            if (column == null)
            {
                error = $"Aggregate column '{query.AggregateColumn}' not found.";
                return null;
            }

            OpenClMem effectiveMask = mask ?? this.AllocateFullMask(table.RowCount, context, out error);
            if (error != null)
            {
                return null;
            }

            OpenClMem? result = this.register.PushData(new float[] { 0.0f });
            if (result == null)
            {
                error = "Failed to allocate sum result.";
                return null;
            }

            context.Track(result);

            OpenClMem[] buffers = { column.Memory, effectiveMask, result };
            if (!this.executor.Execute(ClKernelNames.SumFloatByMask, buffers, new object[] { table.RowCount }, table.RowCount))
            {
                error = "Sum kernel failed.";
                return null;
            }

            float[]? scalar = this.register.PullData<float>(result);
            return (scalar != null && scalar.Length > 0) ? scalar[0] : 0.0;
        }

        private ClQueryResult MinMaxAggregate(ClTable table, ClQuery query, ClQueryContext context, byte[]? maskHost)
        {
            ClColumn? column = table.GetColumn(query.AggregateColumn);
            if (column == null)
            {
                return ClQueryResult.Fail($"Aggregate column '{query.AggregateColumn}' not found.");
            }

            OpenClMem? outMin = this.register.PushData(new float[] { float.MaxValue });
            OpenClMem? outMax = this.register.PushData(new float[] { float.MinValue });
            if (outMin == null || outMax == null)
            {
                return ClQueryResult.Fail("Failed to allocate min/max result buffers.");
            }

            context.Track(outMin);
            context.Track(outMax);

            OpenClMem[] buffers = { column.Memory, outMin, outMax };
            if (!this.executor.Execute(ClKernelNames.MinMaxFloat, buffers, new object[] { table.RowCount }, table.RowCount))
            {
                return ClQueryResult.Fail("Min/max kernel failed.");
            }

            float[]? minHost = this.register.PullData<float>(outMin);
            float[]? maxHost = this.register.PullData<float>(outMax);
            return new ClQueryResult
            {
                Success = true,
                Mask = maskHost,
                Min = (minHost != null && minHost.Length > 0) ? minHost[0] : null,
                Max = (maxHost != null && maxHost.Length > 0) ? maxHost[0] : null
            };
        }

        // ---- Helpers ----

        private OpenClMem AllocateFullMask(int rowCount, ClQueryContext context, out string? error)
        {
            error = null;
            byte[] ones = new byte[rowCount];
            Array.Fill(ones, (byte) 1);
            OpenClMem? mask = this.register.PushData(ones);
            if (mask == null)
            {
                error = "Failed to allocate full selection mask.";
                return null!;
            }

            context.Track(mask);
            return mask;
        }

        private byte[]? ReadMask(OpenClMem? mask)
            => mask == null ? null : this.register.PullData<byte>(mask);

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

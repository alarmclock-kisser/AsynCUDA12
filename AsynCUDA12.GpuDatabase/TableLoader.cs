using System;
using System.Collections.Generic;
using AsynCUDA12.Runtime;

namespace AsynCUDA12.GpuDatabase
{
    /// <summary>
    /// Transforms host tables into VRAM-resident <see cref="GpuTable"/>s and back. On upload, numeric
    /// columns are pushed directly and string columns are packed into the GPU-friendly UTF-8 byte +
    /// offsets + lengths layout. On download, the inverse transformation reconstructs a
    /// <see cref="HostTable"/>; persistent columns are pulled with <c>keepBuffer:true</c> so they stay
    /// resident unless explicitly freed.
    /// </summary>
    public sealed class TableLoader
    {
        private readonly CudaService cuda;

        /// <summary>
        /// Initializes a new instance of the <see cref="TableLoader"/> class.
        /// </summary>
        /// <param name="cuda">The runtime service used for VRAM transfers.</param>
        public TableLoader(CudaService cuda)
        {
            this.cuda = cuda ?? throw new ArgumentNullException(nameof(cuda));
        }

        // ---- Upload ----

        /// <summary>
        /// Uploads a host table to VRAM and returns the resulting <see cref="GpuTable"/>.
        /// </summary>
        /// <param name="host">The host table to upload.</param>
        /// <returns>The VRAM-resident table.</returns>
        public GpuTable Upload(HostTable host)
        {
            GpuTable table = new(host.Name, host.RowCount);
            foreach (HostColumn column in host.Columns)
            {
                if (column.Type == GpuColumnType.String)
                {
                    table.AddStringColumn(this.UploadStringColumn(column));
                }
                else
                {
                    table.AddColumn(this.UploadNumericColumn(column));
                }
            }

            return table;
        }

        private GpuColumn UploadNumericColumn(HostColumn column)
        {
            CudaMem memory = this.PushNumeric(column) ?? throw new InvalidOperationException($"Failed to upload column '{column.Name}'.");
            int elementSize = GpuColumnTypeInfo.ElementSize(column.Type);
            return new GpuColumn(column.Name, column.Type, column.Type, column.RowCount, elementSize, memory);
        }

        private CudaMem? PushNumeric(HostColumn column) => column.Type switch
        {
            GpuColumnType.Int32 => this.cuda.PushData(column.AsArray<int>()),
            GpuColumnType.Int64 => this.cuda.PushData(column.AsArray<long>()),
            GpuColumnType.Single => this.cuda.PushData(column.AsArray<float>()),
            GpuColumnType.Double => this.cuda.PushData(column.AsArray<double>()),
            GpuColumnType.Byte => this.cuda.PushData(column.AsArray<byte>()),
            _ => null
        };

        private GpuStringColumn UploadStringColumn(HostColumn column)
        {
            (byte[] data, int[] offsets, int[] lengths) = PersistenceManager.EncodeStrings(column.AsStrings());

            CudaMem dataMem = this.cuda.PushData(data) ?? throw new InvalidOperationException($"Failed to upload string data of '{column.Name}'.");
            CudaMem offsetsMem = this.cuda.PushData(offsets) ?? throw new InvalidOperationException($"Failed to upload offsets of '{column.Name}'.");
            CudaMem lengthsMem = this.cuda.PushData(lengths) ?? throw new InvalidOperationException($"Failed to upload lengths of '{column.Name}'.");

            GpuColumn byteData = new(column.Name + ".data", GpuColumnType.Byte, GpuColumnType.Byte, data.Length, sizeof(byte), dataMem);
            GpuColumn offsetsColumn = new(column.Name + ".offsets", GpuColumnType.Int32, GpuColumnType.Int32, offsets.Length, sizeof(int), offsetsMem);
            GpuColumn lengthsColumn = new(column.Name + ".lengths", GpuColumnType.Int32, GpuColumnType.Int32, lengths.Length, sizeof(int), lengthsMem);

            return new GpuStringColumn(column.Name, column.RowCount, byteData, offsetsColumn, lengthsColumn);
        }

        // ---- Download ----

        /// <summary>
        /// Downloads a VRAM-resident table back into a host table, keeping the device buffers resident.
        /// </summary>
        /// <param name="table">The VRAM table to read.</param>
        /// <returns>The reconstructed host table.</returns>
        public HostTable Download(GpuTable table)
        {
            HostTable host = new(table.Name, table.RowCount);
            foreach (GpuColumn column in table.ScalarColumns)
            {
                host.Add(this.DownloadNumericColumn(column));
            }

            foreach (GpuStringColumn column in table.StringColumns)
            {
                host.Add(this.DownloadStringColumn(column));
            }

            return host;
        }

        private HostColumn DownloadNumericColumn(GpuColumn column)
        {
            Array data = this.PullNumeric(column) ?? throw new InvalidOperationException($"Failed to download column '{column.Name}'.");
            return HostColumn.Numeric(column.Name, column.LogicalType, data);
        }

        private Array? PullNumeric(GpuColumn column) => column.LogicalType switch
        {
            GpuColumnType.Int32 => this.cuda.PullData<int>(column.IndexPointer, keepBuffer: true),
            GpuColumnType.Int64 => this.cuda.PullData<long>(column.IndexPointer, keepBuffer: true),
            GpuColumnType.Single => this.cuda.PullData<float>(column.IndexPointer, keepBuffer: true),
            GpuColumnType.Double => this.cuda.PullData<double>(column.IndexPointer, keepBuffer: true),
            GpuColumnType.Byte => this.cuda.PullData<byte>(column.IndexPointer, keepBuffer: true),
            _ => null
        };

        private HostColumn DownloadStringColumn(GpuStringColumn column)
        {
            byte[] data = this.cuda.PullData<byte>(column.ByteData.IndexPointer, keepBuffer: true) ?? Array.Empty<byte>();
            int[] offsets = this.cuda.PullData<int>(column.Offsets.IndexPointer, keepBuffer: true) ?? Array.Empty<int>();
            int[] lengths = this.cuda.PullData<int>(column.Lengths.IndexPointer, keepBuffer: true) ?? Array.Empty<int>();

            return HostColumn.Strings(column.Name, PersistenceManager.DecodeStrings(data, offsets, lengths));
        }
    }
}

using System;
using AsynCUDA.OpenCl;

namespace AsynCUDA.ClDatabase
{
    /// <summary>
    /// Transforms host tables into device-resident <see cref="ClTable"/>s and back. On upload, numeric
    /// columns are pushed directly and string columns are packed into the GPU-friendly UTF-8 byte +
    /// offsets + lengths layout. On download, the inverse transformation reconstructs a
    /// <see cref="ClHostTable"/>; reading a buffer with <see cref="OpenClRegister.PullData{T}(OpenClMem)"/>
    /// never frees it, so columns stay resident unless explicitly freed.
    /// </summary>
    public sealed class ClTableLoader
    {
        private readonly OpenClRegister register;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClTableLoader"/> class.
        /// </summary>
        /// <param name="register">The runtime registry used for device-memory transfers.</param>
        public ClTableLoader(OpenClRegister register)
        {
            this.register = register ?? throw new ArgumentNullException(nameof(register));
        }

        // ---- Upload ----

        /// <summary>
        /// Uploads a host table to device memory and returns the resulting <see cref="ClTable"/>.
        /// </summary>
        /// <param name="host">The host table to upload.</param>
        /// <returns>The device-resident table.</returns>
        public ClTable Upload(ClHostTable host)
        {
            ClTable table = new(host.Name, host.RowCount);
            foreach (ClHostColumn column in host.Columns)
            {
                if (column.Type == ClColumnType.String)
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

        private ClColumn UploadNumericColumn(ClHostColumn column)
        {
            OpenClMem memory = this.PushNumeric(column) ?? throw new InvalidOperationException($"Failed to upload column '{column.Name}'.");
            int elementSize = ClColumnTypeInfo.ElementSize(column.Type);
            return new ClColumn(column.Name, column.Type, column.Type, column.RowCount, elementSize, memory);
        }

        private OpenClMem? PushNumeric(ClHostColumn column) => column.Type switch
        {
            ClColumnType.Int32 => this.register.PushData(column.AsArray<int>()),
            ClColumnType.Int64 => this.register.PushData(column.AsArray<long>()),
            ClColumnType.Single => this.register.PushData(column.AsArray<float>()),
            ClColumnType.Double => this.register.PushData(column.AsArray<double>()),
            ClColumnType.Byte => this.register.PushData(column.AsArray<byte>()),
            _ => null
        };

        private ClStringColumn UploadStringColumn(ClHostColumn column)
        {
            (byte[] data, int[] offsets, int[] lengths) = ClPersistenceManager.EncodeStrings(column.AsStrings());

            OpenClMem dataMem = this.register.PushData(data) ?? throw new InvalidOperationException($"Failed to upload string data of '{column.Name}'.");
            OpenClMem offsetsMem = this.register.PushData(offsets) ?? throw new InvalidOperationException($"Failed to upload offsets of '{column.Name}'.");
            OpenClMem lengthsMem = this.register.PushData(lengths) ?? throw new InvalidOperationException($"Failed to upload lengths of '{column.Name}'.");

            ClColumn byteData = new(column.Name + ".data", ClColumnType.Byte, ClColumnType.Byte, data.Length, sizeof(byte), dataMem);
            ClColumn offsetsColumn = new(column.Name + ".offsets", ClColumnType.Int32, ClColumnType.Int32, offsets.Length, sizeof(int), offsetsMem);
            ClColumn lengthsColumn = new(column.Name + ".lengths", ClColumnType.Int32, ClColumnType.Int32, lengths.Length, sizeof(int), lengthsMem);

            return new ClStringColumn(column.Name, column.RowCount, byteData, offsetsColumn, lengthsColumn);
        }

        // ---- Download ----

        /// <summary>
        /// Downloads a device-resident table back into a host table, keeping the device buffers resident.
        /// </summary>
        /// <param name="table">The device table to read.</param>
        /// <returns>The reconstructed host table.</returns>
        public ClHostTable Download(ClTable table)
        {
            ClHostTable host = new(table.Name, table.RowCount);
            foreach (ClColumn column in table.ScalarColumns)
            {
                host.Add(this.DownloadNumericColumn(column));
            }

            foreach (ClStringColumn column in table.StringColumns)
            {
                host.Add(this.DownloadStringColumn(column));
            }

            return host;
        }

        private ClHostColumn DownloadNumericColumn(ClColumn column)
        {
            Array data = this.PullNumeric(column) ?? throw new InvalidOperationException($"Failed to download column '{column.Name}'.");
            return ClHostColumn.Numeric(column.Name, column.LogicalType, data);
        }

        private Array? PullNumeric(ClColumn column) => column.LogicalType switch
        {
            ClColumnType.Int32 => this.register.PullData<int>(column.Memory),
            ClColumnType.Int64 => this.register.PullData<long>(column.Memory),
            ClColumnType.Single => this.register.PullData<float>(column.Memory),
            ClColumnType.Double => this.register.PullData<double>(column.Memory),
            ClColumnType.Byte => this.register.PullData<byte>(column.Memory),
            _ => null
        };

        private ClHostColumn DownloadStringColumn(ClStringColumn column)
        {
            byte[] data = this.register.PullData<byte>(column.ByteData.Memory) ?? Array.Empty<byte>();
            int[] offsets = this.register.PullData<int>(column.Offsets.Memory) ?? Array.Empty<int>();
            int[] lengths = this.register.PullData<int>(column.Lengths.Memory) ?? Array.Empty<int>();

            return ClHostColumn.Strings(column.Name, ClPersistenceManager.DecodeStrings(data, offsets, lengths));
        }
    }
}

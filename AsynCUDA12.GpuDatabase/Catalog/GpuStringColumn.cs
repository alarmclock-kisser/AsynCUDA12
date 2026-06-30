namespace AsynCUDA12.GpuDatabase
{
    /// <summary>
    /// Represents a string column resident in GPU VRAM, composed of three device buffers following the
    /// GPU-friendly string layout: a contiguous UTF-8 byte buffer, an integer offsets buffer and an
    /// integer lengths buffer. An optional dictionary buffer (dictionary encoding) may be added later.
    /// </summary>
    public sealed class GpuStringColumn
    {
        /// <summary>Gets the column name.</summary>
        public string Name { get; }

        /// <summary>Gets the number of rows (strings) in the column.</summary>
        public int RowCount { get; }

        /// <summary>Gets the contiguous UTF-8 byte data buffer.</summary>
        public GpuColumn ByteData { get; }

        /// <summary>Gets the per-row start offsets into <see cref="ByteData"/>.</summary>
        public GpuColumn Offsets { get; }

        /// <summary>Gets the per-row byte lengths.</summary>
        public GpuColumn Lengths { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GpuStringColumn"/> class.
        /// </summary>
        /// <param name="name">The column name.</param>
        /// <param name="rowCount">The number of rows.</param>
        /// <param name="byteData">The UTF-8 byte data buffer.</param>
        /// <param name="offsets">The offsets buffer.</param>
        /// <param name="lengths">The lengths buffer.</param>
        public GpuStringColumn(string name, int rowCount, GpuColumn byteData, GpuColumn offsets, GpuColumn lengths)
        {
            this.Name = name;
            this.RowCount = rowCount;
            this.ByteData = byteData;
            this.Offsets = offsets;
            this.Lengths = lengths;
        }
    }
}

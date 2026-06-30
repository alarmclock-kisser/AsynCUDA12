namespace AsynCUDA.ClDatabase
{
    /// <summary>
    /// Represents a string column resident in OpenCL device memory, composed of three device buffers
    /// following the GPU-friendly string layout: a contiguous UTF-8 byte buffer, an integer offsets buffer
    /// and an integer lengths buffer. An optional dictionary buffer (dictionary encoding) may be added later.
    /// </summary>
    public sealed class ClStringColumn
    {
        /// <summary>Gets the column name.</summary>
        public string Name { get; }

        /// <summary>Gets the number of rows (strings) in the column.</summary>
        public int RowCount { get; }

        /// <summary>Gets the contiguous UTF-8 byte data buffer.</summary>
        public ClColumn ByteData { get; }

        /// <summary>Gets the per-row start offsets into <see cref="ByteData"/>.</summary>
        public ClColumn Offsets { get; }

        /// <summary>Gets the per-row byte lengths.</summary>
        public ClColumn Lengths { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClStringColumn"/> class.
        /// </summary>
        /// <param name="name">The column name.</param>
        /// <param name="rowCount">The number of rows.</param>
        /// <param name="byteData">The UTF-8 byte data buffer.</param>
        /// <param name="offsets">The offsets buffer.</param>
        /// <param name="lengths">The lengths buffer.</param>
        public ClStringColumn(string name, int rowCount, ClColumn byteData, ClColumn offsets, ClColumn lengths)
        {
            this.Name = name;
            this.RowCount = rowCount;
            this.ByteData = byteData;
            this.Offsets = offsets;
            this.Lengths = lengths;
        }
    }
}

namespace AsynCUDA12.GpuDatabase
{
    /// <summary>
    /// Enumerates the logical/physical column element types supported by the GPU database.
    /// Numeric types map directly to unmanaged device buffers; <see cref="String"/> is stored
    /// physically as a UTF-8 byte buffer plus integer offsets and lengths.
    /// </summary>
    public enum GpuColumnType
    {
        /// <summary>32-bit signed integer (<see cref="int"/>).</summary>
        Int32,

        /// <summary>64-bit signed integer (<see cref="long"/>).</summary>
        Int64,

        /// <summary>32-bit floating point (<see cref="float"/>).</summary>
        Single,

        /// <summary>64-bit floating point (<see cref="double"/>).</summary>
        Double,

        /// <summary>Raw byte (<see cref="byte"/>).</summary>
        Byte,

        /// <summary>UTF-8 string column (byte data + offsets + lengths).</summary>
        String
    }

    /// <summary>
    /// Helper methods describing the physical characteristics of a <see cref="GpuColumnType"/>.
    /// </summary>
    public static class GpuColumnTypeInfo
    {
        /// <summary>
        /// Gets the size in bytes of a single element of the given numeric type.
        /// </summary>
        /// <param name="type">The column type to inspect.</param>
        /// <returns>The element size in bytes; 1 for <see cref="GpuColumnType.String"/> (byte data).</returns>
        public static int ElementSize(GpuColumnType type) => type switch
        {
            GpuColumnType.Int32 => sizeof(int),
            GpuColumnType.Int64 => sizeof(long),
            GpuColumnType.Single => sizeof(float),
            GpuColumnType.Double => sizeof(double),
            GpuColumnType.Byte => sizeof(byte),
            GpuColumnType.String => sizeof(byte),
            _ => 0
        };

        /// <summary>
        /// Gets a value indicating whether the type is a fixed-size numeric type
        /// (i.e. not <see cref="GpuColumnType.String"/>).
        /// </summary>
        /// <param name="type">The column type to inspect.</param>
        /// <returns><c>true</c> for numeric/byte types; otherwise <c>false</c>.</returns>
        public static bool IsNumeric(GpuColumnType type) => type != GpuColumnType.String;
    }
}

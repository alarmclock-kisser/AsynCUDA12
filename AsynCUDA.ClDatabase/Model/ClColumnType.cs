namespace AsynCUDA.ClDatabase
{
    /// <summary>
    /// Enumerates the logical/physical column element types supported by the OpenCL database.
    /// Numeric types map directly to unmanaged device buffers; <see cref="String"/> is stored
    /// physically as a UTF-8 byte buffer plus integer offsets and lengths.
    /// </summary>
    public enum ClColumnType
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
    /// Helper methods describing the physical characteristics of a <see cref="ClColumnType"/>.
    /// </summary>
    public static class ClColumnTypeInfo
    {
        /// <summary>
        /// Gets the size in bytes of a single element of the given numeric type.
        /// </summary>
        /// <param name="type">The column type to inspect.</param>
        /// <returns>The element size in bytes; 1 for <see cref="ClColumnType.String"/> (byte data).</returns>
        public static int ElementSize(ClColumnType type) => type switch
        {
            ClColumnType.Int32 => sizeof(int),
            ClColumnType.Int64 => sizeof(long),
            ClColumnType.Single => sizeof(float),
            ClColumnType.Double => sizeof(double),
            ClColumnType.Byte => sizeof(byte),
            ClColumnType.String => sizeof(byte),
            _ => 0
        };

        /// <summary>
        /// Gets a value indicating whether the type is a fixed-size numeric type
        /// (i.e. not <see cref="ClColumnType.String"/>).
        /// </summary>
        /// <param name="type">The column type to inspect.</param>
        /// <returns><c>true</c> for numeric/byte types; otherwise <c>false</c>.</returns>
        public static bool IsNumeric(ClColumnType type) => type != ClColumnType.String;
    }
}

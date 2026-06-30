using System;

namespace AsynCUDA12.GpuDatabase
{
    /// <summary>
    /// Represents a single column held in host (CPU) memory, used as the transfer/transformation
    /// stage between the persisted database file and the GPU VRAM buffers. Numeric columns store a
    /// typed array (for example <see cref="int"/>[]); string columns store a <see cref="string"/>[].
    /// </summary>
    public sealed class HostColumn
    {
        /// <summary>Gets the column name.</summary>
        public string Name { get; }

        /// <summary>Gets the logical column type.</summary>
        public GpuColumnType Type { get; }

        /// <summary>Gets the backing data array (a numeric array or a <see cref="string"/>[]).</summary>
        public Array Data { get; }

        /// <summary>Gets the number of rows (elements) in the column.</summary>
        public int RowCount => this.Data.Length;

        private HostColumn(string name, GpuColumnType type, Array data)
        {
            this.Name = name;
            this.Type = type;
            this.Data = data;
        }

        /// <summary>
        /// Creates a numeric host column from a typed array. The array element type must match
        /// the supplied <paramref name="type"/>.
        /// </summary>
        /// <param name="name">The column name.</param>
        /// <param name="type">The numeric column type.</param>
        /// <param name="data">The typed backing array.</param>
        /// <returns>The created <see cref="HostColumn"/>.</returns>
        public static HostColumn Numeric(string name, GpuColumnType type, Array data)
        {
            if (type == GpuColumnType.String)
            {
                throw new ArgumentException("Use HostColumn.Strings for string columns.", nameof(type));
            }

            return new HostColumn(name, type, data);
        }

        /// <summary>
        /// Creates a string host column from a <see cref="string"/>[].
        /// </summary>
        /// <param name="name">The column name.</param>
        /// <param name="values">The string values.</param>
        /// <returns>The created <see cref="HostColumn"/>.</returns>
        public static HostColumn Strings(string name, string[] values)
        {
            return new HostColumn(name, GpuColumnType.String, values);
        }

        /// <summary>Gets the backing data interpreted as a typed array.</summary>
        /// <typeparam name="T">The expected element type.</typeparam>
        /// <returns>The typed array.</returns>
        public T[] AsArray<T>() => (T[]) this.Data;

        /// <summary>Gets the backing data interpreted as a <see cref="string"/>[].</summary>
        /// <returns>The string array.</returns>
        public string[] AsStrings() => (string[]) this.Data;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace AsynCUDA12.GpuDatabase
{
    /// <summary>
    /// Represents a table resident in GPU VRAM. Owns its scalar <see cref="GpuColumn"/>s and any complex
    /// <see cref="GpuStringColumn"/>s, all sharing the same <see cref="RowCount"/>, and exposes lookups
    /// for resolving column names to device buffers.
    /// </summary>
    public sealed class GpuTable
    {
        private readonly Dictionary<string, GpuColumn> columns = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, GpuStringColumn> stringColumns = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Gets the table name.</summary>
        public string Name { get; }

        /// <summary>Gets the number of rows in the table.</summary>
        public int RowCount { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GpuTable"/> class.
        /// </summary>
        /// <param name="name">The table name.</param>
        /// <param name="rowCount">The number of rows.</param>
        public GpuTable(string name, int rowCount)
        {
            this.Name = name;
            this.RowCount = rowCount;
        }

        /// <summary>Gets the scalar columns of the table.</summary>
        public IReadOnlyCollection<GpuColumn> ScalarColumns => this.columns.Values;

        /// <summary>Gets the string columns of the table.</summary>
        public IReadOnlyCollection<GpuStringColumn> StringColumns => this.stringColumns.Values;

        /// <summary>Adds a scalar column to the table.</summary>
        /// <param name="column">The column to add.</param>
        public void AddColumn(GpuColumn column) => this.columns[column.Name] = column;

        /// <summary>Adds a string column to the table.</summary>
        /// <param name="column">The string column to add.</param>
        public void AddStringColumn(GpuStringColumn column) => this.stringColumns[column.Name] = column;

        /// <summary>Gets a scalar column by name, or <c>null</c> if not found.</summary>
        /// <param name="name">The column name.</param>
        /// <returns>The matching <see cref="GpuColumn"/>, or <c>null</c>.</returns>
        public GpuColumn? GetColumn(string name) => this.columns.GetValueOrDefault(name);

        /// <summary>Gets a string column by name, or <c>null</c> if not found.</summary>
        /// <param name="name">The column name.</param>
        /// <returns>The matching <see cref="GpuStringColumn"/>, or <c>null</c>.</returns>
        public GpuStringColumn? GetStringColumn(string name) => this.stringColumns.GetValueOrDefault(name);

        /// <summary>
        /// Enumerates every device-backed <see cref="GpuColumn"/> of the table, including the
        /// physical components of string columns (used when freeing or snapshotting all VRAM).
        /// </summary>
        /// <returns>All backing <see cref="GpuColumn"/>s.</returns>
        public IEnumerable<GpuColumn> EnumerateBackingColumns()
        {
            foreach (GpuColumn column in this.columns.Values)
            {
                yield return column;
            }

            foreach (GpuStringColumn stringColumn in this.stringColumns.Values)
            {
                yield return stringColumn.ByteData;
                yield return stringColumn.Offsets;
                yield return stringColumn.Lengths;
            }
        }
    }
}

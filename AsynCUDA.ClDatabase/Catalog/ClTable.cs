using System;
using System.Collections.Generic;

namespace AsynCUDA.ClDatabase
{
    /// <summary>
    /// Represents a table resident in OpenCL device memory. Owns its scalar <see cref="ClColumn"/>s and any
    /// complex <see cref="ClStringColumn"/>s, all sharing the same <see cref="RowCount"/>, and exposes
    /// lookups for resolving column names to device buffers.
    /// </summary>
    public sealed class ClTable
    {
        private readonly Dictionary<string, ClColumn> columns = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ClStringColumn> stringColumns = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Gets the table name.</summary>
        public string Name { get; }

        /// <summary>Gets the number of rows in the table.</summary>
        public int RowCount { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClTable"/> class.
        /// </summary>
        /// <param name="name">The table name.</param>
        /// <param name="rowCount">The number of rows.</param>
        public ClTable(string name, int rowCount)
        {
            this.Name = name;
            this.RowCount = rowCount;
        }

        /// <summary>Gets the scalar columns of the table.</summary>
        public IReadOnlyCollection<ClColumn> ScalarColumns => this.columns.Values;

        /// <summary>Gets the string columns of the table.</summary>
        public IReadOnlyCollection<ClStringColumn> StringColumns => this.stringColumns.Values;

        /// <summary>Adds a scalar column to the table.</summary>
        /// <param name="column">The column to add.</param>
        public void AddColumn(ClColumn column) => this.columns[column.Name] = column;

        /// <summary>Adds a string column to the table.</summary>
        /// <param name="column">The string column to add.</param>
        public void AddStringColumn(ClStringColumn column) => this.stringColumns[column.Name] = column;

        /// <summary>Gets a scalar column by name, or <c>null</c> if not found.</summary>
        /// <param name="name">The column name.</param>
        /// <returns>The matching <see cref="ClColumn"/>, or <c>null</c>.</returns>
        public ClColumn? GetColumn(string name) => this.columns.GetValueOrDefault(name);

        /// <summary>Gets a string column by name, or <c>null</c> if not found.</summary>
        /// <param name="name">The column name.</param>
        /// <returns>The matching <see cref="ClStringColumn"/>, or <c>null</c>.</returns>
        public ClStringColumn? GetStringColumn(string name) => this.stringColumns.GetValueOrDefault(name);

        /// <summary>
        /// Enumerates every device-backed <see cref="ClColumn"/> of the table, including the
        /// physical components of string columns (used when freeing or snapshotting all device memory).
        /// </summary>
        /// <returns>All backing <see cref="ClColumn"/>s.</returns>
        public IEnumerable<ClColumn> EnumerateBackingColumns()
        {
            foreach (ClColumn column in this.columns.Values)
            {
                yield return column;
            }

            foreach (ClStringColumn stringColumn in this.stringColumns.Values)
            {
                yield return stringColumn.ByteData;
                yield return stringColumn.Offsets;
                yield return stringColumn.Lengths;
            }
        }
    }
}

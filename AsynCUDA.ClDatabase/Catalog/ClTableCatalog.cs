using System;
using System.Collections.Generic;

namespace AsynCUDA.ClDatabase
{
    /// <summary>
    /// Manages all tables resident in device memory and resolves table/column names to their OpenCL
    /// buffers. Lookups are case-insensitive.
    /// </summary>
    public sealed class ClTableCatalog
    {
        private readonly Dictionary<string, ClTable> tables = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Gets all registered tables.</summary>
        public IReadOnlyCollection<ClTable> Tables => this.tables.Values;

        /// <summary>Gets the number of registered tables.</summary>
        public int Count => this.tables.Count;

        /// <summary>Registers (or replaces) a table.</summary>
        /// <param name="table">The table to add.</param>
        public void Add(ClTable table) => this.tables[table.Name] = table;

        /// <summary>Removes a table by name.</summary>
        /// <param name="name">The table name.</param>
        /// <returns><c>true</c> if a table was removed; otherwise <c>false</c>.</returns>
        public bool Remove(string name) => this.tables.Remove(name);

        /// <summary>Gets a table by name, or <c>null</c> if not found.</summary>
        /// <param name="name">The table name.</param>
        /// <returns>The matching <see cref="ClTable"/>, or <c>null</c>.</returns>
        public ClTable? GetTable(string name) => this.tables.GetValueOrDefault(name);

        /// <summary>
        /// Resolves a table and scalar column name to the backing <see cref="ClColumn"/>.
        /// </summary>
        /// <param name="tableName">The table name.</param>
        /// <param name="columnName">The column name.</param>
        /// <returns>The matching <see cref="ClColumn"/>, or <c>null</c> if either is not found.</returns>
        public ClColumn? ResolveColumn(string tableName, string columnName)
            => this.GetTable(tableName)?.GetColumn(columnName);

        /// <summary>Removes all tables from the catalog.</summary>
        public void Clear() => this.tables.Clear();
    }
}

using System.Collections.Generic;

namespace AsynCUDA12.GpuDatabase
{
    /// <summary>
    /// Represents a table in host (CPU) memory: a named, ordered collection of <see cref="HostColumn"/>s
    /// that all share the same row count. Used while loading from or saving to the persisted database file.
    /// </summary>
    public sealed class HostTable
    {
        /// <summary>Gets the table name.</summary>
        public string Name { get; }

        /// <summary>Gets the number of rows in the table.</summary>
        public int RowCount { get; }

        /// <summary>Gets the ordered list of columns.</summary>
        public List<HostColumn> Columns { get; } = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="HostTable"/> class.
        /// </summary>
        /// <param name="name">The table name.</param>
        /// <param name="rowCount">The number of rows.</param>
        public HostTable(string name, int rowCount)
        {
            this.Name = name;
            this.RowCount = rowCount;
        }

        /// <summary>
        /// Adds a column to the table and returns the same instance for fluent chaining.
        /// </summary>
        /// <param name="column">The column to add.</param>
        /// <returns>This <see cref="HostTable"/> instance.</returns>
        public HostTable Add(HostColumn column)
        {
            this.Columns.Add(column);
            return this;
        }
    }
}

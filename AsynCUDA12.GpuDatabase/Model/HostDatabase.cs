using System.Collections.Generic;

namespace AsynCUDA12.GpuDatabase
{
    /// <summary>
    /// Represents a full database in host (CPU) memory: an ordered collection of <see cref="HostTable"/>s.
    /// This is the in-memory representation produced by reading a persisted database file and the
    /// representation written back to disk on save/checkpoint.
    /// </summary>
    public sealed class HostDatabase
    {
        /// <summary>Gets the tables contained in this database.</summary>
        public List<HostTable> Tables { get; } = new();

        /// <summary>
        /// Adds a table to the database and returns the same instance for fluent chaining.
        /// </summary>
        /// <param name="table">The table to add.</param>
        /// <returns>This <see cref="HostDatabase"/> instance.</returns>
        public HostDatabase Add(HostTable table)
        {
            this.Tables.Add(table);
            return this;
        }
    }
}

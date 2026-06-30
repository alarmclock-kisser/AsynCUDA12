using System.Collections.Generic;

namespace AsynCUDA.ClDatabase
{
    /// <summary>
    /// Represents a full database in host (CPU) memory: an ordered collection of <see cref="ClHostTable"/>s.
    /// This is the in-memory representation produced by reading a persisted database file and the
    /// representation written back to disk on save/checkpoint.
    /// </summary>
    public sealed class ClHostDatabase
    {
        /// <summary>Gets the tables contained in this database.</summary>
        public List<ClHostTable> Tables { get; } = new();

        /// <summary>
        /// Adds a table to the database and returns the same instance for fluent chaining.
        /// </summary>
        /// <param name="table">The table to add.</param>
        /// <returns>This <see cref="ClHostDatabase"/> instance.</returns>
        public ClHostDatabase Add(ClHostTable table)
        {
            this.Tables.Add(table);
            return this;
        }
    }
}

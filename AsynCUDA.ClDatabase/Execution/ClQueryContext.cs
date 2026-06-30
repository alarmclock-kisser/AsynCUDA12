using System;
using System.Collections.Generic;
using AsynCUDA.OpenCl;

namespace AsynCUDA.ClDatabase
{
    /// <summary>
    /// Tracks the temporary <see cref="OpenClMem"/> allocations created during a single query (masks,
    /// partial sums, hash tables, row-id buffers, …) so they can be freed deterministically when the
    /// query completes or fails. Persistent table columns are never registered here and therefore never
    /// freed by the context.
    /// </summary>
    public sealed class ClQueryContext : IDisposable
    {
        private readonly OpenClRegister register;
        private readonly List<OpenClMem> temporaries = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="ClQueryContext"/> class.
        /// </summary>
        /// <param name="register">The runtime registry used to free tracked allocations.</param>
        public ClQueryContext(OpenClRegister register)
        {
            this.register = register ?? throw new ArgumentNullException(nameof(register));
        }

        /// <summary>Gets the number of temporary allocations currently tracked.</summary>
        public int TemporaryCount => this.temporaries.Count;

        /// <summary>
        /// Registers a temporary allocation for cleanup and returns it for fluent use.
        /// </summary>
        /// <param name="memory">The allocation to track.</param>
        /// <returns>The same <paramref name="memory"/> instance.</returns>
        public OpenClMem Track(OpenClMem memory)
        {
            this.temporaries.Add(memory);
            return memory;
        }

        /// <summary>Frees all tracked temporary allocations and clears the tracking list.</summary>
        public void Dispose()
        {
            foreach (OpenClMem memory in this.temporaries)
            {
                this.register.Free(memory);
            }

            this.temporaries.Clear();
        }
    }
}

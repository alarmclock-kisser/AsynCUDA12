using System;
using System.Collections.Generic;
using AsynCUDA12.Runtime;

namespace AsynCUDA12.GpuDatabase
{
    /// <summary>
    /// Tracks the temporary <see cref="CudaMem"/> allocations created during a single query (masks,
    /// partial sums, hash tables, row-id buffers, …) so they can be freed deterministically when the
    /// query completes or fails. Persistent table columns are never registered here and therefore never
    /// freed by the context.
    /// </summary>
    public sealed class QueryContext : IDisposable
    {
        private readonly CudaService cuda;
        private readonly List<CudaMem> temporaries = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="QueryContext"/> class.
        /// </summary>
        /// <param name="cuda">The runtime service used to free tracked allocations.</param>
        public QueryContext(CudaService cuda)
        {
            this.cuda = cuda ?? throw new ArgumentNullException(nameof(cuda));
        }

        /// <summary>Gets the number of temporary allocations currently tracked.</summary>
        public int TemporaryCount => this.temporaries.Count;

        /// <summary>
        /// Registers a temporary allocation for cleanup and returns it for fluent use.
        /// </summary>
        /// <param name="memory">The allocation to track.</param>
        /// <returns>The same <paramref name="memory"/> instance.</returns>
        public CudaMem Track(CudaMem memory)
        {
            this.temporaries.Add(memory);
            return memory;
        }

        /// <summary>Frees all tracked temporary allocations and clears the tracking list.</summary>
        public void Dispose()
        {
            foreach (CudaMem memory in this.temporaries)
            {
                this.cuda.FreeMemory(memory);
            }

            this.temporaries.Clear();
        }
    }
}

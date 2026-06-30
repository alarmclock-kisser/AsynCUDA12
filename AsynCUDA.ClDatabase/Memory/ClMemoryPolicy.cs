using AsynCUDA.OpenCl;

namespace AsynCUDA.ClDatabase
{
    /// <summary>
    /// Encapsulates the rules that govern device-memory usage for the OpenCL database: an optional soft
    /// budget for total device allocation and a simple hot/cold decision used to decide whether a table
    /// should be kept resident in device memory. The policy never frees memory itself; it only advises the
    /// <see cref="ClDatabase"/> facade, keeping the runtime APIs untouched.
    /// </summary>
    public sealed class ClMemoryPolicy
    {
        /// <summary>
        /// Gets or sets the soft device-memory budget in bytes. A non-positive value disables budget checks.
        /// </summary>
        public long VramBudgetBytes { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether tables are kept resident in device memory after loading
        /// (hot storage). When <c>false</c>, callers may choose to keep cold tables in host memory.
        /// </summary>
        public bool KeepTablesHot { get; set; } = true;

        /// <summary>
        /// Determines whether allocating <paramref name="additionalBytes"/> more bytes would still fit
        /// within the configured budget given the current allocation reported by the runtime.
        /// </summary>
        /// <param name="service">The runtime service whose <c>TotalAllocated</c> is consulted.</param>
        /// <param name="additionalBytes">The number of additional bytes about to be allocated.</param>
        /// <returns><c>true</c> if within budget (or budget disabled); otherwise <c>false</c>.</returns>
        public bool CanAllocate(OpenClService service, long additionalBytes)
            => this.CanAllocate(service.TotalAllocated, additionalBytes);

        /// <summary>
        /// Determines whether allocating <paramref name="additionalBytes"/> more bytes would still fit
        /// within the configured budget given an explicit current allocation. This overload keeps the
        /// budget arithmetic independent of any runtime service so it can be evaluated without a GPU.
        /// </summary>
        /// <param name="currentAllocatedBytes">The number of bytes already allocated.</param>
        /// <param name="additionalBytes">The number of additional bytes about to be allocated.</param>
        /// <returns><c>true</c> if within budget (or budget disabled); otherwise <c>false</c>.</returns>
        public bool CanAllocate(long currentAllocatedBytes, long additionalBytes)
        {
            if (this.VramBudgetBytes <= 0)
            {
                return true;
            }

            return currentAllocatedBytes + additionalBytes <= this.VramBudgetBytes;
        }

        /// <summary>
        /// Decides whether a freshly loaded table should remain resident in device memory.
        /// </summary>
        /// <returns><c>true</c> to keep the table hot; otherwise <c>false</c>.</returns>
        public bool ShouldKeepResident() => this.KeepTablesHot;
    }
}

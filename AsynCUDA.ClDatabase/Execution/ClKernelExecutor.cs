using System;
using System.Collections.Generic;
using AsynCUDA.OpenCl;

namespace AsynCUDA.ClDatabase
{
    /// <summary>
    /// A database-specific launcher wrapper that runs OpenCL kernels operating on an arbitrary number of
    /// device buffers and scalar arguments. It delegates to the runtime's <see cref="OpenClLauncher"/>,
    /// which binds arguments positionally: <see cref="OpenClMem"/> buffers are bound as kernel buffers and
    /// scalars are bound by value. Scalars are normalized to the launcher-supported set
    /// (<see cref="int"/>, <see cref="float"/>, <see cref="long"/>, <see cref="uint"/>) so callers can pass
    /// loosely typed values.
    /// </summary>
    /// <remarks>
    /// Kernels launch on the registry's single command queue and the launcher calls <c>Finish</c> after
    /// each launch, so successive pipeline kernels and subsequent synchronous pulls observe completed
    /// results in order.
    /// </remarks>
    public sealed class ClKernelExecutor
    {
        private readonly OpenClLauncher launcher;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClKernelExecutor"/> class.
        /// </summary>
        /// <param name="launcher">The runtime launcher providing kernel execution.</param>
        public ClKernelExecutor(OpenClLauncher launcher)
        {
            this.launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        }

        /// <summary>
        /// Loads and launches a kernel over a linear (1D) work domain of <paramref name="length"/> work-items.
        /// </summary>
        /// <param name="kernelName">The kernel to load and run.</param>
        /// <param name="buffers">The device buffers, in the order they appear in the kernel signature.</param>
        /// <param name="scalars">The scalar arguments, in the order they appear in the kernel signature.</param>
        /// <param name="length">The number of elements/work-items (used to size the launch domain).</param>
        /// <returns><c>true</c> on success; otherwise <c>false</c>.</returns>
        public bool Execute(string kernelName, IReadOnlyList<OpenClMem> buffers, IReadOnlyList<object> scalars, int length)
        {
            if (length <= 0)
            {
                return false;
            }

            object[] arguments = new object[buffers.Count + scalars.Count];
            int slot = 0;
            for (int i = 0; i < buffers.Count; i++)
            {
                arguments[slot++] = buffers[i];
            }

            for (int i = 0; i < scalars.Count; i++)
            {
                arguments[slot++] = NormalizeScalar(scalars[i]);
            }

            return this.launcher.Execute(kernelName, length, 0, arguments);
        }

        private static object NormalizeScalar(object value) => value switch
        {
            int or float or long or uint => value,
            double d => (float) d,
            byte b => (int) b,
            short s => (int) s,
            bool flag => flag ? 1 : 0,
            _ => Convert.ToSingle(value)
        };
    }
}

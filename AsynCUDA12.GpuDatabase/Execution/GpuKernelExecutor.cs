using System;
using System.Collections.Generic;
using AsynCUDA12.Runtime;
using ManagedCuda;
using ManagedCuda.BasicTypes;
using ManagedCuda.VectorTypes;

namespace AsynCUDA12.GpuDatabase
{
    /// <summary>
    /// A database-specific launcher wrapper that runs CUDA kernels operating on an arbitrary number of
    /// device pointers (which the runtime's single-pointer <c>ExecuteLinearKernel</c> cannot express).
    /// It loads kernels via <see cref="CudaCompiler.LoadKernel(string, bool)"/>, reads the ordered
    /// argument signature via <see cref="CudaCompiler.GetArguments(string?, bool)"/>, builds the ordered
    /// argument array (pointers then scalars, by position) and launches with ManagedCuda directly.
    /// </summary>
    /// <remarks>
    /// Correctness relies on the runtime's primary context being current on the calling thread (the
    /// same thread that initialized <see cref="CudaService"/>). Kernels launch on the default stream, so
    /// successive pipeline kernels and subsequent synchronous pulls observe completed results in order.
    /// </remarks>
    public sealed class GpuKernelExecutor
    {
        private const int BlockSize = 256;

        private readonly CudaService cuda;

        /// <summary>
        /// Initializes a new instance of the <see cref="GpuKernelExecutor"/> class.
        /// </summary>
        /// <param name="cuda">The runtime service providing the compiler and active context.</param>
        public GpuKernelExecutor(CudaService cuda)
        {
            this.cuda = cuda ?? throw new ArgumentNullException(nameof(cuda));
        }

        /// <summary>
        /// Loads and launches a kernel over a linear (1D) work domain of <paramref name="length"/> threads.
        /// </summary>
        /// <param name="kernelName">The kernel to load and run.</param>
        /// <param name="pointers">The device pointers, in the order they appear in the kernel signature.</param>
        /// <param name="scalars">The scalar arguments, in the order they appear in the kernel signature.</param>
        /// <param name="length">The number of elements/threads (used to size the launch grid).</param>
        /// <returns><c>true</c> on success; otherwise <c>false</c>.</returns>
        public bool Execute(string kernelName, IReadOnlyList<CUdeviceptr> pointers, IReadOnlyList<object> scalars, int length)
        {
            CudaCompiler? compiler = this.cuda.Compiler;
            if (compiler == null)
            {
                CudaLogger.Log("GpuKernelExecutor: No compiler available (service offline).");
                return false;
            }

            CudaKernel? kernel = compiler.LoadKernel(kernelName, silent: true);
            if (kernel == null)
            {
                CudaLogger.Log($"GpuKernelExecutor: Failed to load kernel '{kernelName}'.");
                return false;
            }

            if (!TryBuildArguments(compiler, pointers, scalars, out object[] kernelArgs))
            {
                return false;
            }

            try
            {
                int gridSize = Math.Max(1, (length + BlockSize - 1) / BlockSize);
                kernel.BlockDimensions = new dim3(BlockSize, 1, 1);
                kernel.GridDimensions = new dim3(gridSize, 1, 1);
                kernel.Run(kernelArgs);
                return true;
            }
            catch (Exception ex)
            {
                CudaLogger.Log($"GpuKernelExecutor: Failed to run kernel '{kernelName}'", ex);
                return false;
            }
        }

        private static bool TryBuildArguments(CudaCompiler compiler, IReadOnlyList<CUdeviceptr> pointers, IReadOnlyList<object> scalars, out object[] kernelArgs)
        {
            Dictionary<string, Type> definitions = compiler.GetArguments(null, silent: true);
            kernelArgs = new object[definitions.Count];

            int pointerIndex = 0;
            int scalarIndex = 0;
            int slot = 0;
            foreach (KeyValuePair<string, Type> definition in definitions)
            {
                if (definition.Value == typeof(IntPtr))
                {
                    if (pointerIndex >= pointers.Count)
                    {
                        CudaLogger.Log($"GpuKernelExecutor: Too few pointers (kernel expects more than {pointers.Count}).");
                        return false;
                    }

                    kernelArgs[slot] = pointers[pointerIndex++];
                }
                else
                {
                    if (scalarIndex >= scalars.Count)
                    {
                        CudaLogger.Log($"GpuKernelExecutor: Too few scalars (kernel expects more than {scalars.Count}).");
                        return false;
                    }

                    kernelArgs[slot] = ConvertScalar(scalars[scalarIndex++], definition.Value);
                }

                slot++;
            }

            if (pointerIndex != pointers.Count || scalarIndex != scalars.Count)
            {
                CudaLogger.Log($"GpuKernelExecutor: Argument count mismatch (pointers {pointerIndex}/{pointers.Count}, scalars {scalarIndex}/{scalars.Count}).");
                return false;
            }

            return true;
        }

        private static object ConvertScalar(object value, Type expected)
        {
            if (expected == typeof(int)) { return Convert.ToInt32(value); }
            if (expected == typeof(float)) { return Convert.ToSingle(value); }
            if (expected == typeof(double)) { return Convert.ToDouble(value); }
            if (expected == typeof(byte)) { return Convert.ToByte(value); }
            if (expected == typeof(char)) { return Convert.ToChar(value); }
            if (expected == typeof(bool)) { return Convert.ToBoolean(value); }
            return value;
        }
    }
}

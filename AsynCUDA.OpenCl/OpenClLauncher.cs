using System;
using System.Runtime.InteropServices;
using OpenTK.Compute.OpenCL;

namespace AsynCUDA.OpenCl
{
    /// <summary>
    /// Executes compiled OpenCL kernels. Provides a flexible argument-mapping launch that accepts a mix of
    /// <see cref="OpenClMem"/> buffers and unmanaged scalar values, configures the work size and synchronizes
    /// the command queue. Kept deliberately small; the Fourier helper builds higher-level pipelines on top of it.
    /// </summary>
    public sealed class OpenClLauncher
    {
        private readonly OpenClCompiler _compiler;
        private readonly CLCommandQueue _queue;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenClLauncher"/> class.
        /// </summary>
        /// <param name="compiler">The compiler that holds the kernels to launch.</param>
        /// <param name="queue">The command queue to enqueue work on.</param>
        internal OpenClLauncher(OpenClCompiler compiler, CLCommandQueue queue)
        {
            this._compiler = compiler;
            this._queue = queue;
        }



        // Launch
        /// <summary>
        /// Executes a one-dimensional kernel by name with the supplied arguments.
        /// Arguments may be <see cref="OpenClMem"/> (bound as buffers), <see cref="CLBuffer"/>, or unmanaged
        /// scalars (<see cref="int"/>, <see cref="float"/>, ...). Blocks until the kernel finishes.
        /// </summary>
        /// <param name="kernelName">The kernel entry-point name.</param>
        /// <param name="globalWorkSize">The total number of work-items.</param>
        /// <param name="arguments">The ordered kernel arguments.</param>
        /// <returns><c>true</c> if the kernel executed successfully; otherwise <c>false</c>.</returns>
        public bool Execute(string kernelName, long globalWorkSize, params object[] arguments)
        {
            return this.Execute(kernelName, globalWorkSize, 0, arguments);
        }

        /// <summary>
        /// Executes a one-dimensional kernel by name with an optional local work size.
        /// </summary>
        /// <param name="kernelName">The kernel entry-point name.</param>
        /// <param name="globalWorkSize">The total number of work-items.</param>
        /// <param name="localWorkSize">The work-group size, or 0 to let the runtime choose.</param>
        /// <param name="arguments">The ordered kernel arguments.</param>
        /// <returns><c>true</c> if the kernel executed successfully; otherwise <c>false</c>.</returns>
        public bool Execute(string kernelName, long globalWorkSize, long localWorkSize, params object[] arguments)
        {
            CLKernel? kernel = this._compiler.GetKernel(kernelName);
            if (kernel == null)
            {
                return false;
            }

            if (globalWorkSize <= 0)
            {
                OpenClLogger.LogError($"Execute '{kernelName}': invalid global work size {globalWorkSize}.");
                return false;
            }

            if (!this.SetArguments(kernel.Value, kernelName, arguments))
            {
                return false;
            }

            UIntPtr[] global = [new UIntPtr((ulong)globalWorkSize)];
            UIntPtr[]? local = localWorkSize > 0 ? [new UIntPtr((ulong)localWorkSize)] : null;

            CLResultCode code = CL.EnqueueNDRangeKernel(this._queue, kernel.Value, 1, null, global, local, 0, null, out _);
            if (code != CLResultCode.Success)
            {
                OpenClLogger.LogError($"Execute '{kernelName}': EnqueueNDRangeKernel failed ({code}).");
                return false;
            }

            CLResultCode finish = CL.Finish(this._queue);
            if (finish != CLResultCode.Success)
            {
                OpenClLogger.LogError($"Execute '{kernelName}': Finish failed ({finish}).");
                return false;
            }

            return true;
        }



        // Arguments
        /// <summary>
        /// Binds each argument to its kernel index, handling buffers and unmanaged scalars.
        /// </summary>
        private bool SetArguments(CLKernel kernel, string kernelName, object[] arguments)
        {
            for (uint i = 0; i < arguments.Length; i++)
            {
                object arg = arguments[i];
                CLResultCode code;

                switch (arg)
                {
                    case OpenClMem mem:
                        {
                            CLBuffer buffer = mem.IndexBuffer;
                            code = CL.SetKernelArg(kernel, i, in buffer);
                            break;
                        }

                    case CLBuffer buffer:
                        code = CL.SetKernelArg(kernel, i, in buffer);
                        break;

                    case int value:
                        code = CL.SetKernelArg(kernel, i, in value);
                        break;

                    case uint value:
                        code = CL.SetKernelArg(kernel, i, in value);
                        break;

                    case float value:
                        code = CL.SetKernelArg(kernel, i, in value);
                        break;

                    case long value:
                        code = CL.SetKernelArg(kernel, i, in value);
                        break;

                    default:
                        OpenClLogger.LogError($"Execute '{kernelName}': unsupported argument type '{arg?.GetType().Name ?? "null"}' at index {i}.");
                        return false;
                }

                if (code != CLResultCode.Success)
                {
                    OpenClLogger.LogError($"Execute '{kernelName}': SetKernelArg failed at index {i} ({code}).");
                    return false;
                }
            }

            return true;
        }
    }
}

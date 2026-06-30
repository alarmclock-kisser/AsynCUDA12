using System;
using System.Collections.Generic;
using AsynCUDA12.Runtime;

namespace AsynCUDA12.GpuDatabase
{
    /// <summary>
    /// Knows the kernel names required by the GPU database and ensures the underlying runtime
    /// <see cref="CudaCompiler"/> has them compiled and loadable. Actual compilation happens inside
    /// the runtime (the compiler compiles all discovered <c>.cu</c> sources when a device is
    /// initialized); this catalog provides explicit (re)compile/load checks on top of that.
    /// </summary>
    public sealed class KernelCatalog
    {
        private readonly CudaService cuda;

        /// <summary>
        /// Initializes a new instance of the <see cref="KernelCatalog"/> class.
        /// </summary>
        /// <param name="cuda">The runtime service used for compilation and loading.</param>
        public KernelCatalog(CudaService cuda)
        {
            this.cuda = cuda ?? throw new ArgumentNullException(nameof(cuda));
        }

        /// <summary>Gets the kernel names the catalog manages.</summary>
        public IReadOnlyList<string> KnownKernels => KernelNames.All;

        /// <summary>
        /// Requests the runtime compiler to (re)compile all discovered kernel sources.
        /// </summary>
        /// <param name="silent">When <c>true</c>, suppresses per-kernel logging.</param>
        /// <returns><c>true</c> if a compiler was available; otherwise <c>false</c>.</returns>
        public bool EnsureCompiled(bool silent = true)
        {
            CudaCompiler? compiler = this.cuda.Compiler;
            if (compiler == null)
            {
                return false;
            }

            compiler.CompileAll(silent, logErrors: true);
            return true;
        }

        /// <summary>
        /// Ensures the given kernel can be loaded into the active CUDA context.
        /// </summary>
        /// <param name="kernelName">The kernel name to load.</param>
        /// <returns><c>true</c> if the kernel loaded successfully; otherwise <c>false</c>.</returns>
        public bool EnsureLoaded(string kernelName)
        {
            CudaCompiler? compiler = this.cuda.Compiler;
            if (compiler == null)
            {
                return false;
            }

            return compiler.LoadKernel(kernelName, silent: true) != null;
        }
    }
}

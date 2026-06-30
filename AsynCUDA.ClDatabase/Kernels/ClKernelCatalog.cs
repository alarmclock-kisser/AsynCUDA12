using System;
using System.Collections.Generic;
using AsynCUDA.OpenCl;

namespace AsynCUDA.ClDatabase
{
    /// <summary>
    /// Knows the kernel names required by the OpenCL database and verifies that the underlying runtime
    /// <see cref="OpenClCompiler"/> has them compiled and available. Actual compilation happens inside
    /// the runtime (the compiler compiles all discovered <c>.cl</c> sources when a device is
    /// initialized); this catalog provides explicit availability checks on top of that.
    /// </summary>
    public sealed class ClKernelCatalog
    {
        private readonly OpenClService service;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClKernelCatalog"/> class.
        /// </summary>
        /// <param name="service">The runtime service exposing the compiled kernels.</param>
        public ClKernelCatalog(OpenClService service)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>Gets the kernel names the catalog manages.</summary>
        public IReadOnlyList<string> KnownKernels => ClKernelNames.All;

        /// <summary>
        /// Verifies that the runtime compiler is available. The OpenCL runtime compiles all discovered
        /// kernel sources when the device is initialized, so this only confirms a compiler exists.
        /// </summary>
        /// <returns><c>true</c> if a compiler was available; otherwise <c>false</c>.</returns>
        public bool EnsureCompiled()
        {
            return this.service.Compiler != null;
        }

        /// <summary>
        /// Ensures the given kernel has been compiled and is available for launch.
        /// </summary>
        /// <param name="kernelName">The kernel name to check.</param>
        /// <returns><c>true</c> if the kernel is available; otherwise <c>false</c>.</returns>
        public bool EnsureLoaded(string kernelName)
        {
            OpenClCompiler? compiler = this.service.Compiler;
            if (compiler == null)
            {
                return false;
            }

            return compiler.HasKernel(kernelName);
        }

        /// <summary>
        /// Verifies that every known database kernel is available in the runtime compiler.
        /// </summary>
        /// <returns>The names of any missing kernels; empty when all are present.</returns>
        public IReadOnlyList<string> FindMissingKernels()
        {
            OpenClCompiler? compiler = this.service.Compiler;
            List<string> missing = new();
            if (compiler == null)
            {
                missing.AddRange(ClKernelNames.All);
                return missing;
            }

            foreach (string kernel in ClKernelNames.All)
            {
                if (!compiler.HasKernel(kernel))
                {
                    missing.Add(kernel);
                }
            }

            return missing;
        }
    }
}

using AsynCUDA.OpenCl;
using AsynCUDA12.Tests.GpuDatabase;
using Shouldly;

namespace AsynCUDA12.Tests.OpenCl
{
    /// <summary>
    /// Integration tests for <see cref="OpenClCompiler"/>: verifies that every shipped Fourier kernel is
    /// discovered, compiled and resolvable, and that unknown kernels are reported as missing. Guarded by
    /// <see cref="TestData.RequireOpenCl"/> so the suite is skipped as inconclusive without an OpenCL runtime.
    /// </summary>
    [TestClass]
    public sealed class OpenClCompilerIntegrationTest
    {
        private static readonly string[] ExpectedKernels =
        {
            "chunked_fft",
            "chunked_ifft",
            "fft_full",
            "ifft_full",
            "pad_real_to_complex",
            "normalize_complex",
            "extract_real",
        };

        private OpenClService Service = null!;
        private OpenClCompiler Compiler = null!;


        [TestInitialize]
        public void Initialize()
        {
            this.Service = TestData.CreateOnlineOpenClServiceOrSkip()!;
            this.Compiler = this.Service.Compiler!;
            this.Compiler.ShouldNotBeNull();
        }

        [TestCleanup]
        public void Cleanup()
        {
            this.Service?.Dispose();
        }



        [TestMethod]
        public void Compiler_CompilesAtLeastOneKernel()
        {
            this.Compiler.KernelNames.Count.ShouldBeGreaterThan(0);
        }

        [TestMethod]
        public void Compiler_HasEveryExpectedFourierKernel()
        {
            foreach (string kernelName in ExpectedKernels)
            {
                this.Compiler.HasKernel(kernelName).ShouldBeTrue($"kernel '{kernelName}' should be compiled.");
            }
        }

        [TestMethod]
        public void Compiler_GetKernel_ReturnsEachExpectedKernel()
        {
            foreach (string kernelName in ExpectedKernels)
            {
                this.Compiler.GetKernel(kernelName).ShouldNotBeNull($"kernel '{kernelName}' should resolve.");
            }
        }

        [TestMethod]
        public void Compiler_HasKernel_UnknownKernel_ReturnsFalse()
        {
            this.Compiler.HasKernel("this_kernel_does_not_exist").ShouldBeFalse();
        }

        [TestMethod]
        public void Compiler_GetKernel_UnknownKernel_ReturnsNull()
        {
            this.Compiler.GetKernel("this_kernel_does_not_exist").ShouldBeNull();
        }

        [TestMethod]
        public void Compiler_KernelDirectory_ContainsClFiles()
        {
            this.Compiler.KernelDirectory.ShouldNotBeNullOrEmpty();
            this.Compiler.GetClFiles().Length.ShouldBeGreaterThan(0);
        }
    }
}

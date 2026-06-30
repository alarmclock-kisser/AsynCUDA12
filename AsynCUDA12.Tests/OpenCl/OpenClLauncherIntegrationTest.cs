using System.Numerics;
using AsynCUDA.OpenCl;
using AsynCUDA12.Tests.GpuDatabase;
using Shouldly;

namespace AsynCUDA12.Tests.OpenCl
{
    /// <summary>
    /// Integration tests for <see cref="OpenClLauncher"/>: launches simple element-wise kernels with a mix
    /// of buffers and scalars and verifies the device results, plus the launcher's error handling for
    /// unknown kernels, invalid work sizes and unsupported argument types. Guarded by
    /// <see cref="TestData.RequireOpenCl"/> so the suite is skipped as inconclusive without an OpenCL runtime.
    /// </summary>
    [TestClass]
    public sealed class OpenClLauncherIntegrationTest
    {
        private OpenClService Service = null!;
        private OpenClLauncher Launcher = null!;
        private OpenClRegister Register = null!;


        [TestInitialize]
        public void Initialize()
        {
            this.Service = TestData.CreateOnlineOpenClServiceOrSkip()!;
            this.Launcher = this.Service.Launcher!;
            this.Register = this.Service.Register!;
            this.Launcher.ShouldNotBeNull();
            this.Register.ShouldNotBeNull();
        }

        [TestCleanup]
        public void Cleanup()
        {
            this.Service?.Dispose();
        }



        // Successful launches
        [TestMethod]
        public void Execute_NormalizeComplex_ScalesEveryElement()
        {
            Vector2[] data = new Vector2[64];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = new Vector2(i, 2 * i);
            }

            OpenClMem? mem = this.Register.PushData(data);
            mem.ShouldNotBeNull();

            const float scale = 0.5f;
            bool ok = this.Launcher.Execute("normalize_complex", data.Length, mem!, scale, data.Length);
            ok.ShouldBeTrue();

            Vector2[]? result = this.Register.PullData<Vector2>(mem!);
            result.ShouldNotBeNull();
            for (int i = 0; i < data.Length; i++)
            {
                result![i].X.ShouldBe(data[i].X * scale, 1e-5f);
                result[i].Y.ShouldBe(data[i].Y * scale, 1e-5f);
            }
        }

        [TestMethod]
        public void Execute_ExtractReal_CopiesRealPartToOutputBuffer()
        {
            Vector2[] data = new Vector2[32];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = new Vector2(i + 1, -i);
            }

            OpenClMem? input = this.Register.PushData(data);
            OpenClMem? output = this.Register.AllocateSingle<float>(data.Length);
            input.ShouldNotBeNull();
            output.ShouldNotBeNull();

            bool ok = this.Launcher.Execute("extract_real", data.Length, input!, output!, data.Length);
            ok.ShouldBeTrue();

            float[]? result = this.Register.PullData<float>(output!);
            result.ShouldNotBeNull();
            for (int i = 0; i < data.Length; i++)
            {
                result![i].ShouldBe(data[i].X, 1e-5f);
            }
        }

        [TestMethod]
        public void Execute_WithLocalWorkSize_Succeeds()
        {
            float[] zeros = new float[16];
            Vector2[] data = new Vector2[16];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = new Vector2(i, i);
            }

            OpenClMem? input = this.Register.PushData(data);
            OpenClMem? output = this.Register.PushData(zeros);
            input.ShouldNotBeNull();
            output.ShouldNotBeNull();

            bool ok = this.Launcher.Execute("extract_real", data.Length, 4, input!, output!, data.Length);
            ok.ShouldBeTrue();
        }



        // Error handling
        [TestMethod]
        public void Execute_UnknownKernel_ReturnsFalse()
        {
            OpenClMem? mem = this.Register.AllocateSingle<float>(8);
            mem.ShouldNotBeNull();

            this.Launcher.Execute("no_such_kernel", 8, mem!, 8).ShouldBeFalse();
        }

        [TestMethod]
        public void Execute_InvalidGlobalWorkSize_ReturnsFalse()
        {
            OpenClMem? mem = this.Register.AllocateSingle<Vector2>(8);
            mem.ShouldNotBeNull();

            this.Launcher.Execute("normalize_complex", 0, mem!, 1.0f, 8).ShouldBeFalse();
        }

        [TestMethod]
        public void Execute_UnsupportedArgumentType_ReturnsFalse()
        {
            OpenClMem? mem = this.Register.AllocateSingle<Vector2>(8);
            mem.ShouldNotBeNull();

            // double is not a supported scalar argument type in the launcher.
            this.Launcher.Execute("normalize_complex", 8, mem!, 1.0d, 8).ShouldBeFalse();
        }
    }
}

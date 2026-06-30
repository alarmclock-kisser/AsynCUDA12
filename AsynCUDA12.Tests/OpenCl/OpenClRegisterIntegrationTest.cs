using System.Numerics;
using AsynCUDA.OpenCl;
using AsynCUDA12.Tests.GpuDatabase;
using Shouldly;

namespace AsynCUDA12.Tests.OpenCl
{
    /// <summary>
    /// Integration tests for <see cref="OpenClRegister"/> covering allocation, host/device transfers,
    /// bookkeeping and disposal. Guarded by <see cref="TestData.RequireOpenCl"/> so the suite is skipped
    /// as inconclusive on machines without a usable OpenCL runtime.
    /// </summary>
    [TestClass]
    public sealed class OpenClRegisterIntegrationTest
    {
        private OpenClService Service = null!;
        private OpenClRegister Register = null!;


        [TestInitialize]
        public void Initialize()
        {
            this.Service = TestData.CreateOnlineOpenClServiceOrSkip()!;
            this.Register = this.Service.Register!;
            this.Register.ShouldNotBeNull();
        }

        [TestCleanup]
        public void Cleanup()
        {
            this.Service?.Dispose();
        }



        // Allocation
        [TestMethod]
        public void AllocateSingle_ValidLength_TracksAllocation()
        {
            OpenClMem? mem = this.Register.AllocateSingle<float>(1024);

            mem.ShouldNotBeNull();
            mem!.Count.ShouldBe(1);
            mem.IndexLength.ShouldBe(1024);
            mem.ElementType.ShouldBe(typeof(float));
            this.Register.Contains(mem).ShouldBeTrue();
            this.Register.AllocationCount.ShouldBe(1);
            this.Register.TotalAllocated.ShouldBe(1024L * sizeof(float));
        }

        [TestMethod]
        public void AllocateSingle_ZeroLength_ReturnsNull()
        {
            this.Register.AllocateSingle<float>(0).ShouldBeNull();
            this.Register.AllocationCount.ShouldBe(0);
        }

        [TestMethod]
        public void AllocateSingle_NegativeLength_ReturnsNull()
        {
            this.Register.AllocateSingle<int>(-5).ShouldBeNull();
            this.Register.AllocationCount.ShouldBe(0);
        }



        // Push / Pull roundtrips
        [TestMethod]
        public void PushPull_FloatArray_RoundTripsIdentically()
        {
            float[] data = new float[256];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = i * 0.5f;
            }

            OpenClMem? mem = this.Register.PushData(data);
            mem.ShouldNotBeNull();

            float[]? result = this.Register.PullData<float>(mem!);
            result.ShouldNotBeNull();
            result!.Length.ShouldBe(data.Length);
            result.ShouldBe(data);
        }

        [TestMethod]
        public void PushPull_IntArray_RoundTripsIdentically()
        {
            int[] data = { 1, 2, 3, 4, 5, -6, 7, int.MaxValue, int.MinValue };

            OpenClMem? mem = this.Register.PushData(data);
            mem.ShouldNotBeNull();

            int[]? result = this.Register.PullData<int>(mem!);
            result.ShouldNotBeNull();
            result!.ShouldBe(data);
        }

        [TestMethod]
        public void PushPull_Vector2Array_RoundTripsIdentically()
        {
            Vector2[] data = new Vector2[128];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = new Vector2(i, -i);
            }

            OpenClMem? mem = this.Register.PushData(data);
            mem.ShouldNotBeNull();

            Vector2[]? result = this.Register.PullData<Vector2>(mem!);
            result.ShouldNotBeNull();
            result!.Length.ShouldBe(data.Length);
            for (int i = 0; i < data.Length; i++)
            {
                result[i].X.ShouldBe(data[i].X);
                result[i].Y.ShouldBe(data[i].Y);
            }
        }

        [TestMethod]
        public async Task PushPullAsync_FloatArray_RoundTripsIdentically()
        {
            float[] data = { 1.5f, 2.5f, 3.5f, 4.5f };

            OpenClMem? mem = await this.Register.PushDataAsync(data);
            mem.ShouldNotBeNull();

            float[]? result = await this.Register.PullDataAsync<float>(mem!);
            result.ShouldNotBeNull();
            result!.ShouldBe(data);
        }

        [TestMethod]
        public void PushData_EmptyArray_ReturnsNull()
        {
            this.Register.PushData(System.Array.Empty<float>()).ShouldBeNull();
            this.Register.AllocationCount.ShouldBe(0);
        }



        // Free / FreeAll
        [TestMethod]
        public void Free_TrackedAllocation_RemovesAndReleases()
        {
            OpenClMem? mem = this.Register.AllocateSingle<float>(64);
            mem.ShouldNotBeNull();

            this.Register.Free(mem!).ShouldBeTrue();
            this.Register.Contains(mem!).ShouldBeFalse();
            this.Register.AllocationCount.ShouldBe(0);
            mem!.IsDisposed.ShouldBeTrue();
        }

        [TestMethod]
        public void FreeAll_ReleasesEveryAllocation()
        {
            this.Register.AllocateSingle<float>(64).ShouldNotBeNull();
            this.Register.AllocateSingle<int>(128).ShouldNotBeNull();
            this.Register.AllocateSingle<double>(32).ShouldNotBeNull();
            this.Register.AllocationCount.ShouldBe(3);

            this.Register.FreeAll();

            this.Register.AllocationCount.ShouldBe(0);
            this.Register.TotalAllocated.ShouldBe(0);
        }

        [TestMethod]
        public void TotalAllocated_ReflectsMultipleAllocations()
        {
            this.Register.AllocateSingle<float>(100).ShouldNotBeNull();
            this.Register.AllocateSingle<int>(50).ShouldNotBeNull();

            long expected = (100L * sizeof(float)) + (50L * sizeof(int));
            this.Register.TotalAllocated.ShouldBe(expected);
        }
    }
}

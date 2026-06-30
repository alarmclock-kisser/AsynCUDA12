using System.Linq;
using AsynCUDA.OpenCl;
using AsynCUDA12.Tests.GpuDatabase;
using Shouldly;

namespace AsynCUDA12.Tests.OpenCl
{
    /// <summary>
    /// Integration tests for the <see cref="OpenClService"/> lifecycle, mirroring the CUDA service tests.
    /// Every test is guarded by <see cref="TestData.RequireOpenCl"/> so the suite is skipped as
    /// inconclusive on machines without any usable OpenCL runtime (OpenCL is commonly available through a
    /// CPU device even without a discrete GPU).
    /// </summary>
    [TestClass]
    public sealed class OpenClServiceIntegrationTest
    {
        private OpenClService Service = null!;


        [TestInitialize]
        public void Initialize()
        {
            // Skip (inconclusive) on machines without a usable OpenCL ICD/device. The OpenClService
            // constructor enumerates devices eagerly, so it must only be created after this guard.
            TestData.RequireOpenCl();

            this.Service = new OpenClService(-1);
            this.Service.SelectedDeviceId.ShouldBe(-1);
        }

        [TestCleanup]
        public void Cleanup()
        {
            this.Service?.Dispose();
        }



        // Discovery
        [TestMethod]
        public void Discovery_FindsAtLeastOneDevice()
        {
            this.Service.AvailableDevices.Count.ShouldBeGreaterThan(0);
            this.Service.DeviceCount.ShouldBe(this.Service.AvailableDevices.Count);
        }

        [TestMethod]
        public void Discovery_DevicesHaveFlatContiguousIndices()
        {
            for (int i = 0; i < this.Service.AvailableDevices.Count; i++)
            {
                this.Service.AvailableDevices[i].Index.ShouldBe(i);
            }
        }



        // Offline (empty)
        [TestMethod]
        public void WhenEmpty_InitializeFirst_ServiceOnlineAndSetDeviceId()
        {
            this.Service.Initialize(0).ShouldBeTrue();
            this.Service.Online.ShouldBeTrue();
            this.Service.SelectedDeviceId.ShouldBe(0);
            this.Service.SelectedDevice.ShouldNotBeNull();
        }

        [TestMethod]
        public void WhenEmpty_InitializeNegativeDeviceId_StaysOffline()
        {
            this.Service.Initialize(-999).ShouldBeFalse();
            this.Service.Online.ShouldBeFalse();
            this.Service.SelectedDeviceId.ShouldBe(-1);
        }

        [TestMethod]
        public void WhenEmpty_InitializeTooHighDeviceId_StaysOffline()
        {
            this.Service.Initialize(this.Service.DeviceCount + 999).ShouldBeFalse();
            this.Service.Online.ShouldBeFalse();
            this.Service.SelectedDeviceId.ShouldBe(-1);
        }

        [TestMethod]
        public void WhenEmpty_Dispose_NoChange()
        {
            this.Service.Online.ShouldBeFalse();
            this.Service.SelectedDeviceId.ShouldBe(-1);
            this.Service.Dispose();
            this.Service.Online.ShouldBeFalse();
            this.Service.SelectedDeviceId.ShouldBe(-1);
        }

        [TestMethod]
        public void WhenEmpty_InitializeValidName_ServiceOnlineAndSetDeviceId()
        {
            string deviceName = this.Service.AvailableDevices[0].DeviceName;
            deviceName.ShouldNotBeNullOrEmpty();

            this.Service.Initialize(deviceName).ShouldBeTrue();
            this.Service.Online.ShouldBeTrue();
            this.Service.SelectedDeviceId.ShouldBe(0);
        }

        [TestMethod]
        public void WhenEmpty_InitializeUnknownName_StaysOffline()
        {
            this.Service.Initialize("ThisDeviceDoesNotExist").ShouldBeFalse();
            this.Service.Online.ShouldBeFalse();
            this.Service.SelectedDeviceId.ShouldBe(-1);
        }

        [TestMethod]
        public void WhenEmpty_InitializeEmptyName_StaysOffline()
        {
            this.Service.Initialize(string.Empty).ShouldBeFalse();
            this.Service.Online.ShouldBeFalse();
            this.Service.SelectedDeviceId.ShouldBe(-1);
        }

        [TestMethod]
        public void WhenOffline_DisposeTwice_NoChange()
        {
            this.Service.Online.ShouldBeFalse();
            this.Service.Dispose();
            this.Service.Dispose();
            this.Service.Online.ShouldBeFalse();
            this.Service.SelectedDeviceId.ShouldBe(-1);
        }



        // Online
        [TestMethod]
        public void WhenOnline_ExposesRegisterAndFourier()
        {
            this.Service.Initialize(0).ShouldBeTrue();
            this.Service.Register.ShouldNotBeNull();
            this.Service.Fourier.ShouldNotBeNull();
        }

        [TestMethod]
        public void WhenOnline_Shutdown_ServiceOfflineAndDeviceIdReset()
        {
            this.Service.Initialize(0).ShouldBeTrue();
            this.Service.Online.ShouldBeTrue();
            this.Service.Shutdown();
            this.Service.Online.ShouldBeFalse();
            this.Service.SelectedDeviceId.ShouldBe(-1);
            this.Service.Register.ShouldBeNull();
            this.Service.Fourier.ShouldBeNull();
        }

        [TestMethod]
        public void WhenOnline_Dispose_ServiceOfflineAndDeviceIdReset()
        {
            this.Service.Initialize(0).ShouldBeTrue();
            this.Service.Online.ShouldBeTrue();
            this.Service.SelectedDeviceId.ShouldBe(0);
            this.Service.Dispose();
            this.Service.Online.ShouldBeFalse();
            this.Service.SelectedDeviceId.ShouldBe(-1);
        }

        [TestMethod]
        public void WhenOnline_DisposeTwice_ServiceRemainsOffline()
        {
            this.Service.Initialize(0).ShouldBeTrue();
            this.Service.Dispose();
            this.Service.Dispose();
            this.Service.Online.ShouldBeFalse();
            this.Service.SelectedDeviceId.ShouldBe(-1);
        }

        [TestMethod]
        public void WhenOnline_ReinitializeSameId_ServiceOnlineAndDeviceIdUnchanged()
        {
            this.Service.Initialize(0).ShouldBeTrue();
            this.Service.Online.ShouldBeTrue();
            this.Service.SelectedDeviceId.ShouldBe(0);
            this.Service.Initialize(0).ShouldBeTrue();
            this.Service.Online.ShouldBeTrue();
            this.Service.SelectedDeviceId.ShouldBe(0);
        }

        [TestMethod]
        public void WhenOnline_ReinitializeInvalidName_KeepsOnlineAndDeviceIdUnchanged()
        {
            this.Service.Initialize(0).ShouldBeTrue();
            this.Service.Online.ShouldBeTrue();
            this.Service.SelectedDeviceId.ShouldBe(0);
            this.Service.Initialize("ThisDeviceDoesNotExist").ShouldBeFalse();
            this.Service.Online.ShouldBeTrue();
            this.Service.SelectedDeviceId.ShouldBe(0);
        }

        [TestMethod]
        public void WhenOfflineAfterShutdown_InitializeValidName_ServiceOnlineAndSetDeviceId()
        {
            string deviceName = this.Service.AvailableDevices[0].DeviceName;
            deviceName.ShouldNotBeNullOrEmpty();

            this.Service.Initialize(0).ShouldBeTrue();
            this.Service.Shutdown();
            this.Service.Initialize(deviceName).ShouldBeTrue();
            this.Service.Online.ShouldBeTrue();
            this.Service.SelectedDeviceId.ShouldBe(0);
        }
    }
}

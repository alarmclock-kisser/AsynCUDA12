using AsynCUDA12.Runtime;
using Shouldly;

namespace AsynCUDA12.Tests
{
	[TestClass]
	public sealed class CudaServiceIntegrationTest
	{
		private CudaService Service = null!;


		[TestInitialize]
		public void Initialize()
		{
			// Skip (inconclusive) on machines without a usable CUDA driver/device. The CudaService
			// constructor enumerates devices eagerly, so it must only be created after this guard.
			GpuDatabase.TestData.RequireCuda();

			this.Service = new CudaService(-1);
			Assert.IsTrue(this.Service.SelectedDeviceId == -1);
		}

		[TestCleanup]
		public void Cleanup()
		{
			this.Service?.Dispose();

			Assert.IsTrue((this.Service?.SelectedDeviceId ?? -1) == -1);
		}



		// Offline (empty)
		[TestMethod]
		public void WhenEmpty_InitializeFirst_ServiceOnlineAndSetDeviceId()
		{
			this.Service.Initialize(0);
			this.Service.Online.ShouldBeTrue();
			this.Service.SelectedDeviceId.ShouldBe(0);
		}

		[TestMethod]
		public void WhenEmpty_InitializeNegativeDeviceId_Disposes()
		{
			this.Service.Initialize(-999);
			this.Service.Online.ShouldBeFalse();
			this.Service.SelectedDeviceId.ShouldBe(-1);
		}

		[TestMethod]
		public void WhenEmpty_InitializeTooHighDeviceId_Disposes()
		{
			this.Service.Initialize(999);
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
		public void WhenEmpty_InitializeValidName_ServiceOnlineAndSetDeviceId_Success()
		{
			var deviceName = this.Service[0]?.DeviceName;
			Assert.IsNotNull(deviceName);
			this.Service.Initialize(deviceName!);
			this.Service.Online.ShouldBeTrue();
			this.Service.SelectedDeviceId.ShouldBe(0);
		}

		[TestMethod]
		public void WhenEmpty_InitializeValidIdAsName_ParsesAsIdAndInitializes_Success()
		{
			this.Service.Initialize("0");
			this.Service.Online.ShouldBeTrue();
			this.Service.SelectedDeviceId.ShouldBe(0);
		}

		[TestMethod]
		public void WhenEmpty_InitializeInvalidIdAsName_ParsesAsIdAndInitializes_Fails()
		{
			this.Service.Initialize("-1");
			this.Service.Online.ShouldBeFalse();
			this.Service.SelectedDeviceId.ShouldBe(-1);
		}

		[TestMethod]
		public void WhenEmpty_InitializeValidName_ServiceOfflineAndSetDeviceId()
		{
			this.Service.Initialize("ThisDeviceDoesNotExist");
			this.Service.Online.ShouldBeFalse();
			this.Service.SelectedDeviceId.ShouldBe(-1);
		}

		[TestMethod]
		public void WhenEmpty_InitializeInvalidNameExactMatch_ServiceOfflineAndSetDeviceId()
		{
			this.Service.Initialize("ThisDeviceDoesNotExist", exactMatch: true);
			this.Service.Online.ShouldBeFalse();
			this.Service.SelectedDeviceId.ShouldBe(-1);
		}

		[TestMethod]
		public void WhenOffline_DisposeTwice_NoChange()
		{
			this.Service.Online.ShouldBeFalse();
			this.Service.SelectedDeviceId.ShouldBe(-1);
			this.Service.Dispose();
			this.Service.Dispose();
			this.Service.Online.ShouldBeFalse();
			this.Service.SelectedDeviceId.ShouldBe(-1);
		}



		// Online
		[TestMethod]
		public void WhenOnline_Dispose_ServiceOfflineAndDeviceIdReset()
		{
			this.Service.Initialize(0);
			this.Service.Online.ShouldBeTrue();
			this.Service.SelectedDeviceId.ShouldBe(0);
			this.Service.Dispose();
			this.Service.Online.ShouldBeFalse();
			this.Service.SelectedDeviceId.ShouldBe(-1);
		}

		[TestMethod]
		public void WhenOnline_DisposeTwice_ServiceRemainsOffline()
		{
			this.Service.Initialize(0);
			this.Service.Dispose();
			this.Service.Dispose();
			this.Service.Online.ShouldBeFalse();
			this.Service.SelectedDeviceId.ShouldBe(-1);
		}

		[TestMethod]
		public void WhenOnline_ReinitializeSameId_ServiceOnlineAndDeviceIdUnchanged()
		{
			this.Service.Initialize(0);
			this.Service.Online.ShouldBeTrue();
			this.Service.SelectedDeviceId.ShouldBe(0);
			this.Service.Initialize(0);
			this.Service.Online.ShouldBeTrue();
			this.Service.SelectedDeviceId.ShouldBe(0);
		}

		[TestMethod]
		public void WhenOnline_ReinitializeInvalidId_DisposesAndOffline()
		{
			this.Service.Initialize(0);
			this.Service.Online.ShouldBeTrue();
			this.Service.SelectedDeviceId.ShouldBe(0);
			this.Service.Initialize(-999).ShouldBeFalse();
			this.Service.Online.ShouldBeFalse();
			this.Service.SelectedDeviceId.ShouldBe(-1);
		}

		[TestMethod]
		public void WhenOnline_ReinitializeInvalidName_KeepsOnlineAndDeviceIdUnchanged()
		{
			this.Service.Initialize(0);
			this.Service.Online.ShouldBeTrue();
			this.Service.SelectedDeviceId.ShouldBe(0);
			this.Service.Initialize("ThisDeviceDoesNotExist").ShouldBeFalse();
			this.Service.Online.ShouldBeTrue();
			this.Service.SelectedDeviceId.ShouldBe(0);
		}

		[TestMethod]
		public void WhenOnline_ReinitializeValidIdAsName_ParsesAndStaysOnline()
		{
			this.Service.Initialize(0);
			this.Service.Online.ShouldBeTrue();
			this.Service.SelectedDeviceId.ShouldBe(0);
			this.Service.Initialize("0").ShouldBeTrue();
			this.Service.Online.ShouldBeTrue();
			this.Service.SelectedDeviceId.ShouldBe(0);
		}

		[TestMethod]
		public void WhenOfflineAfterInvalid_InitializeValidId_ServiceOnlineAndSetDeviceId()
		{
			this.Service.Initialize(-999);
			this.Service.Online.ShouldBeFalse();
			this.Service.SelectedDeviceId.ShouldBe(-1);
			this.Service.Initialize(0);
			this.Service.Online.ShouldBeTrue();
			this.Service.SelectedDeviceId.ShouldBe(0);
		}

		[TestMethod]
		public void WhenOfflineAfterDispose_InitializeValidName_ServiceOnlineAndSetDeviceId()
		{
			var deviceName = this.Service[0]?.DeviceName;
			Assert.IsNotNull(deviceName);
			this.Service.Initialize(0);
			this.Service.Dispose();
			this.Service.Initialize(deviceName!);
			this.Service.Online.ShouldBeTrue();
			this.Service.SelectedDeviceId.ShouldBe(0);
		}



	}
}

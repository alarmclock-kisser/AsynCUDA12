using AsynCUDA12.Runtime;
using ManagedCuda.BasicTypes;
using System.Runtime.InteropServices;

namespace AsynCUDA12.Tests
{
	[TestClass]
	public sealed class CudaRegisterIntegrationTest
	{
		private readonly CudaService Service = new(-1);
		private readonly Random Rndm = new();


		[TestInitialize]
		public void Initialize()
		{
			// Init first CUDA device [0] if devices are available
			if (CudaService.DeviceCount > 0)
			{
				this.Service.Initialize(0);

			}
			else
			{
				Assert.Inconclusive("No CUDA devices available.");
			}
		}

		[TestCleanup]
		public void Cleanup()
		{
			this.Service.Dispose();
			Assert.IsFalse(this.Service.Online);
		}



		// Empty state
		[TestMethod]
		public void WhenEmpty_NothingAllocated()
		{
			Assert.AreEqual(0, this.Service.RegisteredMemoryObjects);
			Assert.AreEqual(0, this.Service.StreamThreadsList.Count);
		}

		[TestMethod]
		public void WhenEmpty_NoActivity()
		{
			Assert.AreEqual(0, this.Service.ThreadsActive);
		}



		// Allocate, push & pull (+ async)
		[TestMethod]
		public void Allocate_IntArray64kSingle_Success()
		{
			CudaMem? mem = this.Service.AllocateSingle<int>(64 * 1024);
			Assert.IsNotNull(mem);
			Assert.AreNotEqual(0, this.Service.ThreadsActive);
			Assert.AreNotEqual(0, this.Service.RegisteredMemoryObjects);
			Assert.AreNotEqual(0, this.Service.StreamThreadsList.Count);
		}

		[TestMethod]
		public void PushPull_IntArray64kSingle_Success()
		{
			int[] data = new int[64 * 1024];
			for (int i = 0; i < data.Length; i++)
			{
				data[i] = this.Rndm.Next();
			}

			CudaMem? mem = this.Service.PushData<int>(data);
			Assert.IsNotNull(mem);
			Assert.AreEqual(0, this.Service.ThreadsActive);
			Assert.AreNotEqual(0, this.Service.RegisteredMemoryObjects);
			Assert.AreEqual(0, this.Service.StreamThreadsList.Count);
		}



		[TestMethod]
		public async Task Allocate_IntArray64kSingle_Async_Success()
		{
			CudaMem? mem = await this.Service.AllocateSingleAsync<int>(64 * 1024);
			Assert.IsNotNull(mem);
			Assert.AreEqual(0, this.Service.ThreadsActive);
			Assert.AreNotEqual(0, this.Service.RegisteredMemoryObjects);
			Assert.AreEqual(0, this.Service.StreamThreadsList.Count);
		}

		[TestMethod]
		public async Task PushPull_IntArray64kSingle_Async_Success()
		{
			int[] data = new int[64 * 1024];
			for (int i = 0; i < data.Length; i++)
			{
				data[i] = this.Rndm.Next();
			}
			CudaMem? mem = await this.Service.PushDataAsync<int>(data);
			Assert.IsNotNull(mem);
			Thread.Sleep(100); // Allow time for async operations to complete
			Assert.AreEqual(0, this.Service.ThreadsActive);
			Assert.AreNotEqual(0, this.Service.RegisteredMemoryObjects);
			Assert.AreEqual(0, this.Service.StreamThreadsList.Count);

			// Pull
			int[]? pulledData = new int[64 * 1024];
			pulledData = await this.Service.PullDataAsync<int>(mem.IndexPointer, false);
			Assert.AreEqual(data.LongLength, pulledData?.LongLength);
			CollectionAssert.AreEqual(data, pulledData);

		}

		[TestMethod]
		public void Allocate_IntArray64kGroup_Success()
		{
			nint[] lengths = new nint[16];
			for (int i = 0; i < lengths.Length; i++)
			{
				lengths[i] = 64 * 1024;
			}

			CudaMem? mem = this.Service.AllocateGroup<int>(lengths);
			Assert.IsNotNull(mem);
			Assert.AreEqual(0, this.Service.ThreadsActive);
			Assert.AreNotEqual(0, this.Service.RegisteredMemoryObjects);
			Assert.AreEqual(0, this.Service.StreamThreadsList.Count);
		}

		[TestMethod]
		public void PushPull_IntArray64kGroup_Success()
		{
			int[][] data = new int[16][];
			for (int i = 0; i < data.Length; i++)
			{
				data[i] = new int[64 * 1024];
				Random.Shared.NextBytes(MemoryMarshal.AsBytes(data[i].AsSpan()));
			}

			CudaMem? mem = this.Service.PushChunks<int>(data);
			Assert.IsNotNull(mem);
			Assert.AreEqual(0, this.Service.ThreadsActive);
			Assert.AreNotEqual(0, this.Service.RegisteredMemoryObjects);
			Assert.AreEqual(0, this.Service.StreamThreadsList.Count);
		}

		[TestMethod]
		public async Task Allocate_IntArray64kGroup_Async_Success()
		{
			nint[] lengths = new nint[16];
			for (int i = 0; i < lengths.Length; i++)
			{
				lengths[i] = 64 * 1024;
			}
			CudaMem? mem = await this.Service.AllocateGroupAsync<int>(lengths);
			Assert.AreEqual(0, this.Service.ThreadsActive);
			Assert.AreNotEqual(0, this.Service.RegisteredMemoryObjects);
			Assert.AreEqual(0, this.Service.StreamThreadsList.Count);
		}

		[TestMethod]
		public async Task PushPull_IntArray64kGroup_Async_Success()
		{
			int[][] data = new int[16][];
			for (int i = 0; i < data.Length; i++)
			{
				data[i] = new int[64 * 1024];
				Random.Shared.NextBytes(MemoryMarshal.AsBytes(data[i].AsSpan()));
			}
			CudaMem? mem = await this.Service.PushChunksAsync<int>(data);
			Assert.IsNotNull(mem);
			Assert.AreEqual(0, this.Service.ThreadsActive);
			Assert.AreNotEqual(0, this.Service.RegisteredMemoryObjects);
			Assert.AreEqual(0, this.Service.StreamThreadsList.Count);
		}
	}
}

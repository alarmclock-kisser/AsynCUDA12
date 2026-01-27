using alarmclockkisser.ImageHandling;
using AsynCUDA12.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsynCUDA12.Tests
{
	[TestClass]
	public sealed class CudaLauncherIntegrationTest
	{
		private readonly CudaService Service = new(-1);
		private const string ValidKernelSourceLinear = @"
		extern ""C"" __global__ void DoubleValues(float* data, int length)
		{
			int idx = blockIdx.x * blockDim.x + threadIdx.x;
			if (idx < length)
			{
				data[idx] = data[idx] * 2.0f;
			}
		}
		";
		private const string ValidKernelSourceImage = @"
		extern ""C"" __global__ void InvertImage(unsigned char* image, int width, int height)
		{
			int x = blockIdx.x * blockDim.x + threadIdx.x;
			int y = blockIdx.y * blockDim.y + threadIdx.y;
			if (x < width && y < height)
			{
				int idx = (y * width + x) * 3; // Assuming 3 channels (RGB)
				image[idx] = 255 - image[idx];       // Invert Red
				image[idx + 1] = 255 - image[idx + 1]; // Invert Green
				image[idx + 2] = 255 - image[idx + 2]; // Invert Blue
			}
		}
		";


		[TestInitialize]
		public void Initialize()
		{
			// Init first CUDA device [0] if devices are available
			if (CudaService.DeviceCount > 0)
			{
				this.Service.Initialize(0);
				Assert.IsTrue(this.Service.Online);
				Assert.IsNotNull(this.Service.Launcher);
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



		// Bad Input
		[TestMethod]
		public void LaunchKernel_Linear_InvalidInput_ShouldReturnFalse()
		{
			// Compile source & verify
			Assert.IsNotNull(this.Service.Launcher);
			Assert.IsNotNull(this.Service.Compiler);
			string? ptxPath = this.Service.Compiler.CompileString(ValidKernelSourceLinear);
			Assert.IsNotNull(ptxPath, "Compilation failed for valid source code.");

			// Prepare invalid input (null device pointer)
			IntPtr invalidDevicePtr = IntPtr.Zero;
			int numElements = 256;

			// Launch kernel
			IntPtr? result = this.Service.Launcher.ExecuteLinearKernel(ptxPath, invalidDevicePtr, [], numElements);

		}

		[TestMethod]
		public void LaunchKernel_Linear_InvalidArgs_ShouldReturnFalse()
		{
			// Compile source & verify
			Assert.IsNotNull(this.Service.Launcher);
			Assert.IsNotNull(this.Service.Compiler);
			string? ptxPath = this.Service.Compiler.CompileString(ValidKernelSourceLinear);
			Assert.IsNotNull(ptxPath, "Compilation failed for valid source code.");

			// Prepare valid device pointer
			int numElements = 256;
			var buffer = this.Service.AllocateSingle<float>(numElements);
			Assert.IsNotNull(buffer, "Failed to allocate device memory.");
			IntPtr devicePtr = buffer.IndexPointer;

			// Prepare invalid arguments (mismatched types)
			object[] invalidArgs = [0, 0.5d];

			// Launch kernel
			IntPtr? result = this.Service.Launcher.ExecuteLinearKernel(ptxPath, devicePtr, invalidArgs, numElements);

			// Verify that the result is null due to invalid arguments
			Assert.IsNull(result, "Kernel launch should fail with invalid arguments.");

			// Clean up
			this.Service.FreeMemory(devicePtr);
		}

		[TestMethod]
		public void LaunchKernel_Image_InvalidInput_ShouldReturnFalse()
		{
			// Compile source & verify
			Assert.IsNotNull(this.Service.Launcher);
			Assert.IsNotNull(this.Service.Compiler);
			string? ptxPath = this.Service.Compiler.CompileString(ValidKernelSourceLinear);
			Assert.IsNotNull(ptxPath, "Compilation failed for valid source code.");

			// Prepare invalid input (null device pointer)
			IntPtr invalidDevicePtr = IntPtr.Zero;
			int numElements = 256;

			// Launch kernel
			IntPtr? result = this.Service.Launcher.ExecuteImageKernel(ptxPath, invalidDevicePtr, [], numElements, numElements);

		}

		[TestMethod]
		public void LaunchKernel_Image_InvalidArgs_ShouldReturnFalse()
		{
			// Compile source & verify
			Assert.IsNotNull(this.Service.Launcher);
			Assert.IsNotNull(this.Service.Compiler);
			string? ptxPath = this.Service.Compiler.CompileString(ValidKernelSourceLinear);
			Assert.IsNotNull(ptxPath, "Compilation failed for valid source code.");

			// Prepare valid device pointer
			int numElements = 256;
			var buffer = this.Service.AllocateSingle<float>(numElements);
			Assert.IsNotNull(buffer, "Failed to allocate device memory.");
			IntPtr devicePtr = buffer.IndexPointer;

			// Prepare invalid arguments (mismatched types)
			object[] invalidArgs = [0, 0.5d];

			// Launch kernel
			IntPtr? result = this.Service.Launcher.ExecuteImageKernel(ptxPath, devicePtr, invalidArgs, numElements, numElements);

			// Verify that the result is null due to invalid arguments
			Assert.IsNull(result, "Kernel launch should fail with invalid arguments.");

			// Clean up
			this.Service.FreeMemory(devicePtr);
		}


		// Args
		[TestMethod]
		public void GetArgs_Linear_ValidArgs_ShouldReturnOne()
		{
			// Compile source & verify
			Assert.IsNotNull(this.Service.Launcher);
			Assert.IsNotNull(this.Service.Compiler);
			string? ptxPath = this.Service.Compiler.CompileString(ValidKernelSourceLinear);
			Assert.IsNotNull(ptxPath, "Compilation failed for valid source code.");

			// Get args
			var args = this.Service.Compiler.GetArguments(ptxPath);
			
			// Verify
			Assert.IsNotNull(args);
			Assert.AreEqual(2, args.Count);
		}

		[TestMethod]
		public void GetArgs_Image_ValidArgs_ShouldReturnOne()
		{
			// Compile source & verify
			Assert.IsNotNull(this.Service.Launcher);
			Assert.IsNotNull(this.Service.Compiler);
			string? ptxPath = this.Service.Compiler.CompileString(ValidKernelSourceImage);
			Assert.IsNotNull(ptxPath, "Compilation failed for valid source code.");

			// Get args
			var args = this.Service.Compiler.GetArguments(ptxPath);

			// Verify
			Assert.IsNotNull(args);
			Assert.AreEqual(3, args.Count);
		}



		// Linear
		[TestMethod]
		public void LaunchKernel_Linear_ValidInput_ShouldReturnDoubledValues()
		{
			// Compile source & verify
			Assert.IsNotNull(this.Service.Launcher);
			Assert.IsNotNull(this.Service.Compiler);
			string? ptxPath = this.Service.Compiler.CompileString(ValidKernelSourceLinear);
			Assert.IsNotNull(ptxPath, "Compilation failed for valid source code.");
			
			// Prepare input data
			int numElements = 256;
			float[] inputData = Enumerable.Range(0, numElements).Select(i => (float) i).ToArray();
			CudaMem? mem = this.Service.PushData(inputData);
			Assert.IsNotNull(mem, "Failed to allocate device memory.");

			// Launch kernel
			IntPtr? resultPtr = this.Service.Launcher.ExecuteLinearKernel(ptxPath, mem.IndexPointer, [numElements], numElements);
			Assert.IsNotNull(resultPtr, "Kernel execution failed.");
			
			// Copy result back to host
			float[]? outputData = this.Service.PullData<float>(mem);
			Assert.IsNotNull(outputData, "Failed to pull data from device.");

			// Verify results
			for (int i = 0; i < numElements; i++)
			{
				Assert.AreEqual(inputData[i] * 2.0f, outputData[i], $"Mismatch at index {i}");
			}

			// Clean up
			this.Service.FreeMemory(mem);
		}

		[TestMethod]
		public async Task LaunchKernel_Linear_ValidInput_ShouldReturnDoubledValues_Async()
		{
			// Compile source & verify
			Assert.IsNotNull(this.Service.Launcher);
			Assert.IsNotNull(this.Service.Compiler);
			string? ptxPath = this.Service.Compiler.CompileString(ValidKernelSourceLinear);
			Assert.IsNotNull(ptxPath, "Compilation failed for valid source code.");
			// Prepare input data
			int numElements = 256;
			float[] inputData = Enumerable.Range(0, numElements).Select(i => (float)i).ToArray();
			CudaMem? mem = await this.Service.PushDataAsync(inputData);
			Assert.IsNotNull(mem, "Failed to allocate device memory.");
			// Launch kernel
			IntPtr? resultPtr = await this.Service.Launcher.ExecuteLinearKernelAsync(ptxPath, mem.IndexPointer, [numElements], numElements);
			Assert.IsNotNull(resultPtr, "Kernel execution failed.");
			// Copy result back to host
			float[]? outputData = await this.Service.PullDataAsync<float>(mem);
			Assert.IsNotNull(outputData, "Failed to pull data from device.");
			// Verify results
			for (int i = 0; i < numElements; i++)
			{
				Assert.AreEqual(inputData[i] * 2.0f, outputData[i], $"Mismatch at index {i}");
			}
			// Clean up
			this.Service.FreeMemory(mem);
		}



		// Image
		[TestMethod]
		public async Task LaunchKernel_Image_ValidInput_ShouldReturnInvertedImage()
		{
			// Compile source & verify
			Assert.IsNotNull(this.Service.Launcher);
			Assert.IsNotNull(this.Service.Compiler);
			string? ptxPath = this.Service.Compiler.CompileString(ValidKernelSourceImage);
			Assert.IsNotNull(ptxPath, "Compilation failed for valid source code.");

			// Resource image
			ImageCollection collection = new(loadResources: true);

			// Prepare input data (push bytes)
			byte[] sourceData = (await collection.Images.First().GetBytesAsync()).ToArray();
			int width = collection.Images.First().Width;
			int height = collection.Images.First().Height;
			int channels = collection.Images.First().Channels;

			byte[] inputData;
			if (channels == 4)
			{
				inputData = new byte[width * height * 3];
				for (int i = 0, dst = 0; i < width * height; i++)
				{
					int src = i * 4;
					inputData[dst++] = sourceData[src];
					inputData[dst++] = sourceData[src + 1];
					inputData[dst++] = sourceData[src + 2];
				}
			}
			else
			{
				inputData = sourceData;
			}

			CudaMem? mem = await this.Service.PushDataAsync(inputData);
			Assert.IsNotNull(mem, "Failed to allocate device memory.");
			
			// Launch kernel
			IntPtr? resultPtr = await this.Service.Launcher.ExecuteImageKernelAsync(ptxPath, mem.IndexPointer, [], width, height);
			Assert.IsNotNull(resultPtr, "Kernel execution failed.");
			
			// Copy result back to host
			byte[]? outputData = await this.Service.PullDataAsync<byte>(mem);
			Assert.IsNotNull(outputData, "Failed to pull data from device.");

			// Verify results
			for (int i = 0; i < inputData.Length; i++)
			{
				Assert.AreEqual((byte) (255 - inputData[i]), outputData[i], $"Mismatch at index {i}");
			}

			// Clean up
			this.Service.FreeMemory(mem);
		}

		[TestMethod]
		public async Task LaunchKernel_Image_ValidInput_ShouldReturnInvertedImage_Async()
		{
			// Compile source & verify
			Assert.IsNotNull(this.Service.Launcher);
			Assert.IsNotNull(this.Service.Compiler);
			string? ptxPath = this.Service.Compiler.CompileString(ValidKernelSourceImage);
			Assert.IsNotNull(ptxPath, "Compilation failed for valid source code.");
			// Prepare input data (simple gradient image)
			int width = 16;
			int height = 16;
			int channels = 3;
			byte[] inputData = new byte[width * height * channels];
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					int idx = (y * width + x) * channels;
					inputData[idx] = (byte)(x * 16);       // R
					inputData[idx + 1] = (byte)(y * 16);   // G
					inputData[idx + 2] = (byte)((x + y) * 8); // B
				}
			}
			CudaMem? mem = await this.Service.PushDataAsync(inputData);
			Assert.IsNotNull(mem, "Failed to allocate device memory.");
			// Launch kernel
			IntPtr? resultPtr = await this.Service.Launcher.ExecuteImageKernelAsync(ptxPath, mem.IndexPointer, [], width, height);
			Assert.IsNotNull(resultPtr, "Kernel execution failed.");
			// Copy result back to host
			byte[]? outputData = await this.Service.PullDataAsync<byte>(mem);
			Assert.IsNotNull(outputData, "Failed to pull data from device.");
			// Verify results
			for (int i = 0; i < inputData.Length; i++)
			{
				Assert.AreEqual((byte)(255 - inputData[i]), outputData[i], $"Mismatch at index {i}");
			}
			// Clean up
			this.Service.FreeMemory(mem);
		}

	}
}

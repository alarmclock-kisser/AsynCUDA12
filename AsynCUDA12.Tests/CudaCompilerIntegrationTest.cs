using AsynCUDA12.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsynCUDA12.Tests
{
	[TestClass]
	public sealed class CudaCompilerIntegrationTest
	{
		private readonly CudaService Service = new(-1);


		[TestInitialize]
		public void Initialize()
		{
			// Init first CUDA device [0] if devices are available
			if (CudaService.DeviceCount > 0)
			{
				this.Service.Initialize(0);
				Assert.IsTrue(this.Service.Online);
				Assert.IsNotNull(this.Service.Compiler);
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
		public void CompileKernel_InvalidSource_ShouldReturnNull()
		{
			string invalidSource = @"
			__global__ void InvalidKernel(float* data)
			{
				int idx = threadIdx.x + blockIdx.x * blockDim.x;
				data[idx] = data[idx] * 2.0f // Missing semicolon
			}
			";

			string? ptxPath = this.Service.Compiler?.CompileString(invalidSource);
			
			Assert.IsNull(ptxPath, "Compilation should fail for invalid source code.");
		}


		// Compile
		[TestMethod]
		public void CompileKernel_ValidSource_ShouldReturnPtxPath()
		{
			string validSource = @"
			extern ""C"" __global__ void DoubleValues(float* data)
			{
				int idx = threadIdx.x + blockIdx.x * blockDim.x;
				data[idx] = data[idx] * 2.0f;
			}
			";
			string? ptxPath = this.Service.Compiler?.CompileString(validSource);

			Assert.IsNotNull(ptxPath, "Compilation should succeed for valid source code.");
			Assert.IsTrue(System.IO.File.Exists(ptxPath), "PTX file should exist at the returned path.");
		}

		[TestMethod]
		public void CompileKernel_ValidSourceFile_ShouldReturnPtxPath()
		{
			string validSource = @"
			extern ""C"" __global__ void DoubleValues(float* data)
			{
				int idx = threadIdx.x + blockIdx.x * blockDim.x;
				data[idx] = data[idx] * 2.0f;
			}
			";
			string sourceFilePath = System.IO.Path.GetTempFileName() + ".cu";
			System.IO.File.WriteAllText(sourceFilePath, validSource);
			string? ptxPath = this.Service.Compiler?.CompileKernel(sourceFilePath);
			Assert.IsNotNull(ptxPath, "Compilation should succeed for valid source file.");
			Assert.IsTrue(System.IO.File.Exists(ptxPath), "PTX file should exist at the returned path.");
			// Cleanup
			System.IO.File.Delete(sourceFilePath);
		}


		// Load & Unload
		[TestMethod]
		public void LoadKernel_InvalidPtxPath_ShouldReturnNull()
		{
			string invalidPtxPath = "non_existent_file.ptx";
			var module = this.Service.Compiler?.LoadKernel(invalidPtxPath);
			Assert.IsNull(module, "Loading should fail for invalid PTX path.");
		}

		[TestMethod]
		public void LoadKernel_ValidPtxPath_ShouldReturnModule()
		{
			string validSource = @"
			extern ""C"" __global__ void DoubleValues(float* data)
			{
				int idx = threadIdx.x + blockIdx.x * blockDim.x;
				data[idx] = data[idx] * 2.0f;
			}
			";
			string? ptxPath = this.Service.Compiler?.CompileString(validSource);
			Assert.IsNotNull(ptxPath, "Compilation should succeed for valid source code.");
			var module = this.Service.Compiler?.LoadKernel(ptxPath);
			Assert.IsNotNull(module, "Loading should succeed for valid PTX path.");
			Assert.IsNotNull(this.Service.Compiler, "Compiler instance should not be null before unloading kernel.");
			this.Service.Compiler.UnloadKernel();
			Assert.IsTrue(this.Service.Compiler.KernelName == null, "Kernel should be unloaded.");
		}

	}
}

using AsynCUDA12.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AsynCUDA12.Tests
{
	[TestClass]
	public sealed class CudaFourierIntegrationTest
	{
		private CudaService Service = null!;
		private readonly Random Rndm = new();


		[TestInitialize]
		public void Initialize()
		{
			// Skip (inconclusive) on machines without a usable CUDA driver/device. The CudaService
			// constructor enumerates devices eagerly, so it must only be created after this guard.
			GpuDatabase.TestData.RequireCuda();

			this.Service = new CudaService(-1);
			this.Service.Initialize(0);
		}

		[TestCleanup]
		public void Cleanup()
		{
			this.Service?.Dispose();
			Assert.IsFalse(this.Service?.Online ?? false);
		}



		// Bad Input
		[TestMethod]
		public void Fft_InvalidComplexInput_ShouldReturnNull()
		{
			Complex[] data = new Complex[1000];
			for (int i = 0; i < data.Length; i++)
			{
				data[i] = new Complex(this.Rndm.NextDouble(), this.Rndm.NextDouble());
			}

			// Push to device
			CudaMem? inputMem = this.Service.PushData(data);
			Assert.IsNotNull(inputMem);

			// Perform FFT
			IntPtr fftPtr = this.Service.Fourier?.PerformFft(inputMem.IndexPointer, true) ?? IntPtr.Zero;
			Assert.AreEqual(inputMem.IndexPointer, fftPtr);
		}

		[TestMethod]
		public void Ifft_InvalidRealInput_ShouldReturnNull()
		{
			float[] data = new float[1000];
			for (int i = 0; i < data.Length; i++)
			{
				data[i] = (float)this.Rndm.NextDouble();
			}

			// Push to device
			CudaMem? inputMem = this.Service.PushData(data);
			Assert.IsNotNull(inputMem);

			// Perform IFFT
			IntPtr ifftPtr = this.Service.Fourier?.PerformIfft(inputMem.IndexPointer, true) ?? IntPtr.Zero;
			Assert.AreEqual(inputMem.IndexPointer, ifftPtr);
		}


		// Sync
		[TestMethod]
		public void Fft_Ifft_Sync()
		{
			// Create float array with sinwave values
			float[] data = new float[65536 * 16];
			for (int i = 0; i < data.Length; i++)
			{
				data[i] = (float)Math.Sin(2.0 * Math.PI * 440.0 * (i / 44100.0));
			}

			// Push to device
			CudaMem? inputMem = this.Service.PushData(data);
			Assert.IsNotNull(inputMem);

			// Perform FFT
			IntPtr? fftPtr = this.Service.Fourier?.PerformFft(inputMem.IndexPointer, false);
			Assert.IsNotNull(fftPtr);

			// Get mem
			CudaMem? outputMem = this.Service[fftPtr.Value];
			Assert.IsNotNull(outputMem);

			// Perform IFFT
			IntPtr? ifftPtr = this.Service.Fourier?.PerformIfft(outputMem.IndexPointer, false);
			Assert.IsNotNull(ifftPtr);

			// Get mem
			CudaMem? resultMem = this.Service[ifftPtr.Value];
			Assert.IsNotNull(resultMem);

			// Pull data
			float[]? resultData = this.Service.PullData<float>(resultMem.IndexPointer);
			Assert.IsNotNull(resultData);
			Assert.AreEqual(data.Length, resultData.Length);

			// Normalize result
			resultData = this.Service.Fourier?.NormalizeIfftResult(resultData);
			Assert.IsNotNull(resultData);

			// Compare data
			for (int i = 0; i < data.Length; i++)
			{
				Assert.AreEqual(data[i], resultData[i], 1e-3f, $"Mismatch at index {i}");
			}
		}

		// Async
		[TestMethod]
		public async Task Fft_Ifft_Async()
		{
			// Verify Fourier service is available
			Assert.IsNotNull(this.Service.Fourier);

			// Create float array with sinwave values
			float[] data = new float[65536 * 16];
			for (int i = 0; i < data.Length; i++)
			{
				data[i] = (float) Math.Sin(2.0 * Math.PI * 440.0 * (i / 44100.0));
			}

			// Push to device
			CudaMem? inputMem = await this.Service.PushDataAsync(data);
			Assert.IsNotNull(inputMem);

			// Perform FFT
			IntPtr? fftPtr = await this.Service.Fourier.PerformFftAsync(inputMem.IndexPointer, false);
			Assert.IsNotNull(fftPtr);

			// Get mem
			CudaMem? outputMem = this.Service[fftPtr.Value];
			Assert.IsNotNull(outputMem);

			// Perform IFFT
			IntPtr? ifftPtr = await this.Service.Fourier.PerformIfftAsync(outputMem.IndexPointer, false);
			Assert.IsNotNull(ifftPtr);

			// Get mem
			CudaMem? resultMem = this.Service[ifftPtr.Value];
			Assert.IsNotNull(resultMem);

			// Pull data
			float[]? resultData = await this.Service.PullDataAsync<float>(resultMem.IndexPointer);
			Assert.IsNotNull(resultData);
			Assert.AreEqual(data.Length, resultData.Length);

			// Normalize result
			resultData = await this.Service.Fourier.NormalizeIfftResultAsync(resultData);
			Assert.IsNotNull(resultData);

			// Compare data
			for (int i = 0; i < data.Length; i++)
			{
				Assert.AreEqual(data[i], resultData[i], 1e-3f, $"Mismatch at index {i}");
			}
		}

		// [TestMethod]
		public async Task Fft_Ifft_Many_Async()
		{
			// Verify Fourier service is available
			Assert.IsNotNull(this.Service.Fourier);

			// Create float array with sinwave values
			float[][] chunks = new float[16][];
			const double twoPi = 2.0 * Math.PI;
			double phaseStep = twoPi * 440.0 / 44100.0;
			Parallel.For(0, chunks.Length, j =>
			{
				float[] arr = new float[65536];
				double phase = 0.0;
				for (int i = 0; i < arr.Length; i++)
				{
					arr[i] = (float) Math.Sin(phase);
					phase += phaseStep;
				}
				chunks[j] = arr;
			});


			// Push to device
			CudaMem? inputMem = await this.Service.PushChunksAsync(chunks);
			Assert.IsNotNull(inputMem);

			// Perform FFT
			IntPtr? fftPtr = await this.Service.Fourier.PerformFftManyAsync(inputMem.IndexPointer, false);
			Assert.IsNotNull(fftPtr);

			// Get mem
			CudaMem? outputMem = this.Service[fftPtr.Value];
			Assert.IsNotNull(outputMem);

			// Perform IFFT
			IntPtr? ifftPtr = await this.Service.Fourier.PerformIfftManyAsync(outputMem.IndexPointer, false);
			Assert.IsNotNull(ifftPtr);

			// Get mem
			CudaMem? resultMem = this.Service[ifftPtr.Value];
			Assert.IsNotNull(resultMem);

			// Pull data
			// Fix für CS8604: Null-Prüfung vor ToArray()
			float[][]? resultDataEnumerable = (await this.Service.PullChunksAsync<float>(resultMem.IndexPointer))?.ToArray();
			Assert.IsNotNull(resultDataEnumerable);
			float[][] resultData = resultDataEnumerable.ToArray();
			Assert.AreEqual(chunks.Length, resultData.Length);

			// Normalize result
			resultData = await this.Service.Fourier.NormalizeIfftManyResultAsync<float>(resultData);
			Assert.IsNotNull(resultData);

			// Compare data
			Parallel.For(0, chunks.Length, j =>
			{
				float[] original = chunks[j];
				float[] result = resultData[j];
				Assert.AreEqual(original.Length, result.Length);
				for (int i = 0; i < original.Length; i++)
				{
					Assert.AreEqual(original[i], result[i], 1e-3f, $"Mismatch at chunk {j}, index {i}");
				}
			});
		}


		// Parallel async
		[TestMethod]
		public async Task Fft_Ifft_Parallel_Async()
		{
			int count = 16;

			var tasks = new List<Task>();
			for (int i = 0; i < count; i++)
			{
				tasks.Add(Task.Run(async () =>
				{
					using var service = new CudaService();
					if (!service.Initialize(0))
					{
						Assert.Inconclusive("No CUDA devices available.");
					}

					// Create float array with sinwave values
					float[] data = new float[65536 * 16];
					for (int j = 0; j < data.Length; j++)
					{
						data[j] = (float) Math.Sin(2.0 * Math.PI * 440.0 * (j / 44100.0));
					}

					CudaMem? inputMem = await service.PushDataAsync(data);
					Assert.IsNotNull(inputMem);

					IntPtr? fftPtr = await service.Fourier!.PerformFftAsync(inputMem.IndexPointer, false);
					Assert.IsNotNull(fftPtr);

					CudaMem? outputMem = service[fftPtr.Value];
					Assert.IsNotNull(outputMem);

					IntPtr? ifftPtr = await service.Fourier.PerformIfftAsync(outputMem.IndexPointer, false);
					Assert.IsNotNull(ifftPtr);

					CudaMem? resultMem = service[ifftPtr.Value];
					Assert.IsNotNull(resultMem);

					float[]? resultData = await service.PullDataAsync<float>(resultMem.IndexPointer);
					Assert.IsNotNull(resultData);
					Assert.AreEqual(data.Length, resultData.Length);

					resultData = await service.Fourier.NormalizeIfftResultAsync(resultData);
					Assert.IsNotNull(resultData);

					for (int j = 0; j < data.Length; j++)
					{
						Assert.AreEqual(data[j], resultData[j], 1e-3f, $"Mismatch at index {j}");
					}
				}));
			}
			await Task.WhenAll(tasks);
		}



	}
}

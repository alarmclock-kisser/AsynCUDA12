using ManagedCuda;
using ManagedCuda.BasicTypes;
using ManagedCuda.CudaFFT;
using ManagedCuda.VectorTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsynCUDA12.Runtime
{
	public class CudaFourier : IDisposable
	{
		// Fields
		private PrimaryContext CTX;
		private CudaRegister Register;


		// Ctor
		internal CudaFourier(PrimaryContext ctx, CudaRegister register)
		{
			this.CTX = ctx;
			this.Register = register;
		}


		// Methods
		public IntPtr PerformFft(IntPtr indexPointer, bool keep = false)
		{
			// Check initialized & input pointer
			if (this.Register == null || this.CTX == null || indexPointer == IntPtr.Zero)
			{
				return IntPtr.Zero;
			}

			// Get memory from register
			var mem = this.Register[indexPointer];
			if (mem == null || mem.IndexPointer == IntPtr.Zero || mem.IndexLength == IntPtr.Zero)
			{
				CudaLogger.Log("Memory not found or invalid pointer.");
				return IntPtr.Zero;
			}

			// Check input memory type
			if (mem.ElementType != typeof(float))
			{
				CudaLogger.Log("Input memory type is not float.");
				return indexPointer;
			}

			// Allocate output memory
			var outputMem = this.Register.AllocateGroup<float2>(mem.Lengths.Select(l => l).ToArray());
			if (outputMem == null || outputMem.IndexPointer == IntPtr.Zero)
			{
				CudaLogger.Log("Could not allocate output memory.");
				return indexPointer;
			}

			// Use Context instead of stream(s)	
			// this.CTX.Synchronize();

			// Check if all lengths are the same (else create plan for each length in loop)
			try
			{
				if (mem.Lengths.Distinct().Count() == 1)
				{
					int nx = (int) mem.IndexLength.ToInt64();
					cufftType fftType = cufftType.R2C;
					int batch = 1;

					CudaFFTPlan1D plan = new(nx, fftType, batch);
					for (int i = 0; i < mem.Count; i++)
					{
						CUdeviceptr inPtr = new(mem.Pointers[i]);
						CUdeviceptr outPtr = new(outputMem.Pointers[i]);
						plan.Exec(inPtr, outPtr);
						outputMem.Pointers[i] = outPtr.Pointer;
					}
					plan.Dispose();
				}
				else
				{
					int[] nx = mem.Lengths.Select(l => (int) l.ToInt64()).ToArray();
					cufftType fftType = cufftType.R2C;
					int batch = 1;

					for (int i = 0; i < mem.Count; i++)
					{
						CudaFFTPlan1D plan = new(nx[i], fftType, batch);
						CUdeviceptr inPtr = new(mem.Pointers[i]);
						CUdeviceptr outPtr = new(outputMem.Pointers[i]);
						plan.Exec(inPtr, outPtr);
						outputMem.Pointers[i] = outPtr.Pointer;
						plan.Dispose();
					}
				}
			}
			catch (Exception ex)
			{
				CudaLogger.Log(ex, "Error during FFT execution.");
				this.Register.FreeMemory(outputMem);
				return indexPointer;
			}

			// Check output memory
			if (outputMem.IndexPointer == IntPtr.Zero)
			{
				CudaLogger.Log("FFT execution failed, result pointer is null.");
				this.Register.FreeMemory(outputMem);
				return indexPointer;
			}

			// Synchronize context
			// this.CTX.Synchronize();

			// Optionally free input memory
			if (!keep)
			{
				this.Register.FreeMemory(indexPointer);
			}

			return outputMem.IndexPointer;
		}

		public IntPtr PerformIfft(IntPtr indexPointer, bool keep = false)
		{
			// Check initialized & input pointer
			if (this.Register == null || this.CTX == null || indexPointer == IntPtr.Zero)
			{
				return IntPtr.Zero;
			}

			// Get memory from register
			var mem = this.Register[indexPointer];
			if (mem == null || mem.IndexPointer == IntPtr.Zero || mem.IndexLength == IntPtr.Zero)
			{
				CudaLogger.Log("Memory not found or invalid pointer.");
				return IntPtr.Zero;
			}

			// Check input memory type
			if (mem.ElementType != typeof(float2))
			{
				CudaLogger.Log("Input memory type is not float2.");
				return indexPointer;
			}

			// Allocate output memory
			var outputMem = this.Register.AllocateGroup<float>(mem.Lengths.Select(l => l).ToArray());
			if (outputMem == null || outputMem.IndexPointer == IntPtr.Zero)
			{
				CudaLogger.Log("Could not allocate output memory.");
				return indexPointer;
			}

			// Use Context instead of stream(s)	
			// this.CTX.Synchronize();

			// Check if all lengths are the same (else create plan for each length in loop)
			try
			{
				if (mem.Lengths.Distinct().Count() == 1)
				{
					int nx = (int) mem.IndexLength.ToInt64();
					cufftType fftType = cufftType.C2R;
					int batch = 1;

					CudaFFTPlan1D plan = new(nx, fftType, batch);
					for (int i = 0; i < mem.Count; i++)
					{
						CUdeviceptr inPtr = new(mem.Pointers[i]);
						CUdeviceptr outPtr = new(outputMem.Pointers[i]);
						plan.Exec(inPtr, outPtr);
						outputMem.Pointers[i] = outPtr.Pointer;
					}
					plan.Dispose();
				}
				else
				{
					int[] nx = mem.Lengths.Select(l => (int) l.ToInt64()).ToArray();
					cufftType fftType = cufftType.C2R;
					int batch = 1;

					for (int i = 0; i < mem.Count; i++)
					{
						CudaFFTPlan1D plan = new(nx[i], fftType, batch);
						CUdeviceptr inPtr = new(mem.Pointers[i]);
						CUdeviceptr outPtr = new(outputMem.Pointers[i]);
						plan.Exec(inPtr, outPtr);
						outputMem.Pointers[i] = outPtr.Pointer;
						plan.Dispose();
					}
				}
			}
			catch (Exception ex)
			{
				CudaLogger.Log(ex, "Error during IFFT execution.");
				this.Register.FreeMemory(outputMem);
				return indexPointer;
			}

			// Check output memory
			if (outputMem.IndexPointer == IntPtr.Zero)
			{
				CudaLogger.Log("FFT execution failed, result pointer is null.");
				this.Register.FreeMemory(outputMem);
				return indexPointer;
			}

			// Synchronize context
			// this.CTX.Synchronize();

			// Optionally free input memory
			if (!keep)
			{
				this.Register.FreeMemory(indexPointer);
			}

			return outputMem.IndexPointer;
		}

		public T[] NormalizeIfftResult<T>(IEnumerable<T> data) where T : unmanaged
		{
			if (data == null)
			{
				return Array.Empty<T>();
			}

			T[] result = data.ToArray();
			long count = result.LongLength;
			if (count == 0)
			{
				return result;
			}

			double scale = 1.0 / count;
			if (typeof(T) == typeof(float))
			{
				float fscale = (float) scale;
				for (int i = 0; i < result.Length; i++)
				{
					result[i] = (T) (object) ((float) (object) result[i] * fscale);
				}
			}
			else if (typeof(T) == typeof(double))
			{
				for (int i = 0; i < result.Length; i++)
				{
					result[i] = (T) (object) ((double) (object) result[i] * scale);
				}
			}

			return result;
		}



		// Methods (async)
		public async Task<IntPtr> PerformFftAsync(IntPtr pointer, bool keep = false)
		{
			if (this.Register == null || this.CTX == null || pointer == IntPtr.Zero)
			{
				return IntPtr.Zero;
			}

			var mem = this.Register[pointer];
			if (mem == null || mem.IndexPointer == IntPtr.Zero || mem.IndexLength == IntPtr.Zero)
			{
				CudaLogger.Log("Memory not found or invalid pointer.");
				return IntPtr.Zero;
			}

			var outputMem = await this.Register.AllocateGroupAsync<float2>(mem.Lengths.Select(l => l).ToArray());
			if (outputMem == null || outputMem.IndexPointer == IntPtr.Zero)
			{
				CudaLogger.Log("Could not allocate output memory.");
				return pointer;
			}

			try
			{
				var stream = this.Register.GetStream();
				if (stream == null)
				{
					this.Register.FreeMemory(outputMem);
					return pointer;
				}

				int nx = (int) mem.IndexLength.ToInt64();
				cufftType fftType = cufftType.R2C;
				int batch = 1;
				CudaFFTPlan1D plan = new(nx, fftType, batch, stream.Stream);

				for (int i = 0; i < mem.Count; i++)
				{
					CUdeviceptr inPtr = new(mem.Pointers[i]);
					CUdeviceptr outPtr = new(outputMem.Pointers[i]);
					plan.Exec(inPtr, outPtr);
					outputMem.Pointers[i] = outPtr.Pointer;
				}

				await Task.Run(stream.Synchronize);
				plan.Dispose();

			}
			catch (Exception ex)
			{
				CudaLogger.Log(ex, "Error during FFT execution.");
				this.Register.FreeMemory(outputMem);
				return pointer;
			}

			if (outputMem.IndexPointer == IntPtr.Zero)
			{
				CudaLogger.Log("FFT execution failed, result pointer is null.");
				this.Register.FreeMemory(outputMem);
				return pointer;
			}

			if (!keep)
			{
				this.Register.FreeMemory(pointer);
			}

			return outputMem.IndexPointer;
		}

		public async Task<IntPtr> PerformIfftAsync(IntPtr pointer, bool keep = false)
		{
			if (this.Register == null || this.CTX == null || pointer == IntPtr.Zero)
			{
				return IntPtr.Zero;
			}

			var mem = this.Register[pointer];
			if (mem == null || mem.IndexPointer == IntPtr.Zero || mem.IndexLength == IntPtr.Zero)
			{
				CudaLogger.Log("Memory not found or invalid pointer.");
				return IntPtr.Zero;
			}

			var outputMem = await this.Register.AllocateGroupAsync<float>(mem.Lengths.Select(l => l).ToArray());
			if (outputMem == null || outputMem.IndexPointer == IntPtr.Zero)
			{
				CudaLogger.Log("Could not allocate output memory.");
				return pointer;
			}

			try
			{
				var stream = this.Register.GetStream();
				if (stream == null)
				{
					this.Register.FreeMemory(outputMem);
					return pointer;
				}

				int nx = (int) mem.IndexLength.ToInt64();
				cufftType fftType = cufftType.C2R;
				int batch = 1;
				CudaFFTPlan1D plan = new(nx, fftType, batch, stream.Stream);

				for (int i = 0; i < mem.Count; i++)
				{
					CUdeviceptr inPtr = new(mem.Pointers[i]);
					CUdeviceptr outPtr = new(outputMem.Pointers[i]);
					plan.Exec(inPtr, outPtr);
					outputMem.Pointers[i] = outPtr.Pointer;
				}

				await Task.Run(stream.Synchronize);
				plan.Dispose();
			}
			catch (Exception ex)
			{
				CudaLogger.Log(ex, "Error during IFFT execution.");
				this.Register.FreeMemory(outputMem);
				return pointer;
			}

			if (outputMem.IndexPointer == IntPtr.Zero)
			{
				CudaLogger.Log("IFFT execution failed, result pointer is null.");
				this.Register.FreeMemory(outputMem);
				return pointer;
			}

			if (!keep)
			{
				this.Register.FreeMemory(pointer);
			}

			return outputMem.IndexPointer;
		}

		public async Task<T[]> NormalizeIfftResultAsync<T>(IEnumerable<T> data) where T : unmanaged
		{
			return await Task.Run(() => this.NormalizeIfftResult(data));
		}




		// Methods (many async)
		public async Task<IntPtr> PerformFftManyAsync(IntPtr indexPointer, bool keep = false)
		{
			if (this.Register == null || this.CTX == null || indexPointer == IntPtr.Zero)
			{
				return IntPtr.Zero;
			}

			var mem = this.Register[indexPointer];
			if (mem == null || mem.IndexPointer == IntPtr.Zero)
			{
				CudaLogger.Log("Memory not found or invalid pointer.");
				return IntPtr.Zero;
			}

			IntPtr[] lengths = mem.Lengths;

			var outputMem = await this.Register.AllocateGroupAsync<float2>(lengths);
			if (outputMem == null || outputMem.IndexPointer == IntPtr.Zero)
			{
				CudaLogger.Log("Could not allocate output memory.");
				return indexPointer;
			}

			try
			{
				var stream = this.Register.GetStream();
				if (stream == null)
				{
					this.Register.FreeMemory(outputMem);
					return indexPointer;
				}

				int rank = 1;
				cufftType fftType = cufftType.R2C;
				CudaFFTPlanMany plan = new(rank, lengths.Select(l => (int) l.ToInt64()).ToArray(), mem.Count, fftType, stream.Stream);

				for (int i = 0; i < mem.Count; i++)
				{
					CUdeviceptr inPtr = new(mem.Pointers[i]);
					CUdeviceptr outPtr = new(outputMem.Pointers[i]);

					plan.Exec(inPtr, outPtr);

					outputMem.Pointers[i] = outPtr.Pointer;
				}

				if (outputMem.IndexPointer == IntPtr.Zero)
				{
					CudaLogger.Log("FFT-many execution failed, result pointer is null.");
					this.Register.FreeMemory(outputMem);
					return indexPointer;
				}

				await Task.Run(stream.Synchronize);

				if (!keep)
				{
					this.Register.FreeMemory(indexPointer);
				}

				plan.Dispose();
			}
			catch (Exception ex)
			{
				CudaLogger.Log(ex, "Error during FFT-many execution.");
				return indexPointer;
			}

			return outputMem.IndexPointer;
		}

		public async Task<IntPtr> PerformIfftManyAsync(IntPtr indexPointer, bool keep = false)
		{
			if (this.Register == null || this.CTX == null || indexPointer == IntPtr.Zero)
			{
				return IntPtr.Zero;
			}

			var mem = this.Register[indexPointer];
			if (mem == null || mem.IndexPointer == IntPtr.Zero)
			{
				CudaLogger.Log("Memory not found or invalid pointer.");
				return IntPtr.Zero;
			}

			IntPtr[] lengths = mem.Lengths;

			var outputMem = await this.Register.AllocateGroupAsync<float>(lengths);
			if (outputMem == null || outputMem.IndexPointer == IntPtr.Zero)
			{
				CudaLogger.Log("Could not allocate output memory.");
				return indexPointer;
			}

			try
			{
				var stream = this.Register.GetStream();
				if (stream == null)
				{
					this.Register.FreeMemory(outputMem);
					return indexPointer;
				}

				int rank = 1;
				cufftType fftType = cufftType.C2R;
				CudaFFTPlanMany plan = new(rank, lengths.Select(l => (int) l.ToInt64()).ToArray(), mem.Count, fftType, stream.Stream);

				for (int i = 0; i < mem.Count; i++)
				{
					CUdeviceptr inPtr = new(mem.Pointers[i]);
					CUdeviceptr outPtr = new(outputMem.Pointers[i]);

					plan.Exec(inPtr, outPtr);

					outputMem.Pointers[i] = outPtr.Pointer;
				}

				if (outputMem.IndexPointer == IntPtr.Zero)
				{
					CudaLogger.Log("IFFT-many execution failed, result pointer is null.");
					this.Register.FreeMemory(outputMem);
					return indexPointer;
				}

				await Task.Run(stream.Synchronize);

				if (!keep)
				{
					this.Register.FreeMemory(indexPointer);
				}

				plan.Dispose();
			}
			catch (Exception ex)
			{
				CudaLogger.Log(ex, "Error during IFFT-many execution.");
				return indexPointer;
			}

			return outputMem.IndexPointer;
		}

		internal T[][] NormalizeIfftManyResult<T>(IEnumerable<T[]> data) where T : unmanaged
		{
			if (data == null)
			{
				return Array.Empty<T[]>();
			}

			T[][] arrays = data.ToArray();
			for (int i = 0; i < arrays.Length; i++)
			{
				T[] src = arrays[i];
				if (src == null || src.Length == 0)
				{
					arrays[i] = src ?? Array.Empty<T>();
					continue;
				}

				double scale = 1.0 / src.Length;
				T[] dst = new T[src.Length];
				if (typeof(T) == typeof(float))
				{
					float fscale = (float) scale;
					for (int j = 0; j < src.Length; j++)
					{
						dst[j] = (T) (object) ((float) (object) src[j] * fscale);
					}
				}
				else if (typeof(T) == typeof(double))
				{
					for (int j = 0; j < src.Length; j++)
					{
						dst[j] = (T) (object) ((double) (object) src[j] * scale);
					}
				}
				else
				{
					Array.Copy(src, dst, src.Length);
				}

				arrays[i] = dst;
			}

			return arrays;
		}

		public async Task<T[][]> NormalizeIfftManyResultAsync<T>(IEnumerable<T[]> data) where T : unmanaged
		{
			return await Task.Run(() => this.NormalizeIfftManyResult(data));
		}





		// Dispose
		public void Dispose()
		{
			GC.SuppressFinalize(this);
		}

	}
}

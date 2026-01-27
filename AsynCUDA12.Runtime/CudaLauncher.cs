using ManagedCuda;
using ManagedCuda.BasicTypes;
using ManagedCuda.VectorTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsynCUDA12.Runtime
{
	public class CudaLauncher : IDisposable
	{
		// Fields
		private PrimaryContext Context;
		private CudaRegister Register;
		private CudaFourier Fourier;
		private CudaCompiler Compiler;

		private CudaKernel? Kernel => this.Compiler.Kernel;
		public string? KernelName => this.Compiler.KernelName;





		// Ctor
		internal CudaLauncher(PrimaryContext ctx, CudaRegister register, CudaFourier fourier, CudaCompiler compiler)
		{
			this.Context = ctx;
			this.Register = register;
			this.Fourier = fourier;
			this.Compiler = compiler;
		}




		// Methods
		public IntPtr? ExecuteLinearKernel(string? kernelName, IntPtr pointer, object[] arguments, int length)
		{
			this.Context.SetCurrent();

			if (this.Kernel == null)
			{
				if (string.IsNullOrEmpty(kernelName))
				{
					CudaLogger.Log("Kernel name not provided for loading.");
					return null;
				}

				this.Compiler.LoadKernel(kernelName);

				if (this.Kernel == null)
				{
					CudaLogger.Log($"Kernel not loaded '{kernelName ?? "N/A"}'");
					return null;
				}
			}

			if (pointer == IntPtr.Zero)
			{
				CudaLogger.Log("Invalid input pointer (null).");
				return null;
			}

			Dictionary<string, Type> args = this.Compiler.GetArguments(null);
			List<Type> expectedUserArgs = [];
			foreach (var arg in args)
			{
				if (arg.Value == typeof(IntPtr))
				{
					continue;
				}
				expectedUserArgs.Add(arg.Value);
			}

			if (arguments.Length != expectedUserArgs.Count)
			{
				CudaLogger.Log($"Argument count mismatch. Expected {expectedUserArgs.Count}, got {arguments.Length}.");
				return null;
			}

			for (int i = 0; i < expectedUserArgs.Count; i++)
			{
				object? value = arguments[i];
				Type expected = expectedUserArgs[i];
				if (value == null || !expected.IsAssignableFrom(value.GetType()))
				{
					CudaLogger.Log($"Argument type mismatch at index {i}. Expected {expected.Name}, got {value?.GetType().Name ?? "null"}.");
					return null;
				}
			}

			try
			{
				CUdeviceptr devicePtr = new(pointer);

				object[] kernelArgs = new object[args.Count];
				int userArgIndex = 0;
				for (int i = 0; i < args.Count; i++)
				{
					Type expected = args.ElementAt(i).Value;
					if (expected == typeof(IntPtr))
					{
						kernelArgs[i] = devicePtr;
					}
					else
					{
						kernelArgs[i] = arguments[userArgIndex++];
					}
				}

				int blockSize = 256;
				int gridSize = (length + blockSize - 1) / blockSize;
				this.Kernel.BlockDimensions = new dim3(blockSize, 1, 1);
				this.Kernel.GridDimensions = new dim3(gridSize, 1, 1);

				this.Kernel.Run(kernelArgs);
				CudaLogger.Log($"Kernel executed '{this.KernelName ?? "N/A"}'");
				this.Context.Synchronize();

				return pointer;
			}
			catch (Exception ex)
			{
				CudaLogger.Log($"Failed to execute kernel '{this.KernelName ?? "N/A"}'", ex);
				return null;
			}
		}

		public IntPtr? ExecuteImageKernel(string? kernelName, IntPtr pointer, object[] arguments, int width, int height, int channels = 4, int bitdepth = 8)
		{
			this.Context.SetCurrent();

			if (this.Kernel == null)
			{
				if (string.IsNullOrEmpty(kernelName))
				{
					CudaLogger.Log("Kernel name not provided for loading.");
					return null;
				}

				this.Compiler.LoadKernel(kernelName);
				if (this.Kernel == null)
				{
					CudaLogger.Log($"Kernel not loaded '{kernelName ?? "N/A"}'");
					return null;
				}
			}

			if (pointer == IntPtr.Zero)
			{
				CudaLogger.Log("Invalid input pointer (null).");
				return null;
			}

			Dictionary<string, Type> args = this.Compiler.GetArguments(null);
			List<Type> expectedUserArgs = [];
			int pointersHandled = 0;
			foreach (var arg in args)
			{
				string name = arg.Key;
				Type type = arg.Value;
				if (type == typeof(IntPtr) && pointersHandled < 2)
				{
					pointersHandled++;
					continue;
				}
				if (name.Contains("width", StringComparison.OrdinalIgnoreCase) ||
					name.Contains("height", StringComparison.OrdinalIgnoreCase) ||
					name.Contains("chan", StringComparison.OrdinalIgnoreCase) ||
					name.Contains("bit", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				expectedUserArgs.Add(type);
			}

			if (arguments.Length != expectedUserArgs.Count)
			{
				CudaLogger.Log($"Argument count mismatch. Expected {expectedUserArgs.Count}, got {arguments.Length}.");
				return null;
			}

			for (int i = 0; i < expectedUserArgs.Count; i++)
			{
				object? value = arguments[i];
				Type expected = expectedUserArgs[i];
				if (value == null || !expected.IsAssignableFrom(value.GetType()))
				{
					CudaLogger.Log($"Argument type mismatch at index {i}. Expected {expected.Name}, got {value?.GetType().Name ?? "null"}.");
					return null;
				}
			}

			try
			{
				CUdeviceptr devicePtr = new(pointer);

				CUdeviceptr outputPtr = new(this.Register.AllocateSingle<byte>(width * height * ((channels * bitdepth) / 8))?.IndexPointer ?? IntPtr.Zero);

				object[] kernelArgs = this.Compiler.MergeArgumentsImage(devicePtr, outputPtr, width, height, channels, bitdepth, arguments);

				int totalThreadsX = width;
				int totalThreadsY = height;

				int blockSizeX = 8;
				int blockSizeY = 8;

				int gridSizeX = (totalThreadsX + blockSizeX - 1) / blockSizeX;
				int gridSizeY = (totalThreadsY + blockSizeY - 1) / blockSizeY;

				this.Kernel.BlockDimensions = new dim3(blockSizeX, blockSizeY, 1);  // 2D-Block
				this.Kernel.GridDimensions = new dim3(gridSizeX, gridSizeY, 1);     // 2D-Grid

				this.Kernel.Run(kernelArgs);

				CudaLogger.Log($"Kernel executed '{this.KernelName ?? "N/A"}'");

				if (outputPtr.Pointer != 0)
				{
					this.Register.FreeMemory(devicePtr.Pointer);
				}

				this.Context.Synchronize();

				return outputPtr.Pointer != 0 ? outputPtr.Pointer : pointer;
			}
			catch (Exception ex)
			{
				CudaLogger.Log($"Failed to execute kernel '{this.KernelName ?? "N/A"}'", ex);
				return null;
			}
		}




		// Methods (async)
		public async Task<IntPtr?> ExecuteLinearKernelAsync(string? kernelName, IntPtr pointer, object[] arguments, int length)
		{
			return await Task.Run(() => ExecuteLinearKernel(kernelName, pointer, arguments, length));
		}

		public async Task<IntPtr?> ExecuteImageKernelAsync(string? kernelName, IntPtr pointer, object[] arguments, int width, int height, int channels = 4, int bitdepth = 8)
		{
			return await Task.Run(() => ExecuteImageKernel(kernelName, pointer, arguments, width, height, channels, bitdepth));
		}




		// Dispose
		public void Dispose()
		{
			GC.SuppressFinalize(this);
		}

	}
}

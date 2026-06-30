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
	/// <summary>
	/// Executes compiled CUDA kernels against device memory. The launcher resolves kernel arguments,
	/// validates their count and types, configures the launch grid/block dimensions and runs the kernel,
	/// supporting both linear (1D) workloads and image (2D) workloads.
	/// </summary>
	public class CudaLauncher : IDisposable
	{
		// Fields
		/// <summary>The CUDA primary context the kernels are launched on.</summary>
		private PrimaryContext Context;

		/// <summary>The registry used to resolve and manage device memory.</summary>
		private CudaRegister Register;

		/// <summary>The Fourier helper available for transform-based workloads.</summary>
		private CudaFourier Fourier;

		/// <summary>The compiler used to load kernels and inspect their argument signatures.</summary>
		private CudaCompiler Compiler;

		/// <summary>Gets the currently loaded kernel from the associated <see cref="CudaCompiler"/>.</summary>
		private CudaKernel? Kernel => this.Compiler.Kernel;

		/// <summary>Gets the name of the currently loaded kernel, or <c>null</c> if none is loaded.</summary>
		public string? KernelName => this.Compiler.KernelName;





		// Ctor
		/// <summary>
		/// Initializes a new instance of the <see cref="CudaLauncher"/> class.
		/// </summary>
		/// <param name="ctx">The CUDA primary context to launch kernels on.</param>
		/// <param name="register">The memory registry used to resolve device buffers.</param>
		/// <param name="fourier">The Fourier helper instance.</param>
		/// <param name="compiler">The compiler that loads kernels and exposes their argument definitions.</param>
		internal CudaLauncher(PrimaryContext ctx, CudaRegister register, CudaFourier fourier, CudaCompiler compiler)
		{
			this.Context = ctx;
			this.Register = register;
			this.Fourier = fourier;
			this.Compiler = compiler;
		}




		// Methods
		/// <summary>
		/// Executes a kernel over a one-dimensional (linear) data buffer.
		/// Loads the kernel if necessary, validates the user-supplied arguments against the kernel signature,
		/// computes a 1D grid using a block size of 256 threads and runs the kernel synchronously.
		/// </summary>
		/// <param name="kernelName">The kernel to load when none is currently loaded; ignored if a kernel is already loaded.</param>
		/// <param name="pointer">The native handle of the input/output device buffer.</param>
		/// <param name="arguments">The non-pointer scalar arguments expected by the kernel, in order.</param>
		/// <param name="length">The number of elements to process (used to size the launch grid).</param>
		/// <returns>The input <paramref name="pointer"/> on success; otherwise <c>null</c>.</returns>
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

		/// <summary>
		/// Executes a kernel over a two-dimensional image buffer.
		/// Loads the kernel if necessary, validates the user-supplied arguments (excluding the two image pointers
		/// and the width/height/channels/bit-depth parameters that are injected automatically), configures an 8x8
		/// 2D block layout and runs the kernel in-place synchronously.
		/// </summary>
		/// <param name="kernelName">The kernel to load when none is currently loaded; ignored if a kernel is already loaded.</param>
		/// <param name="pointer">The native handle of the image device buffer (used as both input and output).</param>
		/// <param name="arguments">The additional scalar arguments expected by the kernel, in order.</param>
		/// <param name="width">The image width in pixels.</param>
		/// <param name="height">The image height in pixels.</param>
		/// <param name="channels">The number of channels per pixel (default 4).</param>
		/// <param name="bitdepth">The bit depth per channel (default 8).</param>
		/// <returns>The input <paramref name="pointer"/> on success; otherwise <c>null</c>.</returns>
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

			// For image kernels that operate in-place, reuse the input buffer as the output buffer
			CUdeviceptr outputPtr = devicePtr;

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

				this.Context.Synchronize();

				return pointer;
			}
			catch (Exception ex)
			{
				CudaLogger.Log($"Failed to execute kernel '{this.KernelName ?? "N/A"}'", ex);
				return null;
			}
		}




		// Methods (async)
		/// <summary>
		/// Asynchronously executes a kernel over a one-dimensional (linear) data buffer by running
		/// <see cref="ExecuteLinearKernel"/> on a background thread.
		/// </summary>
		/// <param name="kernelName">The kernel to load when none is currently loaded.</param>
		/// <param name="pointer">The native handle of the input/output device buffer.</param>
		/// <param name="arguments">The non-pointer scalar arguments expected by the kernel, in order.</param>
		/// <param name="length">The number of elements to process.</param>
		/// <returns>A task producing the input <paramref name="pointer"/> on success; otherwise <c>null</c>.</returns>
		public async Task<IntPtr?> ExecuteLinearKernelAsync(string? kernelName, IntPtr pointer, object[] arguments, int length)
		{
			return await Task.Run(() => ExecuteLinearKernel(kernelName, pointer, arguments, length));
		}

		/// <summary>
		/// Asynchronously executes a kernel over a two-dimensional image buffer by running
		/// <see cref="ExecuteImageKernel"/> on a background thread.
		/// </summary>
		/// <param name="kernelName">The kernel to load when none is currently loaded.</param>
		/// <param name="pointer">The native handle of the image device buffer (used as both input and output).</param>
		/// <param name="arguments">The additional scalar arguments expected by the kernel, in order.</param>
		/// <param name="width">The image width in pixels.</param>
		/// <param name="height">The image height in pixels.</param>
		/// <param name="channels">The number of channels per pixel (default 4).</param>
		/// <param name="bitdepth">The bit depth per channel (default 8).</param>
		/// <returns>A task producing the input <paramref name="pointer"/> on success; otherwise <c>null</c>.</returns>
		public async Task<IntPtr?> ExecuteImageKernelAsync(string? kernelName, IntPtr pointer, object[] arguments, int width, int height, int channels = 4, int bitdepth = 8)
		{
			return await Task.Run(() => ExecuteImageKernel(kernelName, pointer, arguments, width, height, channels, bitdepth));
		}




		// Dispose
		/// <summary>
		/// Releases the resources used by the <see cref="CudaLauncher"/>.
		/// </summary>
		public void Dispose()
		{
			GC.SuppressFinalize(this);
		}

	}
}

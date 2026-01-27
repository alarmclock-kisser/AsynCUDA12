using ManagedCuda;
using ManagedCuda.BasicTypes;
using ManagedCuda.NVRTC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsynCUDA12.Runtime
{
	public class CudaCompiler : IDisposable
	{
		// Fields
		private PrimaryContext Context;

		internal CudaKernel? Kernel = null;
		public string? KernelName = null;
		public string? KernelFile = null;
		public string? KernelCode = null;

		// Properties (static)
		public static string KernelPath = EnsureKernelDirectory();

		public static List<string> SourceFiles => GetCuFiles();
		public static List<string> CompiledFiles => GetPtxFiles();


		// Ctor
		public CudaCompiler(PrimaryContext context)
		{
			this.Context = context;

			KernelPath = EnsureKernelDirectory();
			try
			{
				Directory.CreateDirectory(KernelPath);
				Directory.CreateDirectory(Path.Combine(KernelPath, "CU"));
				Directory.CreateDirectory(Path.Combine(KernelPath, "PTX"));
				Directory.CreateDirectory(Path.Combine(KernelPath, "Logs"));
			}
			catch (Exception ex)
			{
				CudaLogger.Log("Failed to create kernel directory, using temporary path", ex);
				KernelPath = Path.Combine(Path.GetTempPath(), "AsynCUDA12", "Kernels");
				Directory.CreateDirectory(KernelPath);
				Directory.CreateDirectory(Path.Combine(KernelPath, "CU"));
				Directory.CreateDirectory(Path.Combine(KernelPath, "PTX"));
				Directory.CreateDirectory(Path.Combine(KernelPath, "Logs"));
			}

			// Compile all kernels
			this.CompileAll(false, true);
		}

		private static bool TryCreateDirectory(string path, out string createdPath)
		{
			try
			{
				Directory.CreateDirectory(path);
				createdPath = path;
				return true;
			}
			catch
			{
				createdPath = string.Empty;
				return false;
			}
		}

		private static string EnsureKernelDirectory(string? differentPath = null)
		{
			if (!string.IsNullOrWhiteSpace(differentPath) && TryCreateDirectory(differentPath, out var customDir))
			{
				return customDir;
			}

			DirectoryInfo? current = new(AppContext.BaseDirectory);
			for (int i = 0; i < 10 && current != null; i++)
			{
				if (string.Equals(current.Name, "AsynCUDA12.Runtime", StringComparison.OrdinalIgnoreCase))
				{
					string runtimeDir = Path.Combine(current.FullName, "Kernels");
					if (TryCreateDirectory(runtimeDir, out var createdRuntimeDir))
					{
						return createdRuntimeDir;
					}
				}

				string siblingRuntime = Path.Combine(current.FullName, "AsynCUDA12.Runtime");
				if (Directory.Exists(siblingRuntime))
				{
					string candidate = Path.Combine(siblingRuntime, "Kernels");
					if (TryCreateDirectory(candidate, out var created))
					{
						return created;
					}
				}

				current = current.Parent;
			}

			if (TryCreateDirectory(Path.Combine(AppContext.BaseDirectory, "Kernels"), out var assemblyDir))
			{
				return assemblyDir;
			}

			string fallbackPath = Path.Combine(Path.GetTempPath(), "AsynCUDA12", "Kernels");
			Directory.CreateDirectory(fallbackPath);
			return fallbackPath;
		}




		// Methods (static)
		public static List<string> GetPtxFiles(string? path = null)
		{
			path ??= Path.Combine(KernelPath, "PTX");

			// Get all PTX files in kernel path
			string[] files = Directory.GetFiles(path, "*.ptx").Select(f => Path.GetFullPath(f)).ToArray();

			// Return files
			return files.ToList();
		}

		public static List<string> GetCuFiles(string? path = null)
		{
			path ??= Path.Combine(KernelPath, "CU");

			// Get all CU files in kernel path
			string[] files = Directory.GetFiles(path, "*.cu").Select(f => Path.GetFullPath(f)).ToArray();

			// Return files
			return files.ToList();
		}



		// Methods (Load & Unload)
		public void UnloadKernel()
		{
			// Unload kernel
			if (this.Kernel != null)
			{
				try
				{
					this.Context.UnloadKernel(this.Kernel);
				}
				catch (Exception ex)
				{
					CudaLogger.Log("Failed to unload kernel", ex);
				}
				this.Kernel = null;
			}

			this.KernelName = null;
			this.KernelFile = null;
			this.KernelCode = null;
		}

		public CudaKernel? LoadKernel(string kernelName, bool silent = false)
		{
			if (this.Context == null)
			{
				CudaLogger.Log("No CUDA context available");
				return null;
			}

			// Unload?
			if (this.Kernel != null)
			{
				this.UnloadKernel();
			}

			string displayName = kernelName;
			string ptxPath;
			string cuPath;
			bool isPtxPath = File.Exists(kernelName) && string.Equals(Path.GetExtension(kernelName), ".ptx", StringComparison.OrdinalIgnoreCase);
			if (isPtxPath)
			{
				ptxPath = Path.GetFullPath(kernelName);
				displayName = Path.GetFileNameWithoutExtension(ptxPath);
				string ptxDirectory = Path.GetDirectoryName(ptxPath) ?? string.Empty;
				string coLocatedCu = Path.Combine(ptxDirectory, displayName + ".cu");
				cuPath = File.Exists(coLocatedCu) ? coLocatedCu : Path.Combine(KernelPath, "CU", displayName + ".cu");
			}
			else
			{
				ptxPath = Path.Combine(KernelPath, "PTX", displayName + ".ptx");
				cuPath = Path.Combine(KernelPath, "CU", displayName + ".cu");
			}

			// Log
			Stopwatch sw = Stopwatch.StartNew();
			if (!silent)
			{
				CudaLogger.Log("Started loading kernel " + displayName);
			}
			string logpath = Path.Combine(KernelPath, "Logs", displayName + "_load.log");

			// Try to load kernel
			try
			{
				// Load ptx code
				byte[] ptxCode = File.ReadAllBytes(ptxPath);

				// Load kernel
				this.Kernel = this.Context.LoadKernelPTX(ptxCode, displayName);
				this.KernelName = displayName;
				this.KernelFile = ptxPath;
				this.KernelCode = File.Exists(cuPath) ? File.ReadAllText(cuPath) : null;
			}
			catch (Exception ex)
			{
				if (!silent)
				{
					CudaLogger.Log("Failed to load kernel " + displayName, ex);
					string logMsg = ex.Message + Environment.NewLine + Environment.NewLine + ex.InnerException?.Message ?? "";
					File.WriteAllText(logpath, logMsg);
				}
				this.Kernel = null;
			}

			// Log
			sw.Stop();
			long deltaMicros = sw.ElapsedTicks / (Stopwatch.Frequency / (1000L * 1000L));
			if (!silent)
			{
				CudaLogger.Log($"Kernel loaded within {deltaMicros.ToString("N0")} µs");
			}

			return this.Kernel;
		}


		// Methods (Compile)
		public void CompileAll(bool silent = false, bool logErrors = false)
		{
			List<string> sourceFiles = SourceFiles;

			// Compile all source files
			foreach (string sourceFile in sourceFiles)
			{
				string? ptx = this.CompileKernel(sourceFile, silent);
				if (string.IsNullOrEmpty(ptx) && logErrors)
				{
					CudaLogger.Log("Compilation failed: ", Path.GetFileNameWithoutExtension(sourceFile));
				}
			}
		}

		public string? CompileKernel(string filepath, bool silent = false)
		{
			if (this.Context == null)
			{
				if (!silent)
				{
					CudaLogger.Log("No CUDA initialized");
				}
				return null;
			}

			// If file is not a .cu file, but raw kernel string, compile that
			if (Path.GetExtension(filepath) != ".cu")
			{
				return this.CompileString(filepath, silent);
			}

			string kernelName = Path.GetFileNameWithoutExtension(filepath);

			string logpath = Path.Combine(KernelPath, "Logs", kernelName + ".log");

			Stopwatch sw = Stopwatch.StartNew();
			if (!silent)
			{
				CudaLogger.Log("Compiling kernel '" + kernelName + "'");
			}

			// Load kernel file
			string kernelCode = File.ReadAllText(filepath);


			CudaRuntimeCompiler rtc = new(kernelCode, kernelName);

			try
			{
				// Compile kernel
				rtc.Compile([]);
				string log = rtc.GetLogAsString();

				if (log.Length > 0)
				{
					// Count double \n
					int count = log.Split(["\n\n"], StringSplitOptions.None).Length - 1;
					if (!silent)
					{
						CudaLogger.Log($"Compiled with {count} warnings");
					}
					File.WriteAllText(logpath, log);
				}

				sw.Stop();
				long deltaMicros = sw.ElapsedTicks / (Stopwatch.Frequency / (1000L * 1000L));
				if (!silent)
				{
					CudaLogger.Log($"Compiled within {deltaMicros} µs. Repo\\" + Path.GetRelativePath(KernelPath, logpath));
				}

				// Get ptx code
				byte[] ptxCode = rtc.GetPTX();

				// Export ptx
				string ptxPath = Path.Combine(KernelPath, "PTX", kernelName + ".ptx");
				File.WriteAllBytes(ptxPath, ptxCode);

				if (!silent)
				{
					CudaLogger.Log($"PTX exported: {ptxPath}");
				}

				return ptxPath;
			}
			catch (Exception ex)
			{
				File.WriteAllText(logpath, rtc.GetLogAsString());
				CudaLogger.Log(ex);

				return null;
			}

		}

		public string? CompileString(string kernelString, bool silent = false)
		{
			if (this.Context == null)
			{
				if (!silent)
				{
					CudaLogger.Log("No CUDA initialized");
				}
				return null;
			}

			string kernelName = kernelString.Split("void ")[1].Split("(")[0];

			string logpath = Path.Combine(KernelPath, "Logs", kernelName + ".log");

			Stopwatch sw = Stopwatch.StartNew();
			if (!silent)
			{
				CudaLogger.Log("Compiling kernel '" + kernelName + "'");
			}

			// Load kernel file
			string kernelCode = kernelString;

			// Save also the kernel string as .c file
			string cPath = Path.Combine(KernelPath, "CU", kernelName + ".cu");
			File.WriteAllText(cPath, kernelCode);


			CudaRuntimeCompiler rtc = new(kernelCode, kernelName);

			try
			{
				// Compile kernel
				rtc.Compile([]);
				string log = rtc.GetLogAsString();

				if (log.Length > 0)
				{
					// Count double \n
					int count = log.Split(["\n\n"], StringSplitOptions.None).Length - 1;
					if (!silent)
					{
						CudaLogger.Log($"Compiled with {count} warnings");
					}
					File.WriteAllText(logpath, rtc.GetLogAsString());
				}


				sw.Stop();
				long deltaMicros = sw.ElapsedTicks / (Stopwatch.Frequency / (1000L * 1000L));
				if (!silent)
				{
					CudaLogger.Log($"Compiled within {deltaMicros} µs. Repo\\" + Path.GetRelativePath(KernelPath, logpath));
				}


				// Get ptx code
				byte[] ptxCode = rtc.GetPTX();

				// Export ptx
				string ptxPath = Path.Combine(KernelPath, "PTX", kernelName + ".ptx");
				File.WriteAllBytes(ptxPath, ptxCode);

				if (!silent)
				{
					CudaLogger.Log($"PTX exported: {ptxPath}");
				}

				return ptxPath;
			}
			catch (Exception ex)
			{
				File.WriteAllText(logpath, rtc.GetLogAsString());
				CudaLogger.Log(ex);

				return null;
			}
		}

		public string? PrecompileKernelString(string kernelString, bool silent = false)
		{
			// Check contains "extern c"
			if (!kernelString.Contains("extern \"C\""))
			{
				if (!silent)
				{
					CudaLogger.Log("Kernel string does not contain 'extern \"C\"'");
				}
				return null;
			}

			// Check contains "__global__ "
			if (!kernelString.Contains("__global__"))
			{
				if (!silent)
				{
					CudaLogger.Log("Kernel string does not contain '__global__'");
				}
				return null;
			}

			// Check contains "void "
			if (!kernelString.Contains("void "))
			{
				if (!silent)
				{
					CudaLogger.Log("Kernel string does not contain 'void '");
				}
				return null;
			}

			// Check contains int
			if (!kernelString.Contains("int ") && !kernelString.Contains("long "))
			{
				if (!silent)
				{
					CudaLogger.Log("Kernel string does not contain 'int ' (for array length)");
				}
				return null;
			}

			// Check if every bracket is closed (even amount) for {} and () and []
			int open = kernelString.Count(c => c == '{');
			int close = kernelString.Count(c => c == '}');
			if (open != close)
			{
				if (!silent)
				{
					CudaLogger.Log("Kernel string has unbalanced brackets { } ");
				}
				return null;
			}
			open = kernelString.Count(c => c == '(');
			close = kernelString.Count(c => c == ')');
			if (open != close)
			{
				if (!silent)
				{
					CudaLogger.Log("Kernel string has unbalanced brackets ( ) ");
				}
				return null;
			}
			open = kernelString.Count(c => c == '[');
			close = kernelString.Count(c => c == ']');
			if (open != close)
			{
				if (!silent)
				{
					CudaLogger.Log("Kernel string has unbalanced brackets [ ] ");
				}
				return null;
			}

			// Check if kernel contains "blockIdx.x" and "blockDim.x" and "threadIdx.x"
			if (!kernelString.Contains("blockIdx.x") || !kernelString.Contains("blockDim.x") || !kernelString.Contains("threadIdx.x"))
			{
				if (!silent)
				{
					CudaLogger.Log("Kernel string should contain 'blockIdx.x', 'blockDim.x' and 'threadIdx.x'");
				}
			}

			// Get name between "void " and "("
			int start = kernelString.IndexOf("void ") + "void ".Length;
			int end = kernelString.IndexOf("(", start);
			string name = kernelString.Substring(start, end - start);

			// Trim every line ends from empty spaces (split -> trim -> aggregate)
			kernelString = kernelString.Split("\n").Select(x => x.TrimEnd()).Aggregate((x, y) => x + "\n" + y);

			// Log name
			if (!silent)
			{
				CudaLogger.Log($"Succesfully precompiled kernel string '{name}'");
			}

			return name;
		}


		// Methods (Arguments)
		public Type GetArgumentType(string typeName)
		{
			// Pointers are always IntPtr (containing *)
			if (typeName.Contains("*"))
			{
				return typeof(IntPtr);
			}

			string typeIdentifier = typeName.Split(' ').LastOrDefault()?.Trim() ?? "void";
			Type type = typeIdentifier switch
			{
				"int" => typeof(int),
				"float" => typeof(float),
				"double" => typeof(double),
				"char" => typeof(char),
				"bool" => typeof(bool),
				"void" => typeof(void),
				"byte" => typeof(byte),
				_ => typeof(void)
			};

			return type;
		}

		public Dictionary<string, Type> GetArguments(string? kernelCode = null, bool silent = false)
		{
			string? sourceCode = null;

			// Interpret the input: kernel name, .cu path, or .ptx path
			if (!string.IsNullOrWhiteSpace(kernelCode))
			{
				string input = kernelCode.Trim();
				string extension = Path.GetExtension(input);

				if (File.Exists(input))
				{
					if (string.Equals(extension, ".cu", StringComparison.OrdinalIgnoreCase))
					{
						sourceCode = File.ReadAllText(input);
					}
					else if (string.Equals(extension, ".ptx", StringComparison.OrdinalIgnoreCase))
					{
						string displayName = Path.GetFileNameWithoutExtension(input);
						string ptxDirectory = Path.GetDirectoryName(input) ?? string.Empty;
						string coLocatedCu = Path.Combine(ptxDirectory, displayName + ".cu");
						string fallbackCu = Path.Combine(KernelPath, "CU", displayName + ".cu");
						string resolvedCu = File.Exists(coLocatedCu) ? coLocatedCu : fallbackCu;

						if (File.Exists(resolvedCu))
						{
							sourceCode = File.ReadAllText(resolvedCu);
						}
					}
				}
				else
				{
					if (string.IsNullOrEmpty(extension))
					{
						// Treat as kernel name
						string cuPathByName = Path.Combine(KernelPath, "CU", input + ".cu");
						if (File.Exists(cuPathByName))
						{
							sourceCode = File.ReadAllText(cuPathByName);
						}
						else
						{
							string ptxPathByName = Path.Combine(KernelPath, "PTX", input + ".ptx");
							if (File.Exists(ptxPathByName))
							{
								string ptxDirectory = Path.GetDirectoryName(ptxPathByName) ?? string.Empty;
								string coLocatedCu = Path.Combine(ptxDirectory, input + ".cu");
								if (File.Exists(coLocatedCu))
								{
									sourceCode = File.ReadAllText(coLocatedCu);
								}
							}
						}
					}
					else if (string.Equals(extension, ".cu", StringComparison.OrdinalIgnoreCase))
					{
						string cuPath = Path.Combine(KernelPath, "CU", input);
						if (File.Exists(cuPath))
						{
							sourceCode = File.ReadAllText(cuPath);
						}
					}
				}
			}

			// If a path or name was provided and no kernel is loaded yet, try to load it
			if (this.Kernel == null && !string.IsNullOrWhiteSpace(kernelCode))
			{
				string input = kernelCode.Trim();
				string extension = Path.GetExtension(input);
				if (File.Exists(input) || string.IsNullOrEmpty(extension))
				{
					this.LoadKernel(input, true);
				}
			}

			sourceCode ??= this.KernelCode;

			// If no code is available yet, try to resolve the co-located .cu file based on KernelFile
			if (string.IsNullOrEmpty(sourceCode) && !string.IsNullOrEmpty(this.KernelFile))
			{
				string displayName = Path.GetFileNameWithoutExtension(this.KernelFile);
				string ptxDirectory = Path.GetDirectoryName(this.KernelFile) ?? string.Empty;
				string coLocatedCu = Path.Combine(ptxDirectory, displayName + ".cu");
				string fallbackCu = Path.Combine(KernelPath, "CU", displayName + ".cu");
				string resolvedCu = File.Exists(coLocatedCu) ? coLocatedCu : fallbackCu;

				if (File.Exists(resolvedCu))
				{
					sourceCode = File.ReadAllText(resolvedCu);
				}
			}

			kernelCode = sourceCode;
			if (string.IsNullOrEmpty(kernelCode))
			{
				if (!silent)
				{
					CudaLogger.Log($"Kernel code is empty '{this.KernelName ?? "N/A"}'");
				}
				return [];
			}

			Dictionary<string, Type> arguments = [];

			int index = kernelCode.IndexOf("__global__ void");
			if (index == -1)
			{
				if (!silent)
				{
					CudaLogger.Log($"'__global__ void' not found '{this.KernelName ?? "N/A"}'");
				}
				return [];
			}

			index = kernelCode.IndexOf("(", index);
			if (index == -1)
			{
				if (!silent)
				{
					CudaLogger.Log($"'__global__ void' not found '{this.KernelName ?? "N/A"}'");
				}
				return [];
			}

			int endIndex = kernelCode.IndexOf(")", index);
			if (endIndex == -1)
			{
				if (!silent)
				{
					CudaLogger.Log($"')' not found '{this.KernelName ?? "N/A"}'");
				}
				return [];
			}

			string[] args = kernelCode.Substring(index + 1, endIndex - index - 1).Split(',').Select(x => x.Trim()).ToArray();

			// Get loaded kernels function args
			for (int i = 0; i < args.Length; i++)
			{
				string name = args[i].Split(' ').LastOrDefault() ?? "N/A";
				string typeName = args[i].Replace(name, "").Trim();
				Type type = this.GetArgumentType(typeName);

				// Add to dictionary
				arguments.Add(name, type);
			}

			return arguments;
		}

		public int GetPointerArgsCount(string? kernelCode = null, bool silent = false)
		{
			kernelCode ??= this.KernelCode;
			if (string.IsNullOrEmpty(kernelCode) || this.Kernel == null)
			{
				if (!silent)
				{
					CudaLogger.Log($"Kernel code is empty '{this.KernelName ?? "N/A"}'");
				}
				return 0;
			}
			
			Dictionary<string, Type> args = this.GetArguments(kernelCode, silent);
			
			return args.Values.Count(t => t == typeof(IntPtr));
		}


		// Merge args for execution
		public object[] MergeArgumentsRaw(object[] arguments)
		{
			// Get kernel argument definitions
			Dictionary<string, Type> args = this.GetArguments(null, false);
			// Create array for kernel arguments
			object[] kernelArgs = new object[args.Count];
			// Integrate invariables if name fits (contains)
			for (int i = 0; i < kernelArgs.Length; i++)
			{
				string name = args.ElementAt(i).Key;
				// Check if argument is in arguments array
				for (int j = 0; j < arguments.Length; j++)
				{
					if (name == args.ElementAt(j).Key)
					{
						kernelArgs[i] = arguments[j];
						break;
					}
				}
				// If not found, set to 0
				if (kernelArgs[i] == null)
				{
					kernelArgs[i] = 0;
				}
			}
			return kernelArgs;
		}

		public object[] MergeArgumentsAudio(CUdeviceptr inputPointer, CUdeviceptr outputPointer, int sampleRate = 44100, int channels = 2, int bitdepth = 32, Dictionary<string, object>? namedArguments = null)
		{
			// Get kernel argument definitions
			Dictionary<string, Type> args = this.GetArguments(null, false);

			// Create array for kernel arguments
			object[] kernelArgs = new object[args.Count];
			int pointersCount = 0;

			// Integrate invariables if name fits (contains)
			for (int i = 0; i < kernelArgs.Length; i++)
			{
				string name = args.ElementAt(i).Key;
				Type type = args.ElementAt(i).Value;
				if (pointersCount == 0 && type == typeof(IntPtr))
				{
					kernelArgs[i] = inputPointer;
					pointersCount++;
					CudaLogger.Log($"In-pointer: <{inputPointer}>");
				}
				else if (pointersCount == 1 && type == typeof(IntPtr))
				{
					kernelArgs[i] = outputPointer;
					pointersCount++;
					CudaLogger.Log($"Out-pointer: <{outputPointer}>");
				}
				else if (name.Contains("sample") && type == typeof(int))
				{
					CudaLogger.Log($"SampleRate: [{sampleRate}]");
				}
				else if (name.Contains("chan") && type == typeof(int))
				{
					kernelArgs[i] = channels;
					CudaLogger.Log($"Channels: [{channels}]");
				}
				else if (name.Contains("bit") && type == typeof(int))
				{
					kernelArgs[i] = bitdepth;
					CudaLogger.Log($"Bits: [{bitdepth}]");
				}
				else
				{
					// Check if argument is in arguments array
					if (namedArguments != null && namedArguments.Count > 0)
					{
						for (int j = 0; j < namedArguments.Count; j++)
						{
							if (name.Equals(args.ElementAt(j).Key, StringComparison.CurrentCultureIgnoreCase))
							{
								if (namedArguments.TryGetValue(name, out object? value))
								{
									kernelArgs[i] = value;
									CudaLogger.Log($"Named argument: {name} = {value}");
									break;
								}
								else
								{
									CudaLogger.Log($"Named argument '{name}' not found in provided arguments");
									kernelArgs[i] = 0;
								}
							}
						}
					}

					// If not found, set to 0
					if (kernelArgs[i] == null)
					{
						kernelArgs[i] = 0;
					}
				}
			}

			return kernelArgs;
		}

		public object[] MergeArgumentsImage(CUdeviceptr inputPointer, CUdeviceptr outputPointer, int width, int height, int channels, int bitdepth, object[] arguments, bool silent = false)
		{
			// Get kernel argument definitions
			Dictionary<string, Type> args = this.GetArguments(null, silent);

			// Create array for kernel arguments
			object[] kernelArgs = new object[args.Count];

			int pointersCount = 0;
			// Integrate invariables if name fits (contains)
			for (int i = 0; i < kernelArgs.Length; i++)
			{
				string name = args.ElementAt(i).Key;
				Type type = args.ElementAt(i).Value;

				if (pointersCount == 0 && type == typeof(IntPtr))
				{
					kernelArgs[i] = inputPointer;
					pointersCount++;

					if (!silent)
					{
						CudaLogger.Log($"In-pointer: <{inputPointer}>");
					}
				}
				else if (pointersCount == 1 && type == typeof(IntPtr))
				{
					kernelArgs[i] = outputPointer;
					pointersCount++;

					if (!silent)
					{
						CudaLogger.Log($"Out-pointer: <{outputPointer}>");
					}
				}
				else if (name.Contains("width") && type == typeof(int))
				{
					kernelArgs[i] = width;

					if (!silent)
					{
						CudaLogger.Log($"Width: [{width}]");
					}
				}
				else if (name.Contains("height") && type == typeof(int))
				{
					kernelArgs[i] = height;

					if (!silent)
					{
						CudaLogger.Log($"Height: [{height}]");
					}
				}
				else if (name.Contains("chan") && type == typeof(int))
				{
					kernelArgs[i] = channels;

					if (!silent)
					{
						CudaLogger.Log($"Channels: [{channels}]");
					}
				}
				else if (name.Contains("bit") && type == typeof(int))
				{
					kernelArgs[i] = bitdepth;

					if (!silent)
					{
						CudaLogger.Log($"Bits: [{bitdepth}]");
					}
				}
				else
				{
					// Check if argument is in arguments array
					for (int j = 0; j < arguments.Length; j++)
					{
						if (name == args.ElementAt(j).Key)
						{
							kernelArgs[i] = arguments[j];
							break;
						}
					}

					// If not found, set to 0
					if (kernelArgs[i] == null)
					{
						kernelArgs[i] = 0;
					}
				}
			}

			// DEBUG LOG
			//CudaLogger.Log("Kernel arguments: " + string.Join(", ", kernelArgs.Select(x => x.ToString())), "", 1);

			// Return kernel arguments
			return kernelArgs;
		}







		// Dispose
		public void Dispose()
		{
			GC.SuppressFinalize(this);
		}

	}
}

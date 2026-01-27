using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ManagedCuda;

namespace AsynCUDA12.Runtime
{
	public class CudaService : IDisposable
	{
		// Static CUDA properties
		public static int DeviceCount => CudaContext.GetDeviceCount();
		public static Dictionary<int, CudaDeviceProperties> AvailableDevicesProps => GetAvailableDevicesProperties();

		public static Version CudaDriverVersion => CudaContext.GetDriverVersion();

		public static bool SilenceLogging { get; set; } = false;

		// Instance Properties
		public int SelectedDeviceId { get; private set; } = -1;
		public CudaDeviceProperties? SelectedDeviceProperties => this[this.SelectedDeviceId];

		internal PrimaryContext? Context { get; private set; } = null;
		public bool Online => this.Context != null && this.SelectedDeviceId >= 0;

		public BindingList<string> DeviceEntries { get; private set; } = new BindingList<string>(
			GetAvailableDevicesProperties()
			.Select(kv => $"[{kv.Key}] {kv.Value.DeviceName} - {kv.Value.TotalGlobalMemory / (1024 * 1024)} MB")
			.ToList()
		);



		// Accessors
		public CudaMem? this[nint indexPointer] => this.Register?[indexPointer];
		public CudaMem? this[Guid id] => this.Register?[id];

		public long TotalAllocated => this.Register?.TotalAllocated ?? 0;
		public int RegisteredMemoryObjects => this.Register?.RegisteredMemoryObjects ?? 0;
		public int ThreadsActive => this.Register?.ThreadsActive ?? 0;
		public int ThreadsIdle => this.Register?.ThreadsIdle ?? 0;

		public int MaxThreads => this.Register?.MaxThreads ?? 0;

		private static readonly BindingList<long> EmptyMemorySizes = new();
		private static readonly BindingList<int> EmptyStreamThreads = new();
		public BindingList<long> MemorySizesList => this.Register?.MemorySizesList ?? EmptyMemorySizes;
		public BindingList<int> StreamThreadsList => this.Register?.StreamThreadsList ?? EmptyStreamThreads;


		// Fields
		internal CudaRegister? Register { get; private set; } = null;
		public CudaFourier? Fourier { get; private set; } = null;
		public CudaCompiler? Compiler { get; private set; } = null;
		public CudaLauncher? Launcher { get; private set; } = null;



		// Enumerables
		public CudaDeviceProperties? this[int deviceId] => GetAvailableDevicesProperties().GetValueOrDefault(deviceId);
		public CudaDeviceProperties? this[string deviceName] => GetAvailableDevicesProperties().Values.FirstOrDefault(p => p.DeviceName.Equals(deviceName, StringComparison.OrdinalIgnoreCase));

		// Ctor
		public CudaService(int preferredDeviceIndex = -1)
		{
			if (preferredDeviceIndex >= 0)
			{
				this.Initialize(preferredDeviceIndex);
			}
		}

		public CudaService(string preferredDeviceName)
		{
			if (!string.IsNullOrWhiteSpace(preferredDeviceName))
			{
				this.Initialize(preferredDeviceName);
			}
		}



		// Methods (static)
		public static Dictionary<int, CudaDeviceProperties> GetAvailableDevicesProperties()
		{
			var deviceProps = new Dictionary<int, CudaDeviceProperties>();
			int deviceCount = DeviceCount;
			for (int i = 0; i < deviceCount; i++)
			{
				using var context = new CudaContext(i);
				var props = context.GetDeviceInfo();
				deviceProps[i] = props;
			}
			return deviceProps;
		}


		// Methods (instance)
		public void Dispose()
		{
			if (this.Context != null)
			{
				this.Context.Dispose();
				this.Context = null;

				CudaLogger.Log("CudaService: Disposed CUDA context");
			}

			// Dispose objects
			this.Launcher?.Dispose();
			this.Launcher = null;
			CudaLogger.Log("CudaService: Disposed Launcher");
			this.Compiler?.Dispose();
			this.Compiler = null;
			CudaLogger.Log("CudaService: Disposed Compiler");
			this.Fourier?.Dispose();
			this.Fourier = null;
			CudaLogger.Log("CudaService: Disposed Fourier");
			this.Register?.Dispose();
			this.Register = null;
			CudaLogger.Log("CudaService: Disposed Register");
			this.Register = null;

			this.SelectedDeviceId = -1;

			GC.SuppressFinalize(this);
		}

		public bool Initialize(int deviceId = -1)
		{
			if (deviceId < 0 || deviceId >= DeviceCount || DeviceCount <= 0)
			{
				if (deviceId >= DeviceCount)
				{
					CudaLogger.Log($"CudaService: Invalid device ID {deviceId} for initialization");
					return false;
				}

				if (DeviceCount <= 0)
				{
					CudaLogger.Log("CudaService: No CUDA devices available for initialization");
					return false;
				}

				this.Dispose();
				this.SelectedDeviceId = -1;
				CudaLogger.Log("CudaService: Disposed <offline>");
				return false;
			}

			try
			{
				if (this.Context != null)
				{
					CudaLogger.Log($"CudaService: Re-initializing from device ID {this.SelectedDeviceId} to device ID {deviceId}");
					this.Dispose();
				}

				this.Context = new PrimaryContext(deviceId);
				this.SelectedDeviceId = deviceId;
				// Initialize other objects
				this.Register = new CudaRegister(this.Context);
				this.Fourier = new CudaFourier(this.Context, this.Register);
				this.Compiler = new CudaCompiler(this.Context);
				this.Launcher = new CudaLauncher(this.Context, this.Register, this.Fourier, this.Compiler);
				CudaLogger.Log($"CudaService: Initialized on device ID {deviceId} ({this.SelectedDeviceProperties?.DeviceName})");
			}
			catch (Exception ex)
			{
				CudaLogger.Log($"CudaService: Failed to initialize on device ID {deviceId}", ex);
				this.Dispose();
				return false;
			}

			return true;
		}

		public bool Initialize(string name, bool exactMatch = false)
		{
			int index = -1;

			// Try parse name as single int id
			if (int.TryParse(name, out index))
			{
				CudaLogger.Log("Name string was id");
				return this.Initialize(index);
			}

			if (exactMatch)
			{
				var deviceEntry = AvailableDevicesProps.FirstOrDefault(
					kv => kv.Value.DeviceName.Equals(name, StringComparison.OrdinalIgnoreCase));
				if (deviceEntry.Value != null)
				{
					index = deviceEntry.Key;
				}
				else
				{
					CudaLogger.Log($"CudaService: No device found with exact name '{name}'");
					return false;
				}
			}
			else
			{
				// Match by contains & ignore case
				var deviceEntry = AvailableDevicesProps.FirstOrDefault(
					kv => kv.Value.DeviceName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
				if (deviceEntry.Value != null)
				{
					index = deviceEntry.Key;
				}
				else
				{
					CudaLogger.Log($"CudaService: No device found containing name '{name}'");
					return false;
				}
			}

			return this.Initialize(index);
		}



		// Accessors (single)
		public CudaMem? PushData<T>(IEnumerable<T> data) where T : unmanaged
		{
			if (!this.Online || this.Context == null || this.Register == null)
			{
				CudaLogger.Log("CudaService: Cannot push data - service is offline");
				return null;
			}

			return this.Register.PushData(data);
		}

		public T[]? PullData<T>(nint indexPointer, bool keepBuffer = false) where T : unmanaged
		{
			if (!this.Online || this.Context == null || this.Register == null)
			{
				CudaLogger.Log("CudaService: Cannot pull data - service is offline");
				return null;
			}
			return this.Register.PullData<T>(indexPointer, keepBuffer);
		}

		public T[]? PullData<T>(CudaMem mem, bool keepBuffer = false) where T : unmanaged
			{
			if (!this.Online || this.Context == null || this.Register == null)
			{
				CudaLogger.Log("CudaService: Cannot pull data - service is offline");
				return null;
			}

			return this.Register.PullData<T>(mem.IndexPointer, keepBuffer);
		}	

		public CudaMem? AllocateSingle<T>(int elementCount) where T : unmanaged
		{
			if (!this.Online || this.Context == null || this.Register == null)
			{
				CudaLogger.Log("CudaService: Cannot allocate memory - service is offline");
				return null;
			}
			return this.Register.AllocateSingle<T>(elementCount);
		}


		// Accessors (group / chunks)
		public CudaMem? PushChunks<T>(IEnumerable<T[]> data) where T : unmanaged
		{
			if (!this.Online || this.Context == null || this.Register == null)
			{
				CudaLogger.Log("CudaService: Cannot push data - service is offline");
				return null;
			}

			return this.Register.PushChunks(data);
		}

		public IEnumerable<T[]>? PullChunks<T>(nint indexPointer, bool keepBuffer = false) where T : unmanaged
		{
			if (!this.Online || this.Context == null || this.Register == null)
			{
				CudaLogger.Log("CudaService: Cannot pull data - service is offline");
				return null;
			}
			return this.Register.PullChunks<T>(indexPointer, keepBuffer);
		}

		public IEnumerable<T[]>? PullChunks<T>(CudaMem mem, bool keepBuffer = false) where T : unmanaged
		{
			if (!this.Online || this.Context == null || this.Register == null)
			{
				CudaLogger.Log("CudaService: Cannot pull data - service is offline");
				return null;
			}
			return this.Register.PullChunks<T>(mem.IndexPointer, keepBuffer);
		}

		public CudaMem? AllocateGroup<T>(nint[] lengths) where T : unmanaged
		{
			if (!this.Online || this.Context == null || this.Register == null)
			{
				CudaLogger.Log("CudaService: Cannot allocate memory - service is offline");
				return null;
			}
			return this.Register.AllocateGroup<T>(lengths);
		}


		// Accessors (single) ((async))
		public async Task<CudaMem?> PushDataAsync<T>(IEnumerable<T> data) where T : unmanaged
		{
			if (!this.Online || this.Context == null || this.Register == null)
			{
				CudaLogger.Log("CudaService: Cannot push data - service is offline");
				return null;
			}

			return await this.Register.PushDataAsync(data);
		}

		public async Task<T[]?> PullDataAsync<T>(nint indexPointer, bool keepBuffer = false) where T : unmanaged
		{
			if (!this.Online || this.Context == null || this.Register == null)
			{
				CudaLogger.Log("CudaService: Cannot pull data - service is offline");
				return null;
			}
			return await this.Register.PullDataAsync<T>(indexPointer, keepBuffer);
		}

		public async Task<T[]?> PullDataAsync<T>(CudaMem cudaMem, bool keepBuffer = false) where T : unmanaged
		{
			if (!this.Online || this.Context == null || this.Register == null)
			{
				CudaLogger.Log("CudaService: Cannot pull data - service is offline");
				return null;
			}
			return await this.Register.PullDataAsync<T>(cudaMem.IndexPointer, keepBuffer);
		}

		public async Task<CudaMem?> AllocateSingleAsync<T>(int elementCount) where T : unmanaged
		{
			if (!this.Online || this.Context == null || this.Register == null)
			{
				CudaLogger.Log("CudaService: Cannot allocate memory - service is offline");
				return null;
			}
			return await this.Register.AllocateSingleAsync<T>(elementCount);
		}


		// Accessors (group / chunks) ((async))
		public async Task<CudaMem?> PushChunksAsync<T>(IEnumerable<T[]> data) where T : unmanaged
		{
			if (!this.Online || this.Context == null || this.Register == null)
			{
				CudaLogger.Log("CudaService: Cannot push data - service is offline");
				return null;
			}
			return await this.Register.PushChunksAsync(data);
		}

		public async Task<IEnumerable<T[]>?> PullChunksAsync<T>(nint indexPointer, bool keepBuffer = false) where T : unmanaged
		{
			if (!this.Online || this.Context == null || this.Register == null)
			{
				CudaLogger.Log("CudaService: Cannot pull data - service is offline");
				return null;
			}
			return await this.Register.PullChunksAsync<T>(indexPointer, keepBuffer);
		}

		public async Task<IEnumerable<T[]>?> PullChunksAsync<T>(CudaMem mem, bool keepBuffer = false) where T : unmanaged
		{
			if (!this.Online || this.Context == null || this.Register == null)
			{
				CudaLogger.Log("CudaService: Cannot pull data - service is offline");
				return null;
			}
			return await this.Register.PullChunksAsync<T>(mem.IndexPointer, keepBuffer);
		}

		public async Task<CudaMem?> AllocateGroupAsync<T>(nint[] lengths) where T : unmanaged
		{
			if (!this.Online || this.Context == null || this.Register == null)
			{
				CudaLogger.Log("CudaService: Cannot allocate memory - service is offline");
				return null;
			}
			return await this.Register.AllocateGroupAsync<T>(lengths);
		}


		// Accessors (free)
		public long FreeMemory(CudaMem mem)
		{
			if (!this.Online || this.Context == null || this.Register == null)
			{
				CudaLogger.Log("CudaService: Cannot free memory - service is offline");
				return 0;
			}
			return this.Register.FreeMemory(mem);
		}

		public long FreeMemory(nint indexPointer)
		{
			if (!this.Online || this.Context == null || this.Register == null)
			{
				CudaLogger.Log("CudaService: Cannot free memory - service is offline");
				return 0;
			}
			return this.Register.FreeMemory(indexPointer);
		}

		public long FreeMemory(Guid id)
		{
			if (!this.Online || this.Context == null || this.Register == null)
			{
				CudaLogger.Log("CudaService: Cannot free memory - service is offline");
				return 0;
			}
			return this.Register.FreeMemory(id);
		}

		public async Task<long> FreeMemoryAsync(Guid id)
		{
			if (!this.Online || this.Context == null || this.Register == null)
			{
				CudaLogger.Log("CudaService: Cannot free memory - service is offline");
				return 0;
			}
			return await Task.Run(() => this.Register.FreeMemory(id));
		}

		public async Task<long> FreeMemoryAsync(nint indexPointer)
		{
			if (!this.Online || this.Context == null || this.Register == null)
			{
				CudaLogger.Log("CudaService: Cannot free memory - service is offline");
				return 0;
			}
			return await Task.Run(() => this.Register.FreeMemory(indexPointer));
		}

	}
}

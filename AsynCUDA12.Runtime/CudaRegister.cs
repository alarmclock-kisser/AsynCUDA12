using ManagedCuda;
using ManagedCuda.BasicTypes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsynCUDA12.Runtime
{
	internal class CudaRegister : IDisposable
	{
		// Fields
		private readonly ConcurrentDictionary<Guid, CudaMem> Memory = [];
		public readonly BindingList<CudaMem> MemoryList = [];
		private readonly BindingList<long> _memorySizes = [];

		internal readonly ConcurrentDictionary<CudaStream, int> Streams = [];
		private readonly BindingList<int> _streamThreads = [];

		private readonly PrimaryContext Context;


		// Properties
		public long TotalAllocated => this.Memory.Values.Sum(m => m.TotalSize);
		public int RegisteredMemoryObjects => this.Memory.Count;
		public int ThreadsActive => this.Streams.Count(s => s.Value > 0);
		public int ThreadsIdle => this.Streams.Count(s => s.Value <= 0);

		public int MaxThreads => this.Context.GetDeviceInfo().MaxThreadsPerMultiProcessor;

		public BindingList<long> MemorySizesList => this._memorySizes;
		public BindingList<int> StreamThreadsList => this._streamThreads;




		private void ReleaseStream(CudaStream stream, bool removeWhenIdle = true)
		{
			if (this.Streams.TryGetValue(stream, out int value))
			{
				if (value > 0)
				{
					this.Streams[stream] = value - 1;
				}

				if (removeWhenIdle && this.Streams.TryGetValue(stream, out int updated) && updated <= 0)
				{
					this.Streams.TryRemove(stream, out _);
					stream.Dispose();
				}
			}

			// Clean up any idle streams that remain at <= 0
			foreach (var idle in this.Streams.Where(kv => kv.Value <= 0).Select(kv => kv.Key).ToList())
			{
				if (this.Streams.TryRemove(idle, out _))
				{
					idle.Dispose();
				}
			}

			this.RefreshStreamThreads();
		}

		private void AddMemorySize(long size)
		{
			lock (this._memorySizes)
			{
				this._memorySizes.Add(size);
			}
		}

		private void RefreshStreamThreads()
		{
			lock (this._streamThreads)
			{
				this._streamThreads.RaiseListChangedEvents = false;
				this._streamThreads.Clear();
				foreach (var v in this.Streams.Values)
				{
					this._streamThreads.Add(v);
				}
				this._streamThreads.RaiseListChangedEvents = true;
				this._streamThreads.ResetBindings();
			}
		}


		// Enumerables
		public CudaMem? this[nint indexPointer] => this.Memory.Values.FirstOrDefault(m => m.Pointers.Contains(indexPointer));
		public CudaMem? this[Guid id] => this.Memory.ContainsKey(id) ? this.Memory[id] : null;
		internal CudaStream? this[ulong id] => this.GetStream(id);


		// Ctor
		public CudaRegister(PrimaryContext ctx)
		{
			this.Context = ctx;

			this.Context.SetCurrent();
		}



		// Methods (Free)
		public long FreeMemory(CudaMem mem)
		{
			long freed = mem.TotalSize;

			CUdeviceptr[] devicePointers = mem.Pointers.Select(p => new CUdeviceptr(p)).ToArray();
			foreach (var devicePointer in devicePointers)
			{
				try
				{
					this.Context.FreeMemory(devicePointer);
				}
				catch (Exception ex)
				{
					CudaLogger.Log("Error freeing memory", ex);
				}
			}

			if (this.Memory.TryRemove(mem.Id, out _))
			{
				lock (this._memorySizes)
				{
					this._memorySizes.Remove(mem.TotalSize);
				}
				mem.Dispose();
			}
			else
			{
				freed *= -1;
			}

			return freed;
		}

		public long FreeMemory(IntPtr indexPointer)
		{
			CudaMem? mem = this[indexPointer];
			if (mem == null)
			{
				return 0;
			}

			long freed = mem.TotalSize;

			CUdeviceptr[] devicePointers = mem.Pointers.Select(p => new CUdeviceptr(p)).ToArray();
			foreach (var devicePointer in devicePointers)
			{
				try
				{
					this.Context.FreeMemory(devicePointer);
				}
				catch (Exception ex)
				{
					CudaLogger.Log(ex, "Error freeing memory");
				}
			}

			if (this.Memory.TryRemove(mem.Id, out _))
			{
				lock (this._memorySizes)
				{
					this._memorySizes.Remove(mem.TotalSize);
				}
				mem.Dispose();
			}
			else
			{
				freed *= -1;
			}

			return freed;
		}

		public long FreeMemory(Guid id)
		{
			CudaMem? mem = this[id];
			if (mem == null)
			{
				return 0;
			}

			long freed = mem.TotalSize;

			CUdeviceptr[] devicePointers = mem.Pointers.Select(p => new CUdeviceptr(p)).ToArray();
			foreach (var devicePointer in devicePointers)
			{
				try
				{
					this.Context.FreeMemory(devicePointer);
				}
				catch (Exception ex)
				{
					CudaLogger.Log(ex, "Error freeing memory");
				}
			}

			if (this.Memory.TryRemove(mem.Id, out _))
			{
				lock (this._memorySizes)
				{
					this._memorySizes.Remove(mem.TotalSize);
				}
				mem.Dispose();
			}
			else
			{
				freed *= -1;
			}

			return freed;
		}


		// Methods (Streams)
		internal ulong? CreateStream()
		{
			Guid id = Guid.Empty;

			try
			{
				this.Context.SetCurrent();
				CudaStream stream = new();
				stream.Synchronize();

				id = Guid.NewGuid();
				if (this.Streams.TryAdd(stream, 0))
				{
					this.RefreshStreamThreads();
					return stream.ID;
				}
				else
				{
					stream.Dispose();
					return null;
				}
			}
			catch (Exception ex)
			{
				CudaLogger.Log(ex, "Error creating stream");
				return null;
			}
		}

		public CudaStream? GetStream(ulong? id = null)
		{
			int engines = this.Context.GetDeviceInfo().AsyncEngineCount;
			int streams = this.Streams.Count;

			ulong? streamId = null;
			CudaStream? stream = null;

			// Finds the stream by ID if provided, or returns stream with lowest value, or creates a new stream if no ID is provided
			if (id.HasValue)
			{
				stream = this.Streams.Keys.FirstOrDefault(s => s.ID == id.Value);
			}
			else
			{
				if (streams < engines)
				{
					streamId = this.CreateStream();
					if (streamId.HasValue)
					{
						stream = this.Streams.Keys.FirstOrDefault(s => s.ID == streamId.Value);
					}
				}
				else
				{
					// Select stream with the lowest value
					stream = this.Streams.Keys.OrderBy(s => this.Streams[s]).FirstOrDefault();
				}
			}

			if (stream == null)
			{
				CudaLogger.Log("No available stream found or created.", $"{(id.HasValue ? $"ID was {id}" : "")}");
			}

			return stream;
		}

		public IEnumerable<CudaStream>? GetManyStreams(int maxCount = 0, IEnumerable<ulong>? ids = null)
		{
			if (maxCount <= 0)
			{
				maxCount = this.MaxThreads - this.Streams.Count();
			}
			if (maxCount <= 0 || this.Context == null)
			{
				return null;
			}

			CudaStream[] created = new CudaStream[maxCount];
			var results = created.Select((s, i) =>
			{
				CudaStream? stream = ids != null && ids.Any() ? this.GetStream(ids.ElementAt(i)) : this.GetStream();
				if (stream == null)
				{
					return null;
				}
				created[i] = stream;
				return stream;
			});

			foreach (var s in results)
			{
				if (s != null && !this.Streams.ContainsKey(s))
				{
					if (this.Streams.TryAdd(s, 0))
					{
						s.Synchronize();
					}
					else
					{
						s.Dispose();
					}
				}
			}

			if (created.Any(s => s == null))
			{
				CudaLogger.Log("Some streams could not be created or retrieved.");
				return null;
			}

			this.RefreshStreamThreads();

			return created;
		}

		public async Task<IEnumerable<CudaStream>?> GetManyStreamsAsync(int maxCount = 0, IEnumerable<ulong>? ids = null)
		{
			if (maxCount <= 0)
			{
				maxCount = this.MaxThreads - this.Streams.Count();
			}
			if (maxCount <= 0 || this.Context == null)
			{
				return null;
			}

			CudaStream[] created = new CudaStream[maxCount];
			var results = created.Select((s, i) =>
			{
				CudaStream? stream = ids != null && ids.Any() ? this.GetStream(ids.ElementAt(i)) : this.GetStream();
				if (stream == null)
				{
					return null;
				}

				created[i] = stream;
				return stream;
			});

			int added = 0;
			foreach (var s in results)
			{
				if (s != null && !this.Streams.ContainsKey(s))
				{
					if (this.Streams.TryAdd(s, 0))
					{
						await Task.Run(s.Synchronize);
						added++;
					}
					else
					{
						s.Dispose();
					}
				}
			}

			if (added == 0)
			{
				CudaLogger.Log("No streams could be created or retrieved.");
				return null;
			}

			this.RefreshStreamThreads();

			return created;
		}


		// Methods (Allocate)
		public CudaMem? AllocateSingle<T>(IntPtr length) where T : unmanaged
		{
			if (length <= 0)
			{
				return null;
			}

			try
			{
				var stream = this.GetStream();
				if (stream != null)
				{
					this.Streams[stream] = this.Streams.GetValueOrDefault(stream, 0) + 1;
					this.RefreshStreamThreads();
				}

				CudaDeviceVariable<T> devVariable = new((long) length);
				var pointer = devVariable.DevicePointer;
				CudaMem mem = new(pointer, length, typeof(T));
				if (this.Memory.TryAdd(mem.Id, mem))
				{
					this.AddMemorySize(mem.TotalSize);
					return mem;
				}
				else
				{
					devVariable.Dispose();
					mem.Dispose();
					CudaLogger.Log($"Failed to allocate memory for {typeof(T).Name} of length {length}.");
					return null;
				}
			}
			catch (Exception ex)
			{
				CudaLogger.Log(ex, $"Error allocating single memory");
				return null;
			}
		}

		public async Task<CudaMem?> AllocateSingleAsync<T>(IntPtr length) where T : unmanaged
		{
			if (length <= 0)
			{
				return null;
			}

			if (this.Context == null)
			{
				CudaLogger.Log("Error allocating single memory (async): context is null");
				return null;
			}

			try
			{
				return await Task.Run(() =>
				{
					try
					{
						this.Context.SetCurrent();
						CudaDeviceVariable<T> devVariable = new((long) length);
						var pointer = devVariable.DevicePointer;
						CudaMem mem = new(pointer, length, typeof(T));
						if (this.Memory.TryAdd(mem.Id, mem))
						{
							this.AddMemorySize(mem.TotalSize);
							return mem;
						}
						else
						{
							devVariable.Dispose();
							mem.Dispose();
							CudaLogger.Log($"Failed to allocate memory for {typeof(T).Name} of length {length}.");
							return null;
						}
					}
					catch (Exception ex)
					{
						CudaLogger.Log(ex, $"Error allocating single memory (async)");
						return null;
					}
				});
			}
			catch
			{
				return null;
			}
			finally
			{
				// Ensure no lingering stream entries affect ThreadsActive in async path
				if (!this.Streams.IsEmpty)
				{
					foreach (var key in this.Streams.Keys.ToList())
					{
						this.ReleaseStream(key);
					}
				}
			}

		}

		public CudaMem? AllocateGroup<T>(IntPtr[] lengths) where T : unmanaged
		{
			if (lengths.LongLength <= 0 || lengths.Any(l => l <= 0))
			{
				return null;
			}

			CudaStream? stream = null;
			try
			{
				stream = this.GetStream();
				if (stream != null)
				{
					this.Streams[stream] = this.Streams.GetValueOrDefault(stream, 0) + 1;
					this.RefreshStreamThreads();
				}

				CudaDeviceVariable<T>[] devVariables = lengths.Select(l => new CudaDeviceVariable<T>((long) l)).ToArray();
				var pointers = devVariables.Select(v => v.DevicePointer).ToArray();
				CudaMem mem = new(pointers, lengths, typeof(T));
				if (this.Memory.TryAdd(mem.Id, mem))
				{
					this.AddMemorySize(mem.TotalSize);
					return mem;
				}
				else
				{
					foreach (var devVariable in devVariables)
					{
						devVariable.Dispose();
					}
					mem.Dispose();
					CudaLogger.Log($"Failed to allocate grouped memory for {typeof(T).Name} with lengths {(lengths.LongLength + "x " + lengths.FirstOrDefault())}.");
					return null;
				}
			}
			catch (Exception ex)
			{
				CudaLogger.Log(ex, "Error allocating grouped memory");
				return null;
			}
			finally
			{
				if (stream != null)
				{
					this.ReleaseStream(stream);
				}
			}
		}

		public async Task<CudaMem?> AllocateGroupAsync<T>(IntPtr[] lengths) where T : unmanaged
		{
			if (lengths.LongLength <= 0 || lengths.Any(l => l <= 0))
			{
				return null;
			}
			if (this.Context == null)
			{
				CudaLogger.Log("Error allocating grouped memory (async): context is null");
				return null;
			}

			try
			{
				return await Task.Run(() =>
				{
					try
					{
						this.Context.SetCurrent();
						CudaDeviceVariable<T>[] devVariables = lengths.Select(l => new CudaDeviceVariable<T>((long) l)).ToArray();
						var pointers = devVariables.Select(v => v.DevicePointer).ToArray();

						CudaMem mem = new(pointers, lengths, typeof(T));

						if (this.Memory.TryAdd(mem.Id, mem))
						{
							this.AddMemorySize(mem.TotalSize);
							return mem;
						}
						else
						{
							foreach (var devVariable in devVariables)
							{
								devVariable.Dispose();
							}
							mem.Dispose();
							CudaLogger.Log($"Failed to allocate grouped memory for {typeof(T).Name} with lengths {(lengths.LongLength + "x " + lengths.FirstOrDefault())}");
							return null;
						}
					}
					catch (Exception ex)
					{
						CudaLogger.Log(ex, "Error allocating grouped memory (async)");
						return null;
					}
				});
			}
			catch
			{
				return null;
			}
			finally
			{
				if (!this.Streams.IsEmpty)
				{
					foreach (var key in this.Streams.Keys.ToList())
					{
						this.ReleaseStream(key);
					}
				}
			}
		}


		// Methods (Push)
		public CudaMem? PushData<T>(IEnumerable<T> data) where T : unmanaged
		{
			if (data == null || !data.Any())
			{
				return null;
			}

			CudaStream? stream = null;
			try
			{
				stream = this.GetStream();
				if (stream != null)
				{
					this.Streams[stream] = this.Streams.GetValueOrDefault(stream, 0) + 1;
					this.RefreshStreamThreads();
				}

				IntPtr length = (nint) data.LongCount();
				CudaDeviceVariable<T> devVariable = new(length);
				var pointer = devVariable.DevicePointer;

				this.Context.CopyToDevice(devVariable.DevicePointer, data.ToArray());

				CudaMem mem = new(pointer, length, typeof(T));

				if (this.Memory.TryAdd(mem.Id, mem))
				{
					this.AddMemorySize(mem.TotalSize);
					return mem;
				}
				else
				{
					devVariable.Dispose();
					mem.Dispose();
					CudaLogger.Log($"Failed to push data for {typeof(T).Name} of length {length}.");
					return null;
				}
			}
			catch (Exception ex)
			{
				CudaLogger.Log(ex, "Error pushing data");
				return null;
			}
			finally
			{
				if (stream != null)
				{
					this.ReleaseStream(stream);
				}
			}
		}

		public CudaMem? PushChunks<T>(IEnumerable<IEnumerable<T>> chunks) where T : unmanaged
		{
			if (chunks == null || !chunks.Any())
			{
				return null;
			}

			CudaStream? stream = null;
			try
			{
				stream = this.GetStream();
				if (stream != null)
				{
					this.Streams[stream] = this.Streams.GetValueOrDefault(stream, 0) + 1;
					this.RefreshStreamThreads();
				}

				IntPtr[] lengths = chunks.Select(chunk => (nint) chunk.LongCount()).ToArray();
				CudaDeviceVariable<T>[] devVariables = chunks.Select(chunk => new CudaDeviceVariable<T>((nint) chunk.LongCount())).ToArray();
				var pointers = devVariables.Select(v => v.DevicePointer).ToArray();

				for (int i = 0; i < chunks.Count(); i++)
				{
					this.Context.CopyToDevice(devVariables[i].DevicePointer, chunks.ElementAt(i).ToArray());
				}

				CudaMem mem = new(pointers, lengths, typeof(T));

				if (this.Memory.TryAdd(mem.Id, mem))
				{
					this.AddMemorySize(mem.TotalSize);
					return mem;
				}
				else
				{
					foreach (var devVariable in devVariables)
					{
						devVariable.Dispose();
					}
					mem.Dispose();
					CudaLogger.Log($"Failed to push chunks for {typeof(T).Name} with lengths {(lengths.LongLength + "x " + lengths.FirstOrDefault())}.");
					return null;
				}
			}
			catch (Exception ex)
			{
				CudaLogger.Log(ex, "Error pushing chunks");
				return null;
			}
			finally
			{
				if (stream != null)
				{
					this.ReleaseStream(stream);
				}
			}
		}

		public async Task<CudaMem?> PushDataAsync<T>(IEnumerable<T> data, ulong? id = null) where T : unmanaged
		{
			CudaMem? mem = null;

			var stream = this.GetStream(id);
			if (stream == null)
			{
				return null;
			}

			this.Streams[stream] = this.Streams.GetValueOrDefault(stream, 0) + 1;
			this.RefreshStreamThreads();

			try
			{
				IntPtr length = (nint) data.LongCount();
				CudaDeviceVariable<T> devVariable = new(length, stream);
				var pointer = devVariable.DevicePointer;

				devVariable.AsyncCopyToDevice(data.ToArray(), stream);
				await Task.Run(stream.Synchronize);

				mem = new(pointer, length, typeof(T));
				if (!this.Memory.TryAdd(mem.Id, mem))
				{
					devVariable.Dispose();
					mem.Dispose();
					CudaLogger.Log($"Failed to push data for {typeof(T).Name} of length {length}.");
					mem = null;
				}
				else
				{
					this.AddMemorySize(mem.TotalSize);
				}
			}
			catch (Exception ex)
			{
				CudaLogger.Log(ex, $"Error pushing data (async)");
			}
			finally
			{
				this.ReleaseStream(stream);
				GC.Collect();
			}

			return mem;
		}

		public async Task<CudaMem?> PushChunksAsync<T>(IEnumerable<IEnumerable<T>> chunks, ulong? id = null) where T : unmanaged
		{
			CudaMem? mem = null;
			var stream = this.GetStream(id);
			if (stream == null)
			{
				return null;
			}
			this.Streams[stream] = this.Streams.GetValueOrDefault(stream, 0) + 1;
			this.RefreshStreamThreads();

			try
			{
				IntPtr[] lengths = chunks.Select(chunk => (nint) chunk.LongCount()).ToArray();
				CudaDeviceVariable<T>[] devVariables = chunks.Select(chunk => new CudaDeviceVariable<T>((nint) chunk.LongCount(), stream)).ToArray();
				var pointers = devVariables.Select(v => v.DevicePointer).ToArray();

				for (int i = 0; i < chunks.Count(); i++)
				{
					devVariables[i].AsyncCopyToDevice(chunks.ElementAt(i).ToArray(), stream);
				}

				await Task.Run(stream.Synchronize);

				mem = new(pointers, lengths, typeof(T));
				if (!this.Memory.TryAdd(mem.Id, mem))
				{
					foreach (var devVariable in devVariables)
					{
						devVariable.Dispose();
					}

					mem.Dispose();
					CudaLogger.Log($"Failed to push chunks for {typeof(T).Name} with lengths {(lengths.LongLength + "x " + lengths.FirstOrDefault())}.");
					mem = null;
				}
				else
				{
					this.AddMemorySize(mem.TotalSize);
				}
			}
			catch (Exception ex)
			{
				CudaLogger.Log(ex, $"Error pushing chunks (async)");
			}
			finally
			{
				this.ReleaseStream(stream);
				GC.Collect();
			}

			return mem;
		}


		// Methods (Pull)
		public T[] PullData<T>(IntPtr indexPointer, bool keep = false) where T : unmanaged
		{
			CudaMem? mem = this[indexPointer];

			if (mem == null || mem.Pointers.Length == 0 || mem.Lengths.Length == 0)
			{
				return [];
			}

			try
			{
				CUdeviceptr devicePointer = new(mem.IndexPointer);
				CudaDeviceVariable<T> devVariable = new(devicePointer, mem.IndexLength);
				T[] data = new T[mem.IndexLength];

				this.Context.CopyToHost(data, devVariable.DevicePointer);

				// this.Context.Synchronize();

				if (!keep)
				{
					this.FreeMemory(mem);
				}

				return data;
			}
			catch (Exception ex)
			{
				CudaLogger.Log(ex, "Error pulling data");
				return [];
			}
		}

		public List<T[]> PullChunks<T>(IntPtr indexPointer, bool keep = false) where T : unmanaged
		{
			CudaMem? mem = this[indexPointer];

			if (mem == null || mem.Pointers.Length == 0 || mem.Lengths.Length == 0)
			{
				return [];
			}

			try
			{
				List<T[]> chunks = [];
				CUdeviceptr[] devicePointers = mem.Pointers.Select(p => new CUdeviceptr(p)).ToArray();
				CudaDeviceVariable<T>[] devVariables = devicePointers.Select((ptr, i) => new CudaDeviceVariable<T>(ptr, mem.Lengths[i])).ToArray();

				for (int i = 0; i < devVariables.Length; i++)
				{
					T[] chunkData = new T[mem.Lengths[i]];
					this.Context.CopyToHost(chunkData, devVariables[i].DevicePointer);
					chunks.Add(chunkData);
				}

				this.Context.Synchronize();

				if (!keep)
				{
					this.FreeMemory(mem);
				}

				return chunks;
			}
			catch (Exception ex)
			{
				CudaLogger.Log(ex, "Error pulling chunks");
				return [];
			}
		}

		public async Task<T[]> PullDataAsync<T>(IntPtr indexPointer, bool keep = false, ulong? id = null) where T : unmanaged
		{
			CudaMem? mem = this[indexPointer];
			if (mem == null || mem.Count <= 0)
			{
				return [];
			}

			try
			{
				int length = (int) mem.IndexLength;
				T[] data = new T[length];
				var stream = this.GetStream(id);
				if (stream == null)
				{
					return data;
				}
				this.Streams[stream] = this.Streams.GetValueOrDefault(stream, 0) + 1;
				this.RefreshStreamThreads();

				// this.Context.SetCurrent();
				this.Context.SetCurrent();

				CUdeviceptr devicePtr = new(mem.IndexPointer);

				// Native asynchroner Transfer: Device → Host
				int byteSize = data.Length * System.Runtime.InteropServices.Marshal.SizeOf<T>();
				unsafe
				{
					fixed (T* pData = data)
					{
						var res = ManagedCuda.DriverAPINativeMethods.AsynchronousMemcpy_v2.cuMemcpyAsync(
							new CUdeviceptr((IntPtr) pData),
							devicePtr,
							(SizeT) byteSize,
							stream.Stream
						);
						if (res != CUResult.Success)
						{
							throw new CudaException(res);
						}
					}
				}

				await Task.Run(stream.Synchronize);

				this.ReleaseStream(stream);

				if (!keep)
				{
					this.FreeMemory(mem);
				}

				return data;
			}
			catch (Exception ex)
			{
				CudaLogger.Log(ex, "Error pulling data (async)");
				return [];
			}
			finally
			{
				GC.Collect();
			}
		}

		public async Task<List<T[]>> PullChunksAsync<T>(IntPtr indexPointer, bool keep = false, ulong? id = null) where T : unmanaged
		{
			CudaMem? mem = this[indexPointer];
			if (mem == null || mem.Count <= 0)
			{
				return [];
			}

			try
			{
				List<T[]> chunks = [];
				var stream = this.GetStream(id);
				if (stream == null)
				{
					CudaLogger.Log("No stream available for async pull, falling back to synchronous copy.");
					return this.PullChunks<T>(indexPointer, keep);
				}
				this.Streams[stream] = this.Streams.GetValueOrDefault(stream, 0) + 1;
				this.RefreshStreamThreads();

				// this.Context.SetCurrent();

				CUdeviceptr[] devicePointers = mem.Pointers.Select(p => new CUdeviceptr(p)).ToArray();
				for (int i = 0; i < devicePointers.Length; i++)
				{
					T[] data = new T[mem.Lengths[i]];
					CUdeviceptr devicePtr = devicePointers[i];

					// Native asynchroner Transfer: Device → Host
					int byteSize = data.Length * System.Runtime.InteropServices.Marshal.SizeOf<T>();
					unsafe
					{
						fixed (T* pData = data)
						{
							var res = ManagedCuda.DriverAPINativeMethods.AsynchronousMemcpy_v2.cuMemcpyAsync(
								new CUdeviceptr((IntPtr) pData),
								devicePtr,
								(SizeT) byteSize,
								stream.Stream
							);
							if (res != CUResult.Success)
							{
								throw new CudaException(res);
							}
						}
					}

					chunks.Add(data);
				}

				await Task.Run(stream.Synchronize);

				this.ReleaseStream(stream);

				if (!keep)
				{
					this.FreeMemory(mem);
				}

				return chunks;
			}
			catch (Exception ex)
			{
				CudaLogger.Log(ex, "Error pulling chunks (async)");
				return [];
			}
			finally
			{
				GC.Collect();
			}
		}


		// Methods (Statistics)







		// Dispose
		public void Dispose()
		{
			foreach (var stream in this.Streams.Keys)
			{
				try
				{
					stream.Dispose();
				}
				catch
				{
				}
			}
			this.Streams.Clear();
			lock (this._streamThreads)
			{
				this._streamThreads.Clear();
			}
			lock (this._memorySizes)
			{
				this._memorySizes.Clear();
			}

			GC.SuppressFinalize(this);
		}
	}
}

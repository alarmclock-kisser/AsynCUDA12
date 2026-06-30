using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using OpenTK.Compute.OpenCL;

namespace AsynCUDA.OpenCl
{
    /// <summary>
    /// Central registry that owns the OpenCL context and command queue for a single selected device and
    /// tracks every <see cref="OpenClMem"/> allocation. Provides synchronous and asynchronous helpers for
    /// allocating, pushing, pulling and freeing device memory.
    /// </summary>
    public sealed class OpenClRegister : IDisposable
    {
        private readonly ConcurrentDictionary<Guid, OpenClMem> _allocations = new();
        private bool _disposed;

        /// <summary>
        /// Gets the OpenCL context used for all allocations.
        /// </summary>
        public CLContext Context { get; }

        /// <summary>
        /// Gets the command queue used for all transfers and kernel launches.
        /// </summary>
        public CLCommandQueue Queue { get; }

        /// <summary>
        /// Gets the device this registry operates on.
        /// </summary>
        public CLDevice Device { get; }

        /// <summary>
        /// Gets the number of currently tracked allocations.
        /// </summary>
        public int AllocationCount => this._allocations.Count;

        /// <summary>
        /// Gets the total number of bytes currently allocated across all tracked buffers.
        /// </summary>
        public long TotalAllocated
        {
            get
            {
                long total = 0;
                foreach (var mem in this._allocations.Values)
                {
                    total += mem.TotalSize;
                }

                return total;
            }
        }



        // Ctor
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenClRegister"/> class for the given context, queue and device.
        /// </summary>
        internal OpenClRegister(CLContext context, CLCommandQueue queue, CLDevice device)
        {
            this.Context = context;
            this.Queue = queue;
            this.Device = device;
        }



        // Allocation
        /// <summary>
        /// Allocates a single uninitialized device buffer holding <paramref name="length"/> elements of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The unmanaged element type.</typeparam>
        /// <param name="length">The number of elements to allocate.</param>
        /// <param name="flags">The OpenCL memory flags (defaults to read/write).</param>
        /// <returns>The created <see cref="OpenClMem"/>, or <c>null</c> if allocation failed.</returns>
        public OpenClMem? AllocateSingle<T>(long length, MemoryFlags flags = MemoryFlags.ReadWrite) where T : unmanaged
        {
            if (length <= 0)
            {
                OpenClLogger.LogError($"AllocateSingle: invalid length {length}.");
                return null;
            }

            int elementSize = Marshal.SizeOf<T>();
            UIntPtr size = new((ulong)(length * elementSize));

            CLBuffer buffer = CL.CreateBuffer(this.Context, flags, size, IntPtr.Zero, out CLResultCode result);
            if (result != CLResultCode.Success)
            {
                OpenClLogger.LogError($"AllocateSingle: CreateBuffer failed ({result}).");
                return null;
            }

            OpenClMem mem = new(buffer, length, typeof(T));
            this._allocations[mem.Id] = mem;
            return mem;
        }

        /// <summary>
        /// Allocates a single device buffer and copies <paramref name="data"/> into it.
        /// </summary>
        /// <typeparam name="T">The unmanaged element type.</typeparam>
        /// <param name="data">The host data to upload.</param>
        /// <param name="flags">The OpenCL memory flags (defaults to read/write).</param>
        /// <returns>The created <see cref="OpenClMem"/>, or <c>null</c> if allocation or upload failed.</returns>
        public OpenClMem? PushData<T>(T[] data, MemoryFlags flags = MemoryFlags.ReadWrite) where T : unmanaged
        {
            if (data == null || data.Length == 0)
            {
                OpenClLogger.LogError("PushData: data is null or empty.");
                return null;
            }

            OpenClMem? mem = this.AllocateSingle<T>(data.Length, flags);
            if (mem == null)
            {
                return null;
            }

            CLResultCode result = CL.EnqueueWriteBuffer(this.Queue, mem.IndexBuffer, true, UIntPtr.Zero, data, null, out _);
            if (result != CLResultCode.Success)
            {
                OpenClLogger.LogError($"PushData: EnqueueWriteBuffer failed ({result}).");
                this.Free(mem);
                return null;
            }

            return mem;
        }

        /// <summary>
        /// Reads the contents of a single-buffer allocation back into a host array.
        /// </summary>
        /// <typeparam name="T">The unmanaged element type.</typeparam>
        /// <param name="mem">The allocation to read from.</param>
        /// <returns>The host data, or <c>null</c> if the read failed.</returns>
        public T[]? PullData<T>(OpenClMem mem) where T : unmanaged
        {
            if (mem == null || mem.Count == 0)
            {
                OpenClLogger.LogError("PullData: allocation is null or empty.");
                return null;
            }

            T[] result = new T[mem.IndexLength];
            CLResultCode code = CL.EnqueueReadBuffer(this.Queue, mem.IndexBuffer, true, UIntPtr.Zero, result, null, out _);
            if (code != CLResultCode.Success)
            {
                OpenClLogger.LogError($"PullData: EnqueueReadBuffer failed ({code}).");
                return null;
            }

            return result;
        }

        /// <summary>
        /// Asynchronously allocates a buffer and uploads <paramref name="data"/> into it.
        /// </summary>
        public Task<OpenClMem?> PushDataAsync<T>(T[] data, MemoryFlags flags = MemoryFlags.ReadWrite) where T : unmanaged
        {
            return Task.Run(() => this.PushData(data, flags));
        }

        /// <summary>
        /// Asynchronously reads the contents of a single-buffer allocation back into a host array.
        /// </summary>
        public Task<T[]?> PullDataAsync<T>(OpenClMem mem) where T : unmanaged
        {
            return Task.Run(() => this.PullData<T>(mem));
        }



        // Bookkeeping
        /// <summary>
        /// Registers an externally created allocation so the registry tracks and disposes it.
        /// </summary>
        internal void Track(OpenClMem mem)
        {
            if (mem != null)
            {
                this._allocations[mem.Id] = mem;
            }
        }

        /// <summary>
        /// Determines whether the given allocation is tracked by this registry.
        /// </summary>
        public bool Contains(OpenClMem mem)
        {
            return mem != null && this._allocations.ContainsKey(mem.Id);
        }

        /// <summary>
        /// Frees a single tracked allocation and releases its device memory.
        /// </summary>
        /// <param name="mem">The allocation to free.</param>
        /// <returns><c>true</c> if the allocation was tracked and freed; otherwise <c>false</c>.</returns>
        public bool Free(OpenClMem mem)
        {
            if (mem == null)
            {
                return false;
            }

            if (this._allocations.TryRemove(mem.Id, out var tracked))
            {
                tracked.Dispose();
                return true;
            }

            // Not tracked, but still release to avoid leaks.
            mem.Dispose();
            return false;
        }

        /// <summary>
        /// Frees every tracked allocation.
        /// </summary>
        public void FreeAll()
        {
            foreach (var key in this._allocations.Keys)
            {
                if (this._allocations.TryRemove(key, out var mem))
                {
                    mem.Dispose();
                }
            }
        }



        // Disposal
        /// <summary>
        /// Frees all allocations and releases the command queue and context.
        /// </summary>
        public void Dispose()
        {
            if (this._disposed)
            {
                return;
            }

            this.FreeAll();

            try
            {
                CL.ReleaseCommandQueue(this.Queue);
            }
            catch
            {
            }

            try
            {
                CL.ReleaseContext(this.Context);
            }
            catch
            {
            }

            this._disposed = true;
        }
    }
}

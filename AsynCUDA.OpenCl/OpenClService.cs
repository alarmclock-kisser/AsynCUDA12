using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using OpenTK.Compute.OpenCL;

namespace AsynCUDA.OpenCl
{
    /// <summary>
    /// High-level facade for the OpenCL runtime and the OpenCL counterpart to the CUDA runtime service.
    /// Enumerates every available OpenCL device (across all platforms, including CPU devices), creates a
    /// context and command queue for a selected device, compiles the kernels in memory and exposes simple
    /// FFT/IFFT entry points that operate directly on <see cref="float"/> and <see cref="Vector2"/> arrays.
    /// </summary>
    public sealed class OpenClService : IDisposable
    {
        private OpenClRegister? _register;
        private OpenClCompiler? _compiler;
        private OpenClLauncher? _launcher;
        private OpenClFourier? _fourier;
        private bool _disposed;

        /// <summary>
        /// Gets all OpenCL devices available on the machine, each identified by a flat <see cref="OpenClDeviceInfo.Index"/>.
        /// </summary>
        public IReadOnlyList<OpenClDeviceInfo> AvailableDevices { get; }

        /// <summary>
        /// Gets the number of available OpenCL devices.
        /// </summary>
        public int DeviceCount => this.AvailableDevices.Count;

        /// <summary>
        /// Gets the flat index of the currently selected device, or <c>-1</c> when the service is offline.
        /// </summary>
        public int SelectedDeviceId { get; private set; } = -1;

        /// <summary>
        /// Gets the currently selected device info, or <c>null</c> when the service is offline.
        /// </summary>
        public OpenClDeviceInfo? SelectedDevice { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the service has an initialized device context.
        /// </summary>
        public bool Online => this._register != null && this.SelectedDeviceId >= 0;

        /// <summary>
        /// Gets the total number of bytes currently allocated on the device.
        /// </summary>
        public long TotalAllocated => this._register?.TotalAllocated ?? 0;

        /// <summary>
        /// Gets the FFT/IFFT helper, or <c>null</c> when the service is offline.
        /// </summary>
        public OpenClFourier? Fourier => this._fourier;

        /// <summary>
        /// Gets the memory registry, or <c>null</c> when the service is offline.
        /// </summary>
        public OpenClRegister? Register => this._register;

        /// <summary>
        /// Gets the in-memory kernel compiler, or <c>null</c> when the service is offline.
        /// </summary>
        public OpenClCompiler? Compiler => this._compiler;

        /// <summary>
        /// Gets the kernel launcher, or <c>null</c> when the service is offline.
        /// </summary>
        public OpenClLauncher? Launcher => this._launcher;



        // Ctor
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenClService"/> class, discovering all devices.
        /// Optionally initializes a preferred device immediately.
        /// </summary>
        /// <param name="preferredDeviceIndex">
        /// The flat device index to initialize, or <c>-1</c> to leave the service offline until
        /// <see cref="Initialize(int)"/> is called.
        /// </param>
        public OpenClService(int preferredDeviceIndex = -1)
        {
            this.AvailableDevices = OpenClDeviceInfo.DiscoverAll();

            if (this.AvailableDevices.Count == 0)
            {
                OpenClLogger.LogWarning("OpenClService: no OpenCL devices found on this machine.");
            }
            else
            {
                OpenClLogger.LogSuccess($"OpenClService: discovered {this.AvailableDevices.Count} OpenCL device(s).");
            }

            if (preferredDeviceIndex >= 0)
            {
                this.Initialize(preferredDeviceIndex);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenClService"/> class and initializes the first
        /// device whose name contains <paramref name="preferredDeviceName"/>.
        /// </summary>
        /// <param name="preferredDeviceName">A case-insensitive substring of the desired device name.</param>
        public OpenClService(string preferredDeviceName)
        {
            this.AvailableDevices = OpenClDeviceInfo.DiscoverAll();
            this.Initialize(preferredDeviceName);
        }



        // Initialization
        /// <summary>
        /// Initializes the context, command queue, compiler and launcher for the device at the given flat index.
        /// Any previously initialized device is disposed first.
        /// </summary>
        /// <param name="deviceIndex">The flat device index from <see cref="AvailableDevices"/>.</param>
        /// <returns><c>true</c> if the device was initialized successfully; otherwise <c>false</c>.</returns>
        public bool Initialize(int deviceIndex)
        {
            if (deviceIndex < 0 || deviceIndex >= this.AvailableDevices.Count)
            {
                OpenClLogger.LogError($"Initialize: device index {deviceIndex} is out of range (0..{this.AvailableDevices.Count - 1}).");
                return false;
            }

            this.Shutdown();

            OpenClDeviceInfo info = this.AvailableDevices[deviceIndex];

            CLContext context = CL.CreateContext(IntPtr.Zero, [info.Device], IntPtr.Zero, IntPtr.Zero, out CLResultCode contextCode);
            if (contextCode != CLResultCode.Success)
            {
                OpenClLogger.LogError($"Initialize: CreateContext failed for '{info.DeviceName}' ({contextCode}).");
                return false;
            }

            // CreateCommandQueue is deprecated since OpenCL 1.2, but it is the most broadly compatible
            // entry point: many CPU OpenCL runtimes only expose OpenCL 1.2, where CreateCommandQueue is the
            // canonical call. We keep it intentionally to maximize device coverage (including CPU-only systems).
#pragma warning disable CS0618
            CLCommandQueue queue = CL.CreateCommandQueue(context, info.Device, (CommandQueueProperty)0, out CLResultCode queueCode);
#pragma warning restore CS0618
            if (queueCode != CLResultCode.Success)
            {
                OpenClLogger.LogError($"Initialize: CreateCommandQueue failed for '{info.DeviceName}' ({queueCode}).");
                CL.ReleaseContext(context);
                return false;
            }

            this._register = new OpenClRegister(context, queue, info.Device);
            this._compiler = new OpenClCompiler(context, info.Device);
            this._launcher = new OpenClLauncher(this._compiler, queue);
            this._fourier = new OpenClFourier(this._register, this._launcher);

            this.SelectedDeviceId = deviceIndex;
            this.SelectedDevice = info;

            OpenClLogger.LogSuccess($"OpenClService: initialized device {info}.");
            return true;
        }

        /// <summary>
        /// Initializes the first device whose name contains <paramref name="deviceName"/> (case-insensitive).
        /// </summary>
        /// <param name="deviceName">A case-insensitive substring of the desired device name.</param>
        /// <returns><c>true</c> if a matching device was initialized; otherwise <c>false</c>.</returns>
        public bool Initialize(string deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                OpenClLogger.LogError("Initialize: device name is null or empty.");
                return false;
            }

            for (int i = 0; i < this.AvailableDevices.Count; i++)
            {
                if (this.AvailableDevices[i].DeviceName.Contains(deviceName, StringComparison.OrdinalIgnoreCase))
                {
                    return this.Initialize(i);
                }
            }

            OpenClLogger.LogError($"Initialize: no device matching '{deviceName}' found.");
            return false;
        }

        /// <summary>
        /// Releases the current device context, command queue, compiler and all allocations, leaving the
        /// service offline but reusable (devices can be re-enumerated via a new instance).
        /// </summary>
        public void Shutdown()
        {
            this._fourier = null;
            this._launcher = null;

            this._compiler?.Dispose();
            this._compiler = null;

            this._register?.Dispose();
            this._register = null;

            this.SelectedDeviceId = -1;
            this.SelectedDevice = null;
        }



        // Fourier convenience (delegates to OpenClFourier)
        /// <summary>
        /// Runs a full forward FFT over an arbitrary-length array. See <see cref="OpenClFourier.Fft(float[])"/>.
        /// </summary>
        public Vector2[]? Fft(float[] data)
        {
            return this.EnsureFourier()?.Fft(data);
        }

        /// <summary>
        /// Runs a full inverse FFT over an arbitrary-length array. See <see cref="OpenClFourier.Ifft(Vector2[], int)"/>.
        /// </summary>
        public float[]? Ifft(Vector2[] data, int originalLength = -1)
        {
            return this.EnsureFourier()?.Ifft(data, originalLength);
        }

        /// <summary>
        /// Runs a chunked forward FFT. See <see cref="OpenClFourier.FftChunked(float[], int, int)"/>.
        /// </summary>
        public Vector2[]? FftChunked(float[] data, int chunkSize = OpenClFourier.DefaultChunkSize, int overlap = 0)
        {
            return this.EnsureFourier()?.FftChunked(data, chunkSize, overlap);
        }

        /// <summary>
        /// Runs a chunked inverse FFT. See <see cref="OpenClFourier.IfftChunked(Vector2[], int, int)"/>.
        /// </summary>
        public float[]? IfftChunked(Vector2[] data, int chunkSize = OpenClFourier.DefaultChunkSize, int overlap = 0)
        {
            return this.EnsureFourier()?.IfftChunked(data, chunkSize, overlap);
        }

        /// <summary>
        /// Asynchronously runs a full forward FFT. See <see cref="OpenClFourier.FftAsync(float[])"/>.
        /// </summary>
        public Task<Vector2[]?> FftAsync(float[] data)
        {
            OpenClFourier? fourier = this.EnsureFourier();
            return fourier != null ? fourier.FftAsync(data) : Task.FromResult<Vector2[]?>(null);
        }

        /// <summary>
        /// Asynchronously runs a full inverse FFT. See <see cref="OpenClFourier.IfftAsync(Vector2[], int)"/>.
        /// </summary>
        public Task<float[]?> IfftAsync(Vector2[] data, int originalLength = -1)
        {
            OpenClFourier? fourier = this.EnsureFourier();
            return fourier != null ? fourier.IfftAsync(data, originalLength) : Task.FromResult<float[]?>(null);
        }



        // Helpers
        /// <summary>
        /// Returns the Fourier helper, logging an error when the service is offline.
        /// </summary>
        private OpenClFourier? EnsureFourier()
        {
            if (this._fourier == null)
            {
                OpenClLogger.LogError("OpenClService: service is offline. Call Initialize(...) first.");
            }

            return this._fourier;
        }



        // Disposal
        /// <summary>
        /// Disposes the service, releasing all OpenCL resources.
        /// </summary>
        public void Dispose()
        {
            if (this._disposed)
            {
                return;
            }

            this.Shutdown();
            this._disposed = true;
        }
    }
}

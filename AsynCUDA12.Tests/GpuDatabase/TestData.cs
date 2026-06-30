using System;
using System.Collections.Generic;
using System.IO;
using AsynCUDA.ClDatabase;
using AsynCUDA.OpenCl;
using AsynCUDA12.GpuDatabase;
using AsynCUDA12.Runtime;

namespace AsynCUDA12.Tests.GpuDatabase
{
    /// <summary>
    /// Shared, reusable test resources for the GPU database tests. Host tables/databases are built at
    /// runtime (deterministically) so they can be reused across CPU-only tests. A central GPU guard
    /// reports <see cref="Assert.Inconclusive(string)"/> when no usable CUDA device is present (or the
    /// CUDA driver is missing), which is the expected situation on a GPU-less DEV system.
    /// </summary>
    internal static class TestData
    {
        internal const string TableName = "people";

        /// <summary>
        /// Determines whether a CUDA device is available, swallowing driver-load failures (which occur
        /// on systems without the CUDA runtime) and treating them as "no device".
        /// </summary>
        /// <returns><c>true</c> if at least one CUDA device is usable; otherwise <c>false</c>.</returns>
        internal static bool CudaAvailable()
        {
            try
            {
                return CudaService.DeviceCount > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Skips the calling test as inconclusive when no CUDA device is available. Use at the top of
        /// every GPU-dependent test so the suite never fails on a GPU-less machine.
        /// </summary>
        internal static void RequireCuda()
        {
            if (!CudaAvailable())
            {
                Assert.Inconclusive("No CUDA device available on this machine; GPU test skipped.");
            }
        }

        /// <summary>
        /// Creates a usable, online <see cref="CudaService"/> for GPU tests, or returns <c>null</c> and
        /// marks the test inconclusive when initialization is not possible.
        /// </summary>
        /// <returns>An online service, or <c>null</c> when unavailable.</returns>
        internal static CudaService? CreateOnlineServiceOrSkip()
        {
            RequireCuda();
            CudaService service = new(-1);
            service.Initialize(0);
            if (!service.Online)
            {
                service.Dispose();
                Assert.Inconclusive("CUDA device present but could not be initialized; GPU test skipped.");
            }

            return service;
        }

        /// <summary>
        /// Determines whether an OpenCL runtime with at least one usable device is present, swallowing
        /// ICD/driver-load failures (which occur on systems without any OpenCL runtime) and treating them
        /// as "no device". Note that OpenCL is frequently available through a CPU device even on machines
        /// without a discrete GPU.
        /// </summary>
        /// <returns><c>true</c> if at least one OpenCL device is usable; otherwise <c>false</c>.</returns>
        internal static bool OpenClAvailable()
        {
            try
            {
                return OpenClDeviceInfo.DiscoverAll().Count > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Skips the calling test as inconclusive when no OpenCL device is available. Use at the top of
        /// every OpenCL-dependent test so the suite never fails on a machine without an OpenCL runtime.
        /// </summary>
        internal static void RequireOpenCl()
        {
            if (!OpenClAvailable())
            {
                Assert.Inconclusive("No OpenCL device available on this machine; OpenCL test skipped.");
            }
        }

        /// <summary>
        /// Creates a usable, online <see cref="OpenClService"/> for OpenCL tests, or returns <c>null</c>
        /// and marks the test inconclusive when initialization is not possible.
        /// </summary>
        /// <returns>An online service, or <c>null</c> when unavailable.</returns>
        internal static OpenClService? CreateOnlineOpenClServiceOrSkip()
        {
            RequireOpenCl();
            OpenClService service = new(-1);
            service.Initialize(0);
            if (!service.Online)
            {
                service.Dispose();
                Assert.Inconclusive("OpenCL device present but could not be initialized; OpenCL test skipped.");
            }

            return service;
        }

        /// <summary>
        /// Builds a deterministic host table with int/long/float/double/byte and string columns.
        /// </summary>
        /// <returns>The reusable host table.</returns>
        internal static HostTable BuildPeopleTable()
        {
            HostTable table = new(TableName, 5);
            table.Add(HostColumn.Numeric("id", GpuColumnType.Int32, new[] { 1, 2, 3, 4, 5 }));
            table.Add(HostColumn.Numeric("age", GpuColumnType.Int32, new[] { 17, 25, 33, 40, 64 }));
            table.Add(HostColumn.Numeric("bignum", GpuColumnType.Int64, new[] { 10L, 20L, 30L, 40L, 50L }));
            table.Add(HostColumn.Numeric("score", GpuColumnType.Single, new[] { 1.5f, 2.5f, 3.5f, 4.5f, 5.5f }));
            table.Add(HostColumn.Numeric("balance", GpuColumnType.Double, new[] { 100.0, 200.0, 300.0, 400.0, 500.0 }));
            table.Add(HostColumn.Numeric("flags", GpuColumnType.Byte, new byte[] { 0, 1, 0, 1, 1 }));
            table.Add(HostColumn.Strings("name", new[] { "Alice", "Bob", "Carol", "Dave", "Eve" }));
            return table;
        }

        /// <summary>
        /// Builds a deterministic OpenCL host table with int/long/float/double/byte and string columns,
        /// mirroring <see cref="BuildPeopleTable"/> for the ClDatabase tests.
        /// </summary>
        /// <returns>The reusable OpenCL host table.</returns>
        internal static ClHostTable BuildClPeopleTable()
        {
            ClHostTable table = new(TableName, 5);
            table.Add(ClHostColumn.Numeric("id", ClColumnType.Int32, new[] { 1, 2, 3, 4, 5 }));
            table.Add(ClHostColumn.Numeric("age", ClColumnType.Int32, new[] { 17, 25, 33, 40, 64 }));
            table.Add(ClHostColumn.Numeric("bignum", ClColumnType.Int64, new[] { 10L, 20L, 30L, 40L, 50L }));
            table.Add(ClHostColumn.Numeric("score", ClColumnType.Single, new[] { 1.5f, 2.5f, 3.5f, 4.5f, 5.5f }));
            table.Add(ClHostColumn.Numeric("balance", ClColumnType.Double, new[] { 100.0, 200.0, 300.0, 400.0, 500.0 }));
            table.Add(ClHostColumn.Numeric("flags", ClColumnType.Byte, new byte[] { 0, 1, 0, 1, 1 }));
            table.Add(ClHostColumn.Strings("name", new[] { "Alice", "Bob", "Carol", "Dave", "Eve" }));
            return table;
        }

        /// <summary>
        /// Builds a deterministic OpenCL host database containing the OpenCL people table.
        /// </summary>
        /// <returns>The reusable OpenCL host database.</returns>
        internal static ClHostDatabase BuildClDatabase()
        {
            ClHostDatabase database = new();
            database.Add(BuildClPeopleTable());
            return database;
        }
        internal static HostDatabase BuildDatabase()
        {
            HostDatabase database = new();
            database.Add(BuildPeopleTable());
            return database;
        }

        /// <summary>
        /// Builds a small CSV document (with header) matching <see cref="BuildPeopleTable"/>'s shape.
        /// </summary>
        /// <returns>The CSV text.</returns>
        internal static string BuildCsv()
        {
            return string.Join(
                "\n",
                "id,age,name,balance",
                "1,17,Alice,100.5",
                "2,25,Bob,200.5",
                "3,33,Carol,300.5");
        }

        /// <summary>
        /// Creates a unique temporary file path inside a dedicated test directory.
        /// </summary>
        /// <param name="extension">The file extension (including the leading dot).</param>
        /// <returns>The temporary file path (the file is not created).</returns>
        internal static string TempFile(string extension)
        {
            string directory = Path.Combine(Path.GetTempPath(), "AsynCUDA12.Tests");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, Guid.NewGuid().ToString("N") + extension);
        }
    }
}

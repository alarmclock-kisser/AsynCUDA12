using System;
using System.Collections.Generic;
using AsynCUDA.OpenCl;

namespace AsynCUDA.ClDatabase
{
    /// <summary>
    /// The public lifecycle facade for the OpenCL-native, in-memory database. A <see cref="ClDatabase"/>
    /// owns an <see cref="OpenClService"/> and coordinates the <see cref="ClTableCatalog"/>,
    /// <see cref="ClKernelCatalog"/>, <see cref="ClPersistenceManager"/>, <see cref="ClMemoryPolicy"/> and
    /// <see cref="ClQueryEngine"/>. It loads a persisted database file into device memory on
    /// <see cref="Open(string)"/>, writes device data back to disk on <see cref="Save(string)"/> /
    /// <see cref="Checkpoint"/>, executes queries on the GPU, and releases all device memory on
    /// <see cref="Close"/> / <see cref="Dispose"/>. It does not modify any runtime API.
    /// </summary>
    public sealed class ClDatabase : IDisposable
    {
        private readonly OpenClService service;
        private readonly bool ownsService;
        private readonly ClTableCatalog catalog = new();
        private readonly ClPersistenceManager persistence = new();
        private readonly ClKernelCatalog kernels;
        private ClTableLoader? loader;
        private ClQueryEngine? engine;

        private string? path;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClDatabase"/> class on a specific device.
        /// </summary>
        /// <param name="deviceId">The flat OpenCL device index to initialize.</param>
        public ClDatabase(int deviceId = 0)
            : this(new OpenClService(deviceId), ownsService: true)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClDatabase"/> class using an existing runtime service.
        /// </summary>
        /// <param name="service">The already-initialized runtime service to use.</param>
        /// <param name="ownsService">When <c>true</c>, the database disposes the service on teardown.</param>
        public ClDatabase(OpenClService service, bool ownsService = false)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.ownsService = ownsService;
            this.kernels = new ClKernelCatalog(service);
            this.WireRuntime();
        }

        private void WireRuntime()
        {
            OpenClRegister? register = this.service.Register;
            OpenClLauncher? launcher = this.service.Launcher;
            if (register == null || launcher == null)
            {
                return;
            }

            this.loader = new ClTableLoader(register);
            ClKernelExecutor executor = new(launcher);
            this.engine = new ClQueryEngine(register, this.catalog, executor);
        }

        /// <summary>Gets a value indicating whether the underlying OpenCL service is online.</summary>
        public bool IsOnline => this.service.Online;

        /// <summary>Gets the table catalog describing the device-resident tables.</summary>
        public ClTableCatalog Catalog => this.catalog;

        /// <summary>Gets the memory policy used for device-memory budgeting and hot/cold decisions.</summary>
        public ClMemoryPolicy MemoryPolicy { get; } = new();

        /// <summary>
        /// Opens a persisted database file: reads it from disk, transforms its columns and uploads all
        /// tables into device memory. Ensures the database kernels are compiled and loadable first.
        /// </summary>
        /// <param name="databasePath">The database file path.</param>
        public void Open(string databasePath)
        {
            if (!this.service.Online)
            {
                throw new InvalidOperationException("OpenCL service is offline; cannot open database.");
            }

            this.EnsureRuntimeWired();
            this.kernels.EnsureCompiled();
            this.path = databasePath;

            ClHostDatabase host = this.persistence.Load(databasePath);
            foreach (ClHostTable table in host.Tables)
            {
                this.ImportTable(table);
            }
        }

        /// <summary>
        /// Imports a host table into device memory and registers it in the catalog.
        /// </summary>
        /// <param name="table">The host table to import.</param>
        /// <returns>The device-resident table.</returns>
        public ClTable ImportTable(ClHostTable table)
        {
            this.EnsureRuntimeWired();
            ClTable clTable = this.loader!.Upload(table);
            this.catalog.Add(clTable);
            return clTable;
        }

        /// <summary>
        /// Loads a CSV/TXT file into a host table (with automatic type inference) and imports it into device memory.
        /// </summary>
        /// <param name="path">The CSV/TXT file path.</param>
        /// <param name="tableName">The table name; the file name (without extension) when <c>null</c>.</param>
        /// <param name="options">The parsing options; <see cref="ClCsvOptions.Default"/> when <c>null</c>.</param>
        /// <returns>The device-resident table.</returns>
        public ClTable ImportCsv(string path, string? tableName = null, ClCsvOptions? options = null)
        {
            ClHostTable table = new ClCsvTableLoader(options).LoadFile(path, tableName);
            return this.ImportTable(table);
        }

        /// <summary>
        /// Loads a CSV/TXT file into a host table (using an explicit schema) and imports it into device memory.
        /// </summary>
        /// <param name="path">The CSV/TXT file path.</param>
        /// <param name="schema">The explicit per-column types.</param>
        /// <param name="tableName">The table name; the file name (without extension) when <c>null</c>.</param>
        /// <param name="options">The parsing options; <see cref="ClCsvOptions.Default"/> when <c>null</c>.</param>
        /// <returns>The device-resident table.</returns>
        public ClTable ImportCsv(string path, IReadOnlyList<ClColumnType> schema, string? tableName = null, ClCsvOptions? options = null)
        {
            ClHostTable table = new ClCsvTableLoader(options).LoadFile(path, schema, tableName);
            return this.ImportTable(table);
        }

        /// <summary>
        /// Executes a query against the device-resident tables.
        /// </summary>
        /// <param name="query">The query to execute.</param>
        /// <returns>The query result.</returns>
        public ClQueryResult ExecuteQuery(ClQuery query)
        {
            this.EnsureRuntimeWired();
            return this.engine!.Execute(query);
        }

        /// <summary>
        /// Saves all device-resident tables to the given path (atomically), reconstructing the host model
        /// from device memory. Device buffers remain resident.
        /// </summary>
        /// <param name="databasePath">The destination database file path.</param>
        public void Save(string databasePath)
        {
            ClHostDatabase host = this.Snapshot();
            this.persistence.Save(databasePath, host);
            this.path = databasePath;
        }

        /// <summary>
        /// Writes a checkpoint to the path the database was opened from (or last saved to).
        /// </summary>
        public void Checkpoint()
        {
            if (string.IsNullOrEmpty(this.path))
            {
                throw new InvalidOperationException("No database path is known; call Save(path) first.");
            }

            this.Save(this.path);
        }

        /// <summary>
        /// Produces an in-memory host snapshot of all device-resident tables (keeping device buffers).
        /// </summary>
        /// <returns>The host database snapshot.</returns>
        public ClHostDatabase Snapshot()
        {
            this.EnsureRuntimeWired();
            ClHostDatabase host = new();
            foreach (ClTable table in this.catalog.Tables)
            {
                host.Add(this.loader!.Download(table));
            }

            return host;
        }

        /// <summary>
        /// Frees every device buffer backing the catalog's tables and clears the catalog.
        /// </summary>
        public void Close()
        {
            OpenClRegister? register = this.service.Register;
            if (register != null)
            {
                foreach (ClTable table in this.catalog.Tables)
                {
                    foreach (ClColumn column in table.EnumerateBackingColumns())
                    {
                        register.Free(column.Memory);
                    }
                }
            }

            this.catalog.Clear();
        }

        /// <summary>
        /// Closes the database (freeing device memory) and disposes the owned runtime service, if any.
        /// </summary>
        public void Dispose()
        {
            this.Close();
            if (this.ownsService)
            {
                this.service.Dispose();
            }
        }

        private void EnsureRuntimeWired()
        {
            if (this.loader == null || this.engine == null)
            {
                this.WireRuntime();
            }

            if (this.loader == null || this.engine == null)
            {
                throw new InvalidOperationException("OpenCL service is offline; the runtime is not available.");
            }
        }
    }
}

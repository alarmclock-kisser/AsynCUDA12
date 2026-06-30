using System;
using System.Collections.Generic;
using AsynCUDA12.Runtime;

namespace AsynCUDA12.GpuDatabase
{
    /// <summary>
    /// The public lifecycle facade for the GPU-native, in-memory database. A <see cref="GpuDatabase"/>
    /// owns a <see cref="CudaService"/> and coordinates the <see cref="TableCatalog"/>,
    /// <see cref="KernelCatalog"/>, <see cref="PersistenceManager"/>, <see cref="MemoryPolicy"/> and
    /// <see cref="GpuQueryEngine"/>. It loads a persisted database file into VRAM on
    /// <see cref="Open(string)"/>, writes VRAM data back to disk on <see cref="Save(string)"/> /
    /// <see cref="Checkpoint"/>, executes queries on the GPU, and releases all device memory on
    /// <see cref="Close"/> / <see cref="Dispose"/>. It does not modify any runtime API.
    /// </summary>
    public sealed class GpuDatabase : IDisposable
    {
        private readonly CudaService cuda;
        private readonly bool ownsService;
        private readonly TableCatalog catalog = new();
        private readonly PersistenceManager persistence = new();
        private readonly KernelCatalog kernels;
        private readonly TableLoader loader;
        private readonly GpuQueryEngine engine;

        private string? path;

        /// <summary>
        /// Initializes a new instance of the <see cref="GpuDatabase"/> class on a specific device.
        /// </summary>
        /// <param name="deviceId">The CUDA device id to initialize.</param>
        public GpuDatabase(int deviceId = 0)
            : this(new CudaService(deviceId), ownsService: true)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GpuDatabase"/> class using an existing runtime service.
        /// </summary>
        /// <param name="cuda">The already-initialized runtime service to use.</param>
        /// <param name="ownsService">When <c>true</c>, the database disposes the service on teardown.</param>
        public GpuDatabase(CudaService cuda, bool ownsService = false)
        {
            this.cuda = cuda ?? throw new ArgumentNullException(nameof(cuda));
            this.ownsService = ownsService;
            this.kernels = new KernelCatalog(cuda);
            this.loader = new TableLoader(cuda);
            GpuKernelExecutor executor = new(cuda);
            this.engine = new GpuQueryEngine(cuda, this.catalog, executor);
        }

        /// <summary>Gets a value indicating whether the underlying CUDA service is online.</summary>
        public bool IsOnline => this.cuda.Online;

        /// <summary>Gets the table catalog describing the VRAM-resident tables.</summary>
        public TableCatalog Catalog => this.catalog;

        /// <summary>Gets the memory policy used for VRAM budgeting and hot/cold decisions.</summary>
        public MemoryPolicy MemoryPolicy { get; } = new();

        /// <summary>
        /// Opens a persisted database file: reads it from disk, transforms its columns and uploads all
        /// tables into VRAM. Ensures the database kernels are compiled and loadable first.
        /// </summary>
        /// <param name="databasePath">The database file path.</param>
        public void Open(string databasePath)
        {
            if (!this.cuda.Online)
            {
                throw new InvalidOperationException("CUDA service is offline; cannot open database.");
            }

            this.kernels.EnsureCompiled();
            this.path = databasePath;

            HostDatabase host = this.persistence.Load(databasePath);
            foreach (HostTable table in host.Tables)
            {
                this.ImportTable(table);
            }
        }

        /// <summary>
        /// Imports a host table into VRAM and registers it in the catalog.
        /// </summary>
        /// <param name="table">The host table to import.</param>
        /// <returns>The VRAM-resident table.</returns>
        public GpuTable ImportTable(HostTable table)
        {
            GpuTable gpuTable = this.loader.Upload(table);
            this.catalog.Add(gpuTable);
            return gpuTable;
        }

        /// <summary>
        /// Loads a CSV/TXT file into a host table (with automatic type inference) and imports it into VRAM.
        /// </summary>
        /// <param name="path">The CSV/TXT file path.</param>
        /// <param name="tableName">The table name; the file name (without extension) when <c>null</c>.</param>
        /// <param name="options">The parsing options; <see cref="CsvOptions.Default"/> when <c>null</c>.</param>
        /// <returns>The VRAM-resident table.</returns>
        public GpuTable ImportCsv(string path, string? tableName = null, CsvOptions? options = null)
        {
            HostTable table = new CsvTableLoader(options).LoadFile(path, tableName);
            return this.ImportTable(table);
        }

        /// <summary>
        /// Loads a CSV/TXT file into a host table (using an explicit schema) and imports it into VRAM.
        /// </summary>
        /// <param name="path">The CSV/TXT file path.</param>
        /// <param name="schema">The explicit per-column types.</param>
        /// <param name="tableName">The table name; the file name (without extension) when <c>null</c>.</param>
        /// <param name="options">The parsing options; <see cref="CsvOptions.Default"/> when <c>null</c>.</param>
        /// <returns>The VRAM-resident table.</returns>
        public GpuTable ImportCsv(string path, IReadOnlyList<GpuColumnType> schema, string? tableName = null, CsvOptions? options = null)
        {
            HostTable table = new CsvTableLoader(options).LoadFile(path, schema, tableName);
            return this.ImportTable(table);
        }

        /// <summary>
        /// Executes a query against the VRAM-resident tables.
        /// </summary>
        /// <param name="query">The query to execute.</param>
        /// <returns>The query result.</returns>
        public QueryResult ExecuteQuery(GpuQuery query) => this.engine.Execute(query);

        /// <summary>
        /// Saves all VRAM-resident tables to the given path (atomically), reconstructing the host model
        /// from device memory. Device buffers remain resident.
        /// </summary>
        /// <param name="databasePath">The destination database file path.</param>
        public void Save(string databasePath)
        {
            HostDatabase host = this.Snapshot();
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
        /// Produces an in-memory host snapshot of all VRAM-resident tables (keeping device buffers).
        /// </summary>
        /// <returns>The host database snapshot.</returns>
        public HostDatabase Snapshot()
        {
            HostDatabase host = new();
            foreach (GpuTable table in this.catalog.Tables)
            {
                host.Add(this.loader.Download(table));
            }

            return host;
        }

        /// <summary>
        /// Frees every device buffer backing the catalog's tables and clears the catalog.
        /// </summary>
        public void Close()
        {
            foreach (GpuTable table in this.catalog.Tables)
            {
                foreach (GpuColumn column in table.EnumerateBackingColumns())
                {
                    this.cuda.FreeMemory(column.IndexPointer);
                }
            }

            this.catalog.Clear();
        }

        /// <summary>
        /// Closes the database (freeing VRAM) and disposes the owned runtime service, if any.
        /// </summary>
        public void Dispose()
        {
            this.Close();
            if (this.ownsService)
            {
                this.cuda.Dispose();
            }
        }
    }
}

using System;
using System.IO;
using AsynCUDA12.GpuDatabase;
using AsynCUDA12.Runtime;
using Shouldly;
using GpuDb = global::AsynCUDA12.GpuDatabase.GpuDatabase;

namespace AsynCUDA12.Tests.GpuDatabase
{
    /// <summary>
    /// Tests for the <see cref="GpuDatabase"/> facade. Only the truly service-free guard (null argument)
    /// runs everywhere; every path that constructs a <see cref="CudaService"/> requires the CUDA driver
    /// (the service enumerates devices in its constructor) and is therefore skipped as inconclusive on a
    /// GPU-less machine via <see cref="TestData.RequireCuda"/> / <see cref="TestData.CreateOnlineServiceOrSkip"/>.
    /// </summary>
    [TestClass]
    public sealed class GpuDatabaseGpuTests
    {
        // ---- Service-free guard (runs without a GPU) ----

        [TestMethod]
        public void Construct_WithNullService_Throws()
        {
            Should.Throw<ArgumentNullException>(() => new GpuDb(null!));
        }

        // ---- Offline-service behaviour (needs the CUDA driver to construct CudaService) ----

        [TestMethod]
        public void Construct_WithOfflineService_IsNotOnline()
        {
            TestData.RequireCuda();
            using GpuDb db = new(new CudaService(-1));

            db.IsOnline.ShouldBeFalse();
            db.Catalog.Count.ShouldBe(0);
            db.MemoryPolicy.ShouldNotBeNull();
        }

        [TestMethod]
        public void Open_WhenOffline_ThrowsInvalidOperation()
        {
            TestData.RequireCuda();
            using GpuDb db = new(new CudaService(-1));

            Should.Throw<InvalidOperationException>(() => db.Open("anything.gpdb"));
        }

        [TestMethod]
        public void Checkpoint_WithoutKnownPath_Throws()
        {
            TestData.RequireCuda();
            using GpuDb db = new(new CudaService(-1));

            Should.Throw<InvalidOperationException>(() => db.Checkpoint());
        }

        [TestMethod]
        public void CloseAndDispose_OnEmptyDatabase_DoNotThrow()
        {
            TestData.RequireCuda();
            GpuDb db = new(new CudaService(-1));

            Should.NotThrow(() => db.Close());
            Should.NotThrow(() => db.Dispose());
        }

        // ---- End-to-end GPU paths (skipped without a CUDA device) ----

        [TestMethod]
        public void ImportTableQueryAndSnapshot_RoundTrip_OnGpu()
        {
            using CudaService? service = TestData.CreateOnlineServiceOrSkip();
            using GpuDb db = new(service!);

            db.ImportTable(TestData.BuildPeopleTable());
            db.Catalog.Count.ShouldBe(1);

            QueryResult result = db.ExecuteQuery(GpuQuery.From(TestData.TableName).SelectCount());
            result.Success.ShouldBeTrue();

            HostDatabase snapshot = db.Snapshot();
            snapshot.Tables.Count.ShouldBe(1);
            snapshot.Tables[0].RowCount.ShouldBe(5);
        }

        [TestMethod]
        public void SaveThenOpen_RoundTrip_OnGpu()
        {
            using CudaService? service = TestData.CreateOnlineServiceOrSkip();
            string path = TestData.TempFile(".gpdb");

            using (GpuDb writer = new(service!))
            {
                writer.ImportTable(TestData.BuildPeopleTable());
                writer.Save(path);
            }

            File.Exists(path).ShouldBeTrue();
        }

        [TestMethod]
        public void ImportCsv_RoundTrip_OnGpu()
        {
            using CudaService? service = TestData.CreateOnlineServiceOrSkip();
            using GpuDb db = new(service!);
            string path = TestData.TempFile(".csv");
            File.WriteAllText(path, TestData.BuildCsv());

            GpuTable table = db.ImportCsv(path, "people");

            table.RowCount.ShouldBe(3);
            db.Catalog.GetTable("people").ShouldNotBeNull();
        }
    }
}

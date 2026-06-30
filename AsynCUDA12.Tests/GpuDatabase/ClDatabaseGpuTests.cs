using System;
using System.IO;
using AsynCUDA.ClDatabase;
using AsynCUDA.OpenCl;
using Shouldly;
using ClDb = global::AsynCUDA.ClDatabase.ClDatabase;

namespace AsynCUDA12.Tests.GpuDatabase
{
    /// <summary>
    /// Tests for the <see cref="AsynCUDA.ClDatabase.ClDatabase"/> facade. Only the truly service-free guard
    /// (null argument) runs everywhere; every path that constructs an <see cref="OpenClService"/> requires
    /// an OpenCL runtime (the service enumerates devices in its constructor) and is therefore skipped as
    /// inconclusive on a machine without OpenCL via <see cref="TestData.RequireOpenCl"/> /
    /// <see cref="TestData.CreateOnlineOpenClServiceOrSkip"/>.
    /// </summary>
    [TestClass]
    public sealed class ClDatabaseGpuTests
    {
        // ---- Service-free guard (runs without an OpenCL device) ----

        [TestMethod]
        public void Construct_WithNullService_Throws()
        {
            Should.Throw<ArgumentNullException>(() => new ClDb((OpenClService)null!));
        }

        // ---- Offline-service behaviour (needs an OpenCL runtime to construct OpenClService) ----

        [TestMethod]
        public void Construct_WithOfflineService_IsNotOnline()
        {
            TestData.RequireOpenCl();
            using ClDb db = new(new OpenClService(-1));

            db.IsOnline.ShouldBeFalse();
            db.Catalog.Count.ShouldBe(0);
            db.MemoryPolicy.ShouldNotBeNull();
        }

        [TestMethod]
        public void Open_WhenOffline_ThrowsInvalidOperation()
        {
            TestData.RequireOpenCl();
            using ClDb db = new(new OpenClService(-1));

            Should.Throw<InvalidOperationException>(() => db.Open("anything.gpdb"));
        }

        [TestMethod]
        public void Checkpoint_WithoutKnownPath_Throws()
        {
            TestData.RequireOpenCl();
            using ClDb db = new(new OpenClService(-1));

            Should.Throw<InvalidOperationException>(() => db.Checkpoint());
        }

        [TestMethod]
        public void CloseAndDispose_OnEmptyDatabase_DoNotThrow()
        {
            TestData.RequireOpenCl();
            ClDb db = new(new OpenClService(-1));

            Should.NotThrow(() => db.Close());
            Should.NotThrow(() => db.Dispose());
        }

        // ---- End-to-end OpenCL paths (skipped without an OpenCL device) ----

        [TestMethod]
        public void ImportTableQueryAndSnapshot_RoundTrip_OnGpu()
        {
            using OpenClService? service = TestData.CreateOnlineOpenClServiceOrSkip();
            using ClDb db = new(service!);

            db.ImportTable(TestData.BuildClPeopleTable());
            db.Catalog.Count.ShouldBe(1);

            ClQueryResult result = db.ExecuteQuery(ClQuery.From(TestData.TableName).SelectCount());
            result.Success.ShouldBeTrue();

            ClHostDatabase snapshot = db.Snapshot();
            snapshot.Tables.Count.ShouldBe(1);
            snapshot.Tables[0].RowCount.ShouldBe(5);
        }

        [TestMethod]
        public void SaveThenOpen_RoundTrip_OnGpu()
        {
            using OpenClService? service = TestData.CreateOnlineOpenClServiceOrSkip();
            string path = TestData.TempFile(".gpdb");

            using (ClDb writer = new(service!))
            {
                writer.ImportTable(TestData.BuildClPeopleTable());
                writer.Save(path);
            }

            File.Exists(path).ShouldBeTrue();
        }

        [TestMethod]
        public void ImportCsv_RoundTrip_OnGpu()
        {
            using OpenClService? service = TestData.CreateOnlineOpenClServiceOrSkip();
            using ClDb db = new(service!);
            string path = TestData.TempFile(".csv");
            File.WriteAllText(path, TestData.BuildCsv());

            ClTable table = db.ImportCsv(path, "people");

            table.RowCount.ShouldBe(3);
            db.Catalog.GetTable("people").ShouldNotBeNull();
        }
    }
}

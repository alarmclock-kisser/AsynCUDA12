using System;
using System.Collections.Generic;
using System.Text;
using AsynCUDA12.GpuDatabase;
using AsynCUDA12.Runtime;
using ManagedCuda.BasicTypes;
using Shouldly;
using GpuDb = global::AsynCUDA12.GpuDatabase.GpuDatabase;

namespace AsynCUDA12.Tests.GpuDatabase
{
    /// <summary>
    /// Real GPU tests that exercise every one of the 12 database CUDA kernels ("Kernel-SQL functions").
    /// Filter/aggregate kernels are driven through the query engine; the lower-level kernels
    /// (project/index/join/arithmetic) are launched directly through <see cref="GpuKernelExecutor"/>.
    /// All tests are skipped as inconclusive when no CUDA device is available, so they never fail on a
    /// GPU-less DEV system. They require the kernels in the runtime's <c>Kernels/CU</c> folder to have
    /// been compiled by a prior device initialization.
    /// </summary>
    [TestClass]
    public sealed class KernelSqlGpuTests
    {
        private static GpuDb NewDatabase(CudaService service)
        {
            GpuDb db = new(service);
            db.ImportTable(TestData.BuildPeopleTable());
            return db;
        }

        // ---- Filter kernels via the query engine ----

        [TestMethod]
        public void FilterIntEquals_SelectsMatchingRows()
        {
            using CudaService? service = TestData.CreateOnlineServiceOrSkip();
            using GpuDb db = NewDatabase(service!);

            QueryResult result = db.ExecuteQuery(GpuQuery.From(TestData.TableName).WhereIntEquals("age", 33).SelectCount());

            result.Success.ShouldBeTrue();
            result.Count.ShouldBe(1);
        }

        [TestMethod]
        public void FilterIntRange_SelectsRowsInRange()
        {
            using CudaService? service = TestData.CreateOnlineServiceOrSkip();
            using GpuDb db = NewDatabase(service!);

            QueryResult result = db.ExecuteQuery(GpuQuery.From(TestData.TableName).WhereIntBetween("age", 20, 40).SelectCount());

            result.Success.ShouldBeTrue();
            result.Count.ShouldBe(2);
        }

        [TestMethod]
        public void FilterFloatRange_SelectsRowsInRange()
        {
            using CudaService? service = TestData.CreateOnlineServiceOrSkip();
            using GpuDb db = NewDatabase(service!);

            QueryResult result = db.ExecuteQuery(GpuQuery.From(TestData.TableName).WhereFloatBetween("score", 2.0f, 4.0f).SelectCount());

            result.Success.ShouldBeTrue();
            result.Count.ShouldBe(2);
        }

        // ---- Aggregation kernels via the query engine ----

        [TestMethod]
        public void CountMask_CountsSelectedRows()
        {
            using CudaService? service = TestData.CreateOnlineServiceOrSkip();
            using GpuDb db = NewDatabase(service!);

            QueryResult result = db.ExecuteQuery(GpuQuery.From(TestData.TableName).WhereIntBetween("age", 0, 100).SelectCount());

            result.Success.ShouldBeTrue();
            result.Count.ShouldBe(5);
        }

        [TestMethod]
        public void SumFloatByMask_SumsSelectedValues()
        {
            using CudaService? service = TestData.CreateOnlineServiceOrSkip();
            using GpuDb db = NewDatabase(service!);

            QueryResult result = db.ExecuteQuery(GpuQuery.From(TestData.TableName).WhereIntBetween("age", 0, 100).SelectSum("balance"));

            result.Success.ShouldBeTrue();
            result.Value!.Value.ShouldBe(1500.0, 0.001);
        }

        [TestMethod]
        public void Average_DerivedFromSumAndCount()
        {
            using CudaService? service = TestData.CreateOnlineServiceOrSkip();
            using GpuDb db = NewDatabase(service!);

            QueryResult result = db.ExecuteQuery(GpuQuery.From(TestData.TableName).WhereIntBetween("age", 0, 100).SelectAverage("balance"));

            result.Success.ShouldBeTrue();
            result.Value!.Value.ShouldBe(300.0, 0.001);
        }

        [TestMethod]
        public void MinMaxFloat_ComputesMinAndMax()
        {
            using CudaService? service = TestData.CreateOnlineServiceOrSkip();
            using GpuDb db = NewDatabase(service!);

            QueryResult result = db.ExecuteQuery(GpuQuery.From(TestData.TableName).SelectMinMax("score"));

            result.Success.ShouldBeTrue();
            result.Min!.Value.ShouldBe(1.5, 0.001);
            result.Max!.Value.ShouldBe(5.5, 0.001);
        }

        // ---- String kernels via the query engine ----

        [TestMethod]
        public void StringContains_MatchesSubstring()
        {
            using CudaService? service = TestData.CreateOnlineServiceOrSkip();
            using GpuDb db = NewDatabase(service!);

            QueryResult result = db.ExecuteQuery(GpuQuery.From(TestData.TableName).WhereStringContains("name", "ar").SelectCount());

            result.Success.ShouldBeTrue();
            result.Count.ShouldBe(1); // "Carol"
        }

        [TestMethod]
        public void StringFuzzyLevenshtein_MatchesWithinDistance()
        {
            using CudaService? service = TestData.CreateOnlineServiceOrSkip();
            using GpuDb db = NewDatabase(service!);

            QueryResult result = db.ExecuteQuery(GpuQuery.From(TestData.TableName).WhereStringFuzzy("name", "Alik", 2).SelectCount());

            result.Success.ShouldBeTrue();
            result.Count!.Value.ShouldBeGreaterThanOrEqualTo(1); // "Alice" within distance 2
        }

        // ---- Lower-level kernels via the executor directly ----

        [TestMethod]
        public void ProjectByMask_CopiesSelectedValues()
        {
            using CudaService? service = TestData.CreateOnlineServiceOrSkip();
            service!.Compiler!.CompileAll(silent: true, logErrors: true);
            GpuKernelExecutor executor = new(service);

            CudaMem values = service.PushData(new[] { 1f, 2f, 3f, 4f })!;
            CudaMem mask = service.PushData(new byte[] { 1, 0, 1, 0 })!;
            CudaMem output = service.AllocateSingle<float>(4)!;

            bool ok = executor.Execute(
                KernelNames.ProjectByMask,
                new[] { values.DevicePointers[0], mask.DevicePointers[0], output.DevicePointers[0] },
                new object[] { 4 },
                4);

            ok.ShouldBeTrue();
            float[] result = service.PullData<float>(output.IndexPointer)!;
            result[0].ShouldBe(1f);
            result[1].ShouldBe(0f);
            result[2].ShouldBe(3f);
        }

        [TestMethod]
        public void ApplyArithmeticFloat_MultipliesInPlace()
        {
            using CudaService? service = TestData.CreateOnlineServiceOrSkip();
            service!.Compiler!.CompileAll(silent: true, logErrors: true);
            GpuKernelExecutor executor = new(service);

            CudaMem values = service.PushData(new[] { 1f, 2f, 3f })!;

            bool ok = executor.Execute(
                KernelNames.ApplyArithmeticFloat,
                new[] { values.DevicePointers[0] },
                new object[] { 3, 2 /* op=mul */, 10f },
                3);

            ok.ShouldBeTrue();
            float[] result = service.PullData<float>(values.IndexPointer)!;
            result.ShouldBe(new[] { 10f, 20f, 30f });
        }

        [TestMethod]
        public void BuildHashIndexAndHashJoinInt_FindMatches()
        {
            using CudaService? service = TestData.CreateOnlineServiceOrSkip();
            service!.Compiler!.CompileAll(silent: true, logErrors: true);
            GpuKernelExecutor executor = new(service);

            int capacity = 16;
            int[] keys = { 5, 9, 13 };
            CudaMem keyMem = service.PushData(keys)!;
            CudaMem tableKeys = service.PushData(InitArray(capacity, -1))!;
            CudaMem tableRows = service.PushData(InitArray(capacity, -1))!;

            bool built = executor.Execute(
                KernelNames.BuildHashIndexInt,
                new[] { keyMem.DevicePointers[0], tableKeys.DevicePointers[0], tableRows.DevicePointers[0] },
                new object[] { keys.Length, capacity },
                keys.Length);
            built.ShouldBeTrue();

            int[] probe = { 9, 7 };
            CudaMem probeMem = service.PushData(probe)!;
            CudaMem result = service.AllocateSingle<int>(probe.Length)!;

            bool joined = executor.Execute(
                KernelNames.HashJoinInt,
                new[] { probeMem.DevicePointers[0], tableKeys.DevicePointers[0], tableRows.DevicePointers[0], result.DevicePointers[0] },
                new object[] { probe.Length, capacity },
                probe.Length);
            joined.ShouldBeTrue();

            int[] matches = service.PullData<int>(result.IndexPointer)!;
            matches[0].ShouldBe(1);   // key 9 was inserted at row index 1
            matches[1].ShouldBe(-1);  // key 7 not present
        }

        private static int[] InitArray(int length, int value)
        {
            int[] array = new int[length];
            for (int i = 0; i < length; i++)
            {
                array[i] = value;
            }

            return array;
        }
    }
}

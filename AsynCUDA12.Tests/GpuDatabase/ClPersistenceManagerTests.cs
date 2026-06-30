using System;
using System.IO;
using AsynCUDA.ClDatabase;
using Shouldly;

namespace AsynCUDA12.Tests.GpuDatabase
{
    /// <summary>
    /// CPU-only tests for <see cref="ClPersistenceManager"/>: GPDB save/load roundtrips for every column
    /// type, the UTF-8 string layout (offsets/lengths), atomic overwrite, and malformed-file handling.
    /// These run fully on a machine without OpenCL.
    /// </summary>
    [TestClass]
    public sealed class ClPersistenceManagerTests
    {
        private readonly ClPersistenceManager persistence = new();

        [TestMethod]
        public void SaveThenLoad_RoundTripsAllColumnTypes()
        {
            string path = TestData.TempFile(".gpdb");
            ClHostDatabase original = TestData.BuildClDatabase();

            this.persistence.Save(path, original);
            ClHostDatabase loaded = this.persistence.Load(path);

            loaded.Tables.Count.ShouldBe(1);
            ClHostTable table = loaded.Tables[0];
            table.Name.ShouldBe(TestData.TableName);
            table.RowCount.ShouldBe(5);
            table.Columns.Count.ShouldBe(7);
        }

        [TestMethod]
        public void SaveThenLoad_PreservesNumericValues()
        {
            string path = TestData.TempFile(".gpdb");
            this.persistence.Save(path, TestData.BuildClDatabase());

            ClHostTable table = this.persistence.Load(path).Tables[0];

            table.Columns[0].AsArray<int>().ShouldBe(new[] { 1, 2, 3, 4, 5 });
            table.Columns[2].AsArray<long>().ShouldBe(new[] { 10L, 20L, 30L, 40L, 50L });
            table.Columns[3].AsArray<float>().ShouldBe(new[] { 1.5f, 2.5f, 3.5f, 4.5f, 5.5f });
            table.Columns[4].AsArray<double>().ShouldBe(new[] { 100.0, 200.0, 300.0, 400.0, 500.0 });
            table.Columns[5].AsArray<byte>().ShouldBe(new byte[] { 0, 1, 0, 1, 1 });
        }

        [TestMethod]
        public void SaveThenLoad_PreservesStrings()
        {
            string path = TestData.TempFile(".gpdb");
            this.persistence.Save(path, TestData.BuildClDatabase());

            ClHostTable table = this.persistence.Load(path).Tables[0];

            table.Columns[6].Type.ShouldBe(ClColumnType.String);
            table.Columns[6].AsStrings().ShouldBe(new[] { "Alice", "Bob", "Carol", "Dave", "Eve" });
        }

        [TestMethod]
        public void Save_OverwritesExistingFileAtomically()
        {
            string path = TestData.TempFile(".gpdb");
            this.persistence.Save(path, TestData.BuildClDatabase());
            this.persistence.Save(path, TestData.BuildClDatabase());

            File.Exists(path).ShouldBeTrue();
            File.Exists(path + ".tmp").ShouldBeFalse();
            this.persistence.Load(path).Tables.Count.ShouldBe(1);
        }

        [TestMethod]
        public void Load_BadMagic_Throws()
        {
            string path = TestData.TempFile(".gpdb");
            File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

            Should.Throw<InvalidDataException>(() => this.persistence.Load(path));
        }

        [TestMethod]
        public void EncodeStrings_ProducesContiguousOffsetsAndLengths()
        {
            (byte[] data, int[] offsets, int[] lengths) = ClPersistenceManager.EncodeStrings(new[] { "ab", "", "cde" });

            data.Length.ShouldBe(5);
            offsets.ShouldBe(new[] { 0, 2, 2 });
            lengths.ShouldBe(new[] { 2, 0, 3 });
        }

        [TestMethod]
        public void DecodeStrings_ReconstructsOriginalValues()
        {
            string[] values = { "Hello", "", "Wörld" };
            (byte[] data, int[] offsets, int[] lengths) = ClPersistenceManager.EncodeStrings(values);

            ClPersistenceManager.DecodeStrings(data, offsets, lengths).ShouldBe(values);
        }

        [TestMethod]
        public void EncodeStrings_NullEntry_TreatedAsEmpty()
        {
            (byte[] data, int[] offsets, int[] lengths) = ClPersistenceManager.EncodeStrings(new string[] { null! });

            data.Length.ShouldBe(0);
            offsets.ShouldBe(new[] { 0 });
            lengths.ShouldBe(new[] { 0 });
        }
    }
}

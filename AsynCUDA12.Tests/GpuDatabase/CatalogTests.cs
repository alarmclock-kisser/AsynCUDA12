using System;
using System.Linq;
using AsynCUDA12.GpuDatabase;
using AsynCUDA12.Runtime;
using ManagedCuda.BasicTypes;
using Shouldly;

namespace AsynCUDA12.Tests.GpuDatabase
{
    /// <summary>
    /// CPU-only tests for the VRAM catalog model (<see cref="GpuColumn"/>, <see cref="GpuStringColumn"/>,
    /// <see cref="GpuTable"/>, <see cref="TableCatalog"/>). No CUDA device is required: backing
    /// <see cref="CudaMem"/> descriptors are constructed with dummy device handles so the metadata,
    /// lookup, enumeration and registry logic can be exercised without any GPU call.
    /// </summary>
    [TestClass]
    public sealed class CatalogTests
    {
        private static GpuColumn DummyColumn(string name, GpuColumnType type = GpuColumnType.Int32, long handle = 0x1000)
        {
            CudaMem memory = new(new CUdeviceptr(handle), new IntPtr(8), typeof(int));
            return new GpuColumn(name, type, type, 8, GpuColumnTypeInfo.ElementSize(type), memory);
        }

        private static GpuStringColumn DummyStringColumn(string name)
        {
            return new GpuStringColumn(
                name,
                3,
                DummyColumn(name + ".data", GpuColumnType.Byte, 0x2000),
                DummyColumn(name + ".offsets", GpuColumnType.Int32, 0x3000),
                DummyColumn(name + ".lengths", GpuColumnType.Int32, 0x4000));
        }

        [TestMethod]
        public void GpuColumn_ExposesMetadataAndPointers()
        {
            GpuColumn column = DummyColumn("age", GpuColumnType.Int32, 0x55AA);

            column.Name.ShouldBe("age");
            column.LogicalType.ShouldBe(GpuColumnType.Int32);
            column.ElementSize.ShouldBe(4);
            column.ElementCount.ShouldBe(8);
            column.IndexPointer.ShouldBe((nint) 0x55AA);
        }

        [TestMethod]
        public void GpuTable_AddAndGetColumn_IsCaseInsensitive()
        {
            GpuTable table = new("t", 8);
            table.AddColumn(DummyColumn("Age"));

            table.GetColumn("age").ShouldNotBeNull();
            table.GetColumn("AGE").ShouldNotBeNull();
            table.GetColumn("missing").ShouldBeNull();
        }

        [TestMethod]
        public void GpuTable_StringColumns_AreStoredSeparately()
        {
            GpuTable table = new("t", 3);
            table.AddStringColumn(DummyStringColumn("name"));

            table.GetStringColumn("name").ShouldNotBeNull();
            table.GetColumn("name").ShouldBeNull();
            table.StringColumns.Count.ShouldBe(1);
        }

        [TestMethod]
        public void GpuTable_EnumerateBackingColumns_IncludesStringComponents()
        {
            GpuTable table = new("t", 3);
            table.AddColumn(DummyColumn("id"));
            table.AddStringColumn(DummyStringColumn("name"));

            string[] names = table.EnumerateBackingColumns().Select(c => c.Name).ToArray();

            names.ShouldContain("id");
            names.ShouldContain("name.data");
            names.ShouldContain("name.offsets");
            names.ShouldContain("name.lengths");
            names.Length.ShouldBe(4);
        }

        [TestMethod]
        public void TableCatalog_AddGetRemoveClear_BehaveAsExpected()
        {
            TableCatalog catalog = new();
            GpuTable table = new("people", 8);
            table.AddColumn(DummyColumn("id"));
            catalog.Add(table);

            catalog.Count.ShouldBe(1);
            catalog.GetTable("PEOPLE").ShouldNotBeNull();
            catalog.ResolveColumn("people", "id").ShouldNotBeNull();
            catalog.ResolveColumn("people", "missing").ShouldBeNull();
            catalog.ResolveColumn("missing", "id").ShouldBeNull();

            catalog.Remove("people").ShouldBeTrue();
            catalog.Remove("people").ShouldBeFalse();
            catalog.Count.ShouldBe(0);
        }

        [TestMethod]
        public void TableCatalog_Clear_RemovesAllTables()
        {
            TableCatalog catalog = new();
            catalog.Add(new GpuTable("a", 0));
            catalog.Add(new GpuTable("b", 0));

            catalog.Clear();

            catalog.Count.ShouldBe(0);
            catalog.Tables.Count.ShouldBe(0);
        }

        [TestMethod]
        public void GpuStringColumn_ExposesComponents()
        {
            GpuStringColumn column = DummyStringColumn("name");

            column.RowCount.ShouldBe(3);
            column.ByteData.Name.ShouldBe("name.data");
            column.Offsets.Name.ShouldBe("name.offsets");
            column.Lengths.Name.ShouldBe("name.lengths");
        }
    }
}

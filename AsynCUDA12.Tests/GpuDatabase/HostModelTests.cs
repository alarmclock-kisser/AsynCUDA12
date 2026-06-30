using System;
using AsynCUDA12.GpuDatabase;
using Shouldly;

namespace AsynCUDA12.Tests.GpuDatabase
{
    /// <summary>
    /// CPU-only tests for the host model (<see cref="HostColumn"/>, <see cref="HostTable"/>,
    /// <see cref="HostDatabase"/>) and the <see cref="GpuColumnTypeInfo"/> helpers.
    /// </summary>
    [TestClass]
    public sealed class HostModelTests
    {
        [TestMethod]
        public void HostColumn_Numeric_ExposesTypeRowCountAndData()
        {
            HostColumn column = HostColumn.Numeric("age", GpuColumnType.Int32, new[] { 1, 2, 3 });

            column.Name.ShouldBe("age");
            column.Type.ShouldBe(GpuColumnType.Int32);
            column.RowCount.ShouldBe(3);
            column.AsArray<int>().ShouldBe(new[] { 1, 2, 3 });
        }

        [TestMethod]
        public void HostColumn_Numeric_WithStringType_Throws()
        {
            Should.Throw<ArgumentException>(() => HostColumn.Numeric("x", GpuColumnType.String, new[] { 1 }));
        }

        [TestMethod]
        public void HostColumn_Strings_ExposesStringData()
        {
            HostColumn column = HostColumn.Strings("name", new[] { "a", "b" });

            column.Type.ShouldBe(GpuColumnType.String);
            column.RowCount.ShouldBe(2);
            column.AsStrings().ShouldBe(new[] { "a", "b" });
        }

        [TestMethod]
        public void HostTable_Add_IsFluentAndKeepsOrder()
        {
            HostTable table = new("t", 2);

            HostTable returned = table
                .Add(HostColumn.Numeric("a", GpuColumnType.Int32, new[] { 1, 2 }))
                .Add(HostColumn.Strings("b", new[] { "x", "y" }));

            returned.ShouldBeSameAs(table);
            table.Columns.Count.ShouldBe(2);
            table.Columns[0].Name.ShouldBe("a");
            table.Columns[1].Name.ShouldBe("b");
        }

        [TestMethod]
        public void HostDatabase_Add_IsFluentAndCollectsTables()
        {
            HostDatabase database = new();

            HostDatabase returned = database
                .Add(new HostTable("t1", 0))
                .Add(new HostTable("t2", 0));

            returned.ShouldBeSameAs(database);
            database.Tables.Count.ShouldBe(2);
            database.Tables[0].Name.ShouldBe("t1");
        }

        [TestMethod]
        public void GpuColumnTypeInfo_ElementSize_MatchesUnmanagedSizes()
        {
            GpuColumnTypeInfo.ElementSize(GpuColumnType.Int32).ShouldBe(4);
            GpuColumnTypeInfo.ElementSize(GpuColumnType.Int64).ShouldBe(8);
            GpuColumnTypeInfo.ElementSize(GpuColumnType.Single).ShouldBe(4);
            GpuColumnTypeInfo.ElementSize(GpuColumnType.Double).ShouldBe(8);
            GpuColumnTypeInfo.ElementSize(GpuColumnType.Byte).ShouldBe(1);
            GpuColumnTypeInfo.ElementSize(GpuColumnType.String).ShouldBe(1);
        }

        [TestMethod]
        public void GpuColumnTypeInfo_IsNumeric_TrueForAllButString()
        {
            GpuColumnTypeInfo.IsNumeric(GpuColumnType.Int32).ShouldBeTrue();
            GpuColumnTypeInfo.IsNumeric(GpuColumnType.Double).ShouldBeTrue();
            GpuColumnTypeInfo.IsNumeric(GpuColumnType.Byte).ShouldBeTrue();
            GpuColumnTypeInfo.IsNumeric(GpuColumnType.String).ShouldBeFalse();
        }
    }
}

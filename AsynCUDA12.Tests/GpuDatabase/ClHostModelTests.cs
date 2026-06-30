using AsynCUDA.ClDatabase;
using Shouldly;

namespace AsynCUDA12.Tests.GpuDatabase
{
    /// <summary>
    /// CPU-only tests for the OpenCL host model (<see cref="ClHostColumn"/>, <see cref="ClHostTable"/>,
    /// <see cref="ClHostDatabase"/>) and the <see cref="ClColumnTypeInfo"/> helpers.
    /// </summary>
    [TestClass]
    public sealed class ClHostModelTests
    {
        [TestMethod]
        public void ClHostColumn_Numeric_ExposesTypeRowCountAndData()
        {
            ClHostColumn column = ClHostColumn.Numeric("age", ClColumnType.Int32, new[] { 1, 2, 3 });

            column.Name.ShouldBe("age");
            column.Type.ShouldBe(ClColumnType.Int32);
            column.RowCount.ShouldBe(3);
            column.AsArray<int>().ShouldBe(new[] { 1, 2, 3 });
        }

        [TestMethod]
        public void ClHostColumn_Numeric_WithStringType_Throws()
        {
            Should.Throw<System.ArgumentException>(() => ClHostColumn.Numeric("x", ClColumnType.String, new[] { 1 }));
        }

        [TestMethod]
        public void ClHostColumn_Strings_ExposesStringData()
        {
            ClHostColumn column = ClHostColumn.Strings("name", new[] { "a", "b" });

            column.Type.ShouldBe(ClColumnType.String);
            column.RowCount.ShouldBe(2);
            column.AsStrings().ShouldBe(new[] { "a", "b" });
        }

        [TestMethod]
        public void ClHostTable_Add_IsFluentAndKeepsOrder()
        {
            ClHostTable table = new("t", 2);

            ClHostTable returned = table
                .Add(ClHostColumn.Numeric("a", ClColumnType.Int32, new[] { 1, 2 }))
                .Add(ClHostColumn.Strings("b", new[] { "x", "y" }));

            returned.ShouldBeSameAs(table);
            table.Columns.Count.ShouldBe(2);
            table.Columns[0].Name.ShouldBe("a");
            table.Columns[1].Name.ShouldBe("b");
        }

        [TestMethod]
        public void ClHostDatabase_Add_IsFluentAndCollectsTables()
        {
            ClHostDatabase database = new();

            ClHostDatabase returned = database
                .Add(new ClHostTable("t1", 0))
                .Add(new ClHostTable("t2", 0));

            returned.ShouldBeSameAs(database);
            database.Tables.Count.ShouldBe(2);
            database.Tables[0].Name.ShouldBe("t1");
        }

        [TestMethod]
        public void ClColumnTypeInfo_ElementSize_MatchesUnmanagedSizes()
        {
            ClColumnTypeInfo.ElementSize(ClColumnType.Int32).ShouldBe(4);
            ClColumnTypeInfo.ElementSize(ClColumnType.Int64).ShouldBe(8);
            ClColumnTypeInfo.ElementSize(ClColumnType.Single).ShouldBe(4);
            ClColumnTypeInfo.ElementSize(ClColumnType.Double).ShouldBe(8);
            ClColumnTypeInfo.ElementSize(ClColumnType.Byte).ShouldBe(1);
            ClColumnTypeInfo.ElementSize(ClColumnType.String).ShouldBe(1);
        }

        [TestMethod]
        public void ClColumnTypeInfo_IsNumeric_TrueForAllButString()
        {
            ClColumnTypeInfo.IsNumeric(ClColumnType.Int32).ShouldBeTrue();
            ClColumnTypeInfo.IsNumeric(ClColumnType.Double).ShouldBeTrue();
            ClColumnTypeInfo.IsNumeric(ClColumnType.Byte).ShouldBeTrue();
            ClColumnTypeInfo.IsNumeric(ClColumnType.String).ShouldBeFalse();
        }
    }
}

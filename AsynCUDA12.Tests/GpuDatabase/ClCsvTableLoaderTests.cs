using System;
using System.IO;
using AsynCUDA.ClDatabase;
using Shouldly;

namespace AsynCUDA12.Tests.GpuDatabase
{
    /// <summary>
    /// CPU-only tests for <see cref="ClCsvTableLoader"/>: header handling, automatic type inference
    /// (Int32 → Int64 → Double → String), explicit schema, quoting/embedded delimiters/newlines,
    /// custom delimiters, headerless input, empty cells, file loading and error cases.
    /// </summary>
    [TestClass]
    public sealed class ClCsvTableLoaderTests
    {
        private readonly ClCsvTableLoader loader = new();

        [TestMethod]
        public void LoadText_WithHeader_UsesHeaderNamesAndRowCount()
        {
            ClHostTable table = this.loader.LoadText(TestData.BuildCsv(), "people");

            table.Name.ShouldBe("people");
            table.RowCount.ShouldBe(3);
            table.Columns.Count.ShouldBe(4);
            table.Columns[0].Name.ShouldBe("id");
            table.Columns[2].Name.ShouldBe("name");
        }

        [TestMethod]
        public void LoadText_InfersIntColumn()
        {
            ClHostTable table = this.loader.LoadText(TestData.BuildCsv(), "people");

            table.Columns[0].Type.ShouldBe(ClColumnType.Int32);
            table.Columns[0].AsArray<int>().ShouldBe(new[] { 1, 2, 3 });
        }

        [TestMethod]
        public void LoadText_InfersDoubleColumn()
        {
            ClHostTable table = this.loader.LoadText(TestData.BuildCsv(), "people");

            table.Columns[3].Type.ShouldBe(ClColumnType.Double);
            table.Columns[3].AsArray<double>().ShouldBe(new[] { 100.5, 200.5, 300.5 });
        }

        [TestMethod]
        public void LoadText_InfersStringColumn()
        {
            ClHostTable table = this.loader.LoadText(TestData.BuildCsv(), "people");

            table.Columns[2].Type.ShouldBe(ClColumnType.String);
            table.Columns[2].AsStrings().ShouldBe(new[] { "Alice", "Bob", "Carol" });
        }

        [TestMethod]
        public void LoadText_LargeIntegers_InfersInt64()
        {
            string csv = "v\n3000000000\n4000000000";

            ClHostTable table = this.loader.LoadText(csv, "t");

            table.Columns[0].Type.ShouldBe(ClColumnType.Int64);
            table.Columns[0].AsArray<long>().ShouldBe(new[] { 3000000000L, 4000000000L });
        }

        [TestMethod]
        public void LoadText_QuotedFieldsWithDelimiterAndQuotes_ParsedCorrectly()
        {
            string csv = "name,note\n\"Doe, John\",\"says \"\"hi\"\"\"\n\"Plain\",\"x\"";

            ClHostTable table = this.loader.LoadText(csv, "t");

            table.Columns[0].AsStrings().ShouldBe(new[] { "Doe, John", "Plain" });
            table.Columns[1].AsStrings().ShouldBe(new[] { "says \"hi\"", "x" });
        }

        [TestMethod]
        public void LoadText_EmbeddedNewlineInsideQuotes_KeptInSameField()
        {
            string csv = "name\n\"line1\nline2\"";

            ClHostTable table = this.loader.LoadText(csv, "t");

            table.RowCount.ShouldBe(1);
            table.Columns[0].AsStrings()[0].ShouldBe("line1\nline2");
        }

        [TestMethod]
        public void LoadText_CustomDelimiter_Semicolon()
        {
            ClCsvTableLoader semi = new(new ClCsvOptions { Delimiter = ';' });

            ClHostTable table = semi.LoadText("a;b\n1;2", "t");

            table.Columns.Count.ShouldBe(2);
            table.Columns[1].AsArray<int>().ShouldBe(new[] { 2 });
        }

        [TestMethod]
        public void LoadText_NoHeader_GeneratesColumnNames()
        {
            ClCsvTableLoader noHeader = new(new ClCsvOptions { HasHeader = false });

            ClHostTable table = noHeader.LoadText("1,2,3\n4,5,6", "t");

            table.Columns[0].Name.ShouldBe("col0");
            table.Columns[2].Name.ShouldBe("col2");
            table.RowCount.ShouldBe(2);
        }

        [TestMethod]
        public void LoadText_ExplicitSchema_OverridesInference()
        {
            ClColumnType[] schema = { ClColumnType.Single, ClColumnType.String };

            ClHostTable table = this.loader.LoadText("a,b\n1,2", schema, "t");

            table.Columns[0].Type.ShouldBe(ClColumnType.Single);
            table.Columns[0].AsArray<float>().ShouldBe(new[] { 1.0f });
            table.Columns[1].Type.ShouldBe(ClColumnType.String);
            table.Columns[1].AsStrings().ShouldBe(new[] { "2" });
        }

        [TestMethod]
        public void LoadText_SchemaColumnCountMismatch_Throws()
        {
            ClColumnType[] schema = { ClColumnType.Int32 };

            Should.Throw<ArgumentException>(() => this.loader.LoadText("a,b\n1,2", schema, "t"));
        }

        [TestMethod]
        public void LoadText_MissingTrailingCells_FilledAsEmpty()
        {
            ClHostTable table = this.loader.LoadText("a,b,c\n1", "t");

            table.Columns.Count.ShouldBe(3);
            table.Columns[1].Type.ShouldBe(ClColumnType.String);
            table.Columns[1].AsStrings().ShouldBe(new[] { "" });
        }

        [TestMethod]
        public void LoadText_Empty_ProducesEmptyTable()
        {
            ClHostTable table = this.loader.LoadText(string.Empty, "t");

            table.Columns.Count.ShouldBe(0);
            table.RowCount.ShouldBe(0);
        }

        [TestMethod]
        public void LoadFile_AutoInference_ReadsFromDisk()
        {
            string path = TestData.TempFile(".csv");
            File.WriteAllText(path, TestData.BuildCsv());

            ClHostTable table = this.loader.LoadFile(path);

            table.Name.ShouldBe(Path.GetFileNameWithoutExtension(path));
            table.RowCount.ShouldBe(3);
        }

        [TestMethod]
        public void LoadFile_WithSchema_ReadsFromDisk()
        {
            string path = TestData.TempFile(".csv");
            File.WriteAllText(path, "a,b\n1,2");
            ClColumnType[] schema = { ClColumnType.Int32, ClColumnType.Int32 };

            ClHostTable table = this.loader.LoadFile(path, schema, "custom");

            table.Name.ShouldBe("custom");
            table.Columns[1].AsArray<int>().ShouldBe(new[] { 2 });
        }

        [TestMethod]
        public void LoadText_CrlfLineEndings_Handled()
        {
            ClHostTable table = this.loader.LoadText("a,b\r\n1,2\r\n3,4", "t");

            table.RowCount.ShouldBe(2);
            table.Columns[0].AsArray<int>().ShouldBe(new[] { 1, 3 });
        }
    }
}

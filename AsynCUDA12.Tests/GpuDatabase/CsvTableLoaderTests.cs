using System;
using System.Globalization;
using System.IO;
using AsynCUDA12.GpuDatabase;
using Shouldly;

namespace AsynCUDA12.Tests.GpuDatabase
{
    /// <summary>
    /// CPU-only tests for <see cref="CsvTableLoader"/>: header handling, automatic type inference
    /// (Int32 → Int64 → Double → String), explicit schema, quoting/embedded delimiters/newlines,
    /// custom delimiters, headerless input, empty cells, file loading and error cases.
    /// </summary>
    [TestClass]
    public sealed class CsvTableLoaderTests
    {
        private readonly CsvTableLoader loader = new();

        [TestMethod]
        public void LoadText_WithHeader_UsesHeaderNamesAndRowCount()
        {
            HostTable table = this.loader.LoadText(TestData.BuildCsv(), "people");

            table.Name.ShouldBe("people");
            table.RowCount.ShouldBe(3);
            table.Columns.Count.ShouldBe(4);
            table.Columns[0].Name.ShouldBe("id");
            table.Columns[2].Name.ShouldBe("name");
        }

        [TestMethod]
        public void LoadText_InfersIntColumn()
        {
            HostTable table = this.loader.LoadText(TestData.BuildCsv(), "people");

            table.Columns[0].Type.ShouldBe(GpuColumnType.Int32);
            table.Columns[0].AsArray<int>().ShouldBe(new[] { 1, 2, 3 });
        }

        [TestMethod]
        public void LoadText_InfersDoubleColumn()
        {
            HostTable table = this.loader.LoadText(TestData.BuildCsv(), "people");

            table.Columns[3].Type.ShouldBe(GpuColumnType.Double);
            table.Columns[3].AsArray<double>().ShouldBe(new[] { 100.5, 200.5, 300.5 });
        }

        [TestMethod]
        public void LoadText_InfersStringColumn()
        {
            HostTable table = this.loader.LoadText(TestData.BuildCsv(), "people");

            table.Columns[2].Type.ShouldBe(GpuColumnType.String);
            table.Columns[2].AsStrings().ShouldBe(new[] { "Alice", "Bob", "Carol" });
        }

        [TestMethod]
        public void LoadText_LargeIntegers_InfersInt64()
        {
            string csv = "v\n3000000000\n4000000000";

            HostTable table = this.loader.LoadText(csv, "t");

            table.Columns[0].Type.ShouldBe(GpuColumnType.Int64);
            table.Columns[0].AsArray<long>().ShouldBe(new[] { 3000000000L, 4000000000L });
        }

        [TestMethod]
        public void LoadText_QuotedFieldsWithDelimiterAndQuotes_ParsedCorrectly()
        {
            string csv = "name,note\n\"Doe, John\",\"says \"\"hi\"\"\"\n\"Plain\",\"x\"";

            HostTable table = this.loader.LoadText(csv, "t");

            table.Columns[0].AsStrings().ShouldBe(new[] { "Doe, John", "Plain" });
            table.Columns[1].AsStrings().ShouldBe(new[] { "says \"hi\"", "x" });
        }

        [TestMethod]
        public void LoadText_EmbeddedNewlineInsideQuotes_KeptInSameField()
        {
            string csv = "name\n\"line1\nline2\"";

            HostTable table = this.loader.LoadText(csv, "t");

            table.RowCount.ShouldBe(1);
            table.Columns[0].AsStrings()[0].ShouldBe("line1\nline2");
        }

        [TestMethod]
        public void LoadText_CustomDelimiter_Semicolon()
        {
            CsvTableLoader semi = new(new CsvOptions { Delimiter = ';' });

            HostTable table = semi.LoadText("a;b\n1;2", "t");

            table.Columns.Count.ShouldBe(2);
            table.Columns[1].AsArray<int>().ShouldBe(new[] { 2 });
        }

        [TestMethod]
        public void LoadText_NoHeader_GeneratesColumnNames()
        {
            CsvTableLoader noHeader = new(new CsvOptions { HasHeader = false });

            HostTable table = noHeader.LoadText("1,2,3\n4,5,6", "t");

            table.Columns[0].Name.ShouldBe("col0");
            table.Columns[2].Name.ShouldBe("col2");
            table.RowCount.ShouldBe(2);
        }

        [TestMethod]
        public void LoadText_ExplicitSchema_OverridesInference()
        {
            GpuColumnType[] schema = { GpuColumnType.Single, GpuColumnType.String };

            HostTable table = this.loader.LoadText("a,b\n1,2", schema, "t");

            table.Columns[0].Type.ShouldBe(GpuColumnType.Single);
            table.Columns[0].AsArray<float>().ShouldBe(new[] { 1.0f });
            table.Columns[1].Type.ShouldBe(GpuColumnType.String);
            table.Columns[1].AsStrings().ShouldBe(new[] { "2" });
        }

        [TestMethod]
        public void LoadText_SchemaColumnCountMismatch_Throws()
        {
            GpuColumnType[] schema = { GpuColumnType.Int32 };

            Should.Throw<ArgumentException>(() => this.loader.LoadText("a,b\n1,2", schema, "t"));
        }

        [TestMethod]
        public void LoadText_MissingTrailingCells_FilledAsEmpty()
        {
            HostTable table = this.loader.LoadText("a,b,c\n1", "t");

            table.Columns.Count.ShouldBe(3);
            table.Columns[1].Type.ShouldBe(GpuColumnType.String);
            table.Columns[1].AsStrings().ShouldBe(new[] { "" });
        }

        [TestMethod]
        public void LoadText_Empty_ProducesEmptyTable()
        {
            HostTable table = this.loader.LoadText(string.Empty, "t");

            table.Columns.Count.ShouldBe(0);
            table.RowCount.ShouldBe(0);
        }

        [TestMethod]
        public void LoadFile_AutoInference_ReadsFromDisk()
        {
            string path = TestData.TempFile(".csv");
            File.WriteAllText(path, TestData.BuildCsv());

            HostTable table = this.loader.LoadFile(path);

            table.Name.ShouldBe(Path.GetFileNameWithoutExtension(path));
            table.RowCount.ShouldBe(3);
        }

        [TestMethod]
        public void LoadFile_WithSchema_ReadsFromDisk()
        {
            string path = TestData.TempFile(".csv");
            File.WriteAllText(path, "a,b\n1,2");
            GpuColumnType[] schema = { GpuColumnType.Int32, GpuColumnType.Int32 };

            HostTable table = this.loader.LoadFile(path, schema, "custom");

            table.Name.ShouldBe("custom");
            table.Columns[1].AsArray<int>().ShouldBe(new[] { 2 });
        }

        [TestMethod]
        public void LoadText_CrlfLineEndings_Handled()
        {
            HostTable table = this.loader.LoadText("a,b\r\n1,2\r\n3,4", "t");

            table.RowCount.ShouldBe(2);
            table.Columns[0].AsArray<int>().ShouldBe(new[] { 1, 3 });
        }
    }
}

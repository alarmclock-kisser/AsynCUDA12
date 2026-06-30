using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace AsynCUDA12.GpuDatabase
{
    /// <summary>
    /// Parses delimited text (CSV/TXT) into a host-side <see cref="HostTable"/> without touching the GPU.
    /// The parser is RFC-4180-friendly: it honours quoting, embedded delimiters, embedded quotes
    /// (doubled), and embedded newlines inside quoted fields. Column types are either inferred
    /// automatically (Int32 → Int64 → Double, otherwise String) or taken from an explicit schema.
    /// </summary>
    public sealed class CsvTableLoader
    {
        private readonly CsvOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="CsvTableLoader"/> class.
        /// </summary>
        /// <param name="options">The parsing options; <see cref="CsvOptions.Default"/> when <c>null</c>.</param>
        public CsvTableLoader(CsvOptions? options = null)
        {
            this.options = options ?? CsvOptions.Default;
        }

        /// <summary>
        /// Loads a table from a CSV/TXT file using automatic type inference.
        /// </summary>
        /// <param name="path">The file path to read.</param>
        /// <param name="tableName">The resulting table name; the file name (without extension) when <c>null</c>.</param>
        /// <returns>The parsed <see cref="HostTable"/>.</returns>
        public HostTable LoadFile(string path, string? tableName = null)
        {
            string text = File.ReadAllText(path, Encoding.UTF8);
            string name = tableName ?? Path.GetFileNameWithoutExtension(path);
            return this.LoadText(text, name);
        }

        /// <summary>
        /// Loads a table from a CSV/TXT file using an explicit per-column schema.
        /// </summary>
        /// <param name="path">The file path to read.</param>
        /// <param name="schema">The explicit column types (one per column).</param>
        /// <param name="tableName">The resulting table name; the file name (without extension) when <c>null</c>.</param>
        /// <returns>The parsed <see cref="HostTable"/>.</returns>
        public HostTable LoadFile(string path, IReadOnlyList<GpuColumnType> schema, string? tableName = null)
        {
            string text = File.ReadAllText(path, Encoding.UTF8);
            string name = tableName ?? Path.GetFileNameWithoutExtension(path);
            return this.LoadText(text, schema, name);
        }

        /// <summary>
        /// Loads a table from in-memory CSV/TXT text using automatic type inference.
        /// </summary>
        /// <param name="text">The delimited text.</param>
        /// <param name="tableName">The resulting table name.</param>
        /// <returns>The parsed <see cref="HostTable"/>.</returns>
        public HostTable LoadText(string text, string tableName)
            => this.Build(text, tableName, schema: null);

        /// <summary>
        /// Loads a table from in-memory CSV/TXT text using an explicit per-column schema.
        /// </summary>
        /// <param name="text">The delimited text.</param>
        /// <param name="schema">The explicit column types (one per column).</param>
        /// <param name="tableName">The resulting table name.</param>
        /// <returns>The parsed <see cref="HostTable"/>.</returns>
        public HostTable LoadText(string text, IReadOnlyList<GpuColumnType> schema, string tableName)
            => this.Build(text, tableName, schema ?? throw new ArgumentNullException(nameof(schema)));

        // ---- Build pipeline ----

        private HostTable Build(string text, string tableName, IReadOnlyList<GpuColumnType>? schema)
        {
            List<string[]> rows = this.Tokenize(text ?? string.Empty);
            (string[] headers, int dataStart) = this.ResolveHeaders(rows);
            int columnCount = headers.Length;

            if (schema != null && schema.Count != columnCount)
            {
                throw new ArgumentException($"Schema has {schema.Count} columns but the data has {columnCount}.", nameof(schema));
            }

            int rowCount = rows.Count - dataStart;
            HostTable table = new(tableName, rowCount);
            for (int c = 0; c < columnCount; c++)
            {
                string[] cells = ExtractColumn(rows, dataStart, c, columnCount);
                GpuColumnType type = schema != null ? schema[c] : InferType(cells, this.options.Culture);
                table.Add(this.BuildColumn(headers[c], type, cells));
            }

            return table;
        }

        private (string[] headers, int dataStart) ResolveHeaders(List<string[]> rows)
        {
            if (rows.Count == 0)
            {
                return (Array.Empty<string>(), 0);
            }

            int columnCount = rows[0].Length;
            if (this.options.HasHeader)
            {
                return (rows[0], 1);
            }

            string[] generated = new string[columnCount];
            for (int i = 0; i < columnCount; i++)
            {
                generated[i] = "col" + i.ToString(CultureInfo.InvariantCulture);
            }

            return (generated, 0);
        }

        private static string[] ExtractColumn(List<string[]> rows, int dataStart, int columnIndex, int columnCount)
        {
            string[] cells = new string[rows.Count - dataStart];
            for (int r = dataStart; r < rows.Count; r++)
            {
                string[] row = rows[r];
                cells[r - dataStart] = columnIndex < row.Length ? row[columnIndex] : string.Empty;
            }

            return cells;
        }

        private HostColumn BuildColumn(string name, GpuColumnType type, string[] cells)
        {
            CultureInfo culture = this.options.Culture;
            switch (type)
            {
                case GpuColumnType.Int32:
                    return HostColumn.Numeric(name, type, ParseInts(cells, culture));
                case GpuColumnType.Int64:
                    return HostColumn.Numeric(name, type, ParseLongs(cells, culture));
                case GpuColumnType.Single:
                    return HostColumn.Numeric(name, type, ParseFloats(cells, culture));
                case GpuColumnType.Double:
                    return HostColumn.Numeric(name, type, ParseDoubles(cells, culture));
                case GpuColumnType.Byte:
                    return HostColumn.Numeric(name, type, ParseBytes(cells, culture));
                default:
                    return HostColumn.Strings(name, cells);
            }
        }

        // ---- Tokenizer (RFC-4180-friendly) ----

        private List<string[]> Tokenize(string text)
        {
            List<string[]> rows = new();
            List<string> current = new();
            StringBuilder field = new();
            bool inQuotes = false;
            bool sawAny = false;

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (inQuotes)
                {
                    i = this.ConsumeQuoted(text, i, field, ref inQuotes);
                    continue;
                }

                if (ch == this.options.Quote)
                {
                    inQuotes = true;
                    sawAny = true;
                }
                else if (ch == this.options.Delimiter)
                {
                    this.EndField(current, field);
                    sawAny = true;
                }
                else if (ch == '\n' || ch == '\r')
                {
                    i = ConsumeNewline(text, i, ch);
                    this.EndRow(rows, current, field, ref sawAny);
                }
                else
                {
                    field.Append(ch);
                    sawAny = true;
                }
            }

            if (sawAny || field.Length > 0 || current.Count > 0)
            {
                this.EndField(current, field);
                rows.Add(current.ToArray());
            }

            return rows;
        }

        private int ConsumeQuoted(string text, int index, StringBuilder field, ref bool inQuotes)
        {
            char ch = text[index];
            if (ch != this.options.Quote)
            {
                field.Append(ch);
                return index;
            }

            bool isEscapedQuote = index + 1 < text.Length && text[index + 1] == this.options.Quote;
            if (isEscapedQuote)
            {
                field.Append(this.options.Quote);
                return index + 1;
            }

            inQuotes = false;
            return index;
        }

        private static int ConsumeNewline(string text, int index, char ch)
        {
            if (ch == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
            {
                return index + 1;
            }

            return index;
        }

        private void EndField(List<string> current, StringBuilder field)
        {
            string value = field.ToString();
            if (this.options.TrimWhitespace)
            {
                value = value.Trim();
            }

            current.Add(value);
            field.Clear();
        }

        private void EndRow(List<string[]> rows, List<string> current, StringBuilder field, ref bool sawAny)
        {
            this.EndField(current, field);
            rows.Add(current.ToArray());
            current.Clear();
            sawAny = false;
        }

        // ---- Inference ----

        private static GpuColumnType InferType(string[] cells, CultureInfo culture)
        {
            bool allInt32 = true;
            bool allInt64 = true;
            bool allDouble = true;
            bool any = false;

            foreach (string cell in cells)
            {
                if (string.IsNullOrEmpty(cell))
                {
                    continue;
                }

                any = true;
                if (allInt32 && !int.TryParse(cell, NumberStyles.Integer, culture, out _)) { allInt32 = false; }
                if (allInt64 && !long.TryParse(cell, NumberStyles.Integer, culture, out _)) { allInt64 = false; }
                if (allDouble && !double.TryParse(cell, NumberStyles.Float, culture, out _)) { allDouble = false; }
            }

            if (!any) { return GpuColumnType.String; }
            if (allInt32) { return GpuColumnType.Int32; }
            if (allInt64) { return GpuColumnType.Int64; }
            if (allDouble) { return GpuColumnType.Double; }
            return GpuColumnType.String;
        }

        // ---- Numeric parsing helpers (empty cells map to 0) ----

        private static int[] ParseInts(string[] cells, CultureInfo culture)
        {
            int[] result = new int[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                int.TryParse(cells[i], NumberStyles.Integer, culture, out result[i]);
            }

            return result;
        }

        private static long[] ParseLongs(string[] cells, CultureInfo culture)
        {
            long[] result = new long[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                long.TryParse(cells[i], NumberStyles.Integer, culture, out result[i]);
            }

            return result;
        }

        private static float[] ParseFloats(string[] cells, CultureInfo culture)
        {
            float[] result = new float[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                float.TryParse(cells[i], NumberStyles.Float, culture, out result[i]);
            }

            return result;
        }

        private static double[] ParseDoubles(string[] cells, CultureInfo culture)
        {
            double[] result = new double[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                double.TryParse(cells[i], NumberStyles.Float, culture, out result[i]);
            }

            return result;
        }

        private static byte[] ParseBytes(string[] cells, CultureInfo culture)
        {
            byte[] result = new byte[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                byte.TryParse(cells[i], NumberStyles.Integer, culture, out result[i]);
            }

            return result;
        }
    }
}

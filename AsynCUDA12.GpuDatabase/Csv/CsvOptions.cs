using System.Globalization;

namespace AsynCUDA12.GpuDatabase
{
    /// <summary>
    /// Configuration for parsing delimited text (CSV/TXT) into a <see cref="HostTable"/>. Defaults match
    /// the most common CSV variant: a comma delimiter, a header row, double-quote quoting and the
    /// invariant culture (so numeric inference is locale-independent and deterministic).
    /// </summary>
    public sealed class CsvOptions
    {
        /// <summary>Gets the shared default options (comma delimiter, header row, invariant culture).</summary>
        public static CsvOptions Default { get; } = new();

        /// <summary>Gets or sets the field delimiter character. Default is <c>,</c>.</summary>
        public char Delimiter { get; set; } = ',';

        /// <summary>Gets or sets the quote character used to wrap fields. Default is <c>"</c>.</summary>
        public char Quote { get; set; } = '"';

        /// <summary>
        /// Gets or sets a value indicating whether the first non-empty line is a header row that
        /// supplies column names. When <c>false</c>, columns are named <c>col0</c>, <c>col1</c>, …
        /// Default is <c>true</c>.
        /// </summary>
        public bool HasHeader { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether leading/trailing whitespace is trimmed from each
        /// unquoted field before inference/storage. Default is <c>true</c>.
        /// </summary>
        public bool TrimWhitespace { get; set; } = true;

        /// <summary>
        /// Gets or sets the culture used for numeric parsing/inference. Default is
        /// <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;
    }
}

namespace AsynCUDA.ClDatabase
{
    /// <summary>
    /// The result of executing a <see cref="ClQuery"/>. Depending on the query, it carries the row
    /// selection mask, the matched row count, and/or the computed aggregate value(s). <see cref="Success"/>
    /// indicates whether the OpenCL pipeline completed; <see cref="Error"/> describes the failure otherwise.
    /// </summary>
    public sealed class ClQueryResult
    {
        /// <summary>Gets a value indicating whether the query executed successfully.</summary>
        public bool Success { get; init; }

        /// <summary>Gets the error message when <see cref="Success"/> is <c>false</c>.</summary>
        public string? Error { get; init; }

        /// <summary>Gets the per-row selection mask (1 = selected), or <c>null</c> when not produced.</summary>
        public byte[]? Mask { get; init; }

        /// <summary>Gets the number of selected rows, or <c>null</c> when not computed.</summary>
        public int? Count { get; init; }

        /// <summary>Gets the aggregate sum/average result, or <c>null</c> when not computed.</summary>
        public double? Value { get; init; }

        /// <summary>Gets the minimum value, or <c>null</c> when not computed.</summary>
        public double? Min { get; init; }

        /// <summary>Gets the maximum value, or <c>null</c> when not computed.</summary>
        public double? Max { get; init; }

        /// <summary>Creates a failed result with the given error message.</summary>
        /// <param name="error">The failure description.</param>
        /// <returns>A failed <see cref="ClQueryResult"/>.</returns>
        public static ClQueryResult Fail(string error) => new() { Success = false, Error = error };
    }
}

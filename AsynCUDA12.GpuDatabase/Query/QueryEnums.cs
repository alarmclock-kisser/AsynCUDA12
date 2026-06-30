namespace AsynCUDA12.GpuDatabase
{
    /// <summary>
    /// The comparison/predicate kinds supported by a <see cref="GpuQuery"/> filter. These map directly
    /// onto the available filter kernels (equality and range for numeric columns; contains and bounded
    /// fuzzy match for string columns).
    /// </summary>
    public enum QueryPredicate
    {
        /// <summary>No filter; every row is selected.</summary>
        None,

        /// <summary>Integer equality (<c>filter_int_equals</c>).</summary>
        IntEquals,

        /// <summary>Inclusive integer range (<c>filter_int_range</c>).</summary>
        IntRange,

        /// <summary>Inclusive float range (<c>filter_float_range</c>).</summary>
        FloatRange,

        /// <summary>Substring/byte-pattern match on a string column (<c>string_contains</c>).</summary>
        StringContains,

        /// <summary>Bounded Levenshtein match on a string column (<c>string_fuzzy_levenshtein_limited</c>).</summary>
        StringFuzzy
    }

    /// <summary>
    /// The aggregate to compute over the rows selected by a <see cref="GpuQuery"/>. These map onto the
    /// reduction kernels (count, sum, min/max) and a host-side average derived from sum/count.
    /// </summary>
    public enum QueryAggregate
    {
        /// <summary>No aggregate; the query returns the selection mask / matching rows.</summary>
        None,

        /// <summary>Count of selected rows (<c>count_mask</c>).</summary>
        Count,

        /// <summary>Sum of a float column over selected rows (<c>sum_float_by_mask</c>).</summary>
        Sum,

        /// <summary>Average of a float column over selected rows (sum / count).</summary>
        Average,

        /// <summary>Minimum and maximum of a float column (<c>min_max_float</c>, ignores the mask).</summary>
        MinMax
    }
}

using System.Collections.Generic;

namespace AsynCUDA12.GpuDatabase
{
    /// <summary>
    /// Canonical names of the CUDA kernels shipped with the GPU database. Each name matches both the
    /// <c>.cu</c> source file (in the runtime's <c>Kernels/CU</c> folder) and the <c>extern "C"</c>
    /// kernel function name, so the runtime <c>CudaCompiler</c> can discover, compile and load them.
    /// </summary>
    public static class KernelNames
    {
        /// <summary>Filters an int column for equality with a value, writing a 0/1 mask.</summary>
        public const string FilterIntEquals = "filter_int_equals";

        /// <summary>Filters an int column for an inclusive range, writing a 0/1 mask.</summary>
        public const string FilterIntRange = "filter_int_range";

        /// <summary>Filters a float column for an inclusive range, writing a 0/1 mask.</summary>
        public const string FilterFloatRange = "filter_float_range";

        /// <summary>Copies masked float values into an output buffer (0 where unmasked).</summary>
        public const string ProjectByMask = "project_by_mask";

        /// <summary>Counts the set entries of a byte mask into a single int result.</summary>
        public const string CountMask = "count_mask";

        /// <summary>Sums masked float values into a single float result.</summary>
        public const string SumFloatByMask = "sum_float_by_mask";

        /// <summary>Computes the minimum and maximum of a float column.</summary>
        public const string MinMaxFloat = "min_max_float";

        /// <summary>Marks rows whose string contains a byte pattern.</summary>
        public const string StringContains = "string_contains";

        /// <summary>Marks rows whose string is within a bounded Levenshtein distance of a pattern.</summary>
        public const string StringFuzzyLevenshteinLimited = "string_fuzzy_levenshtein_limited";

        /// <summary>Builds a GPU open-addressing hash index over an int key column.</summary>
        public const string BuildHashIndexInt = "build_hash_index_int";

        /// <summary>Probes a GPU hash index to join on int keys.</summary>
        public const string HashJoinInt = "hash_join_int";

        /// <summary>Applies a simple arithmetic transform (add/sub/mul/div) to a float column in place.</summary>
        public const string ApplyArithmeticFloat = "apply_arithmetic_float";

        /// <summary>Gets all kernel names provided by the GPU database.</summary>
        public static IReadOnlyList<string> All { get; } = new[]
        {
            FilterIntEquals,
            FilterIntRange,
            FilterFloatRange,
            ProjectByMask,
            CountMask,
            SumFloatByMask,
            MinMaxFloat,
            StringContains,
            StringFuzzyLevenshteinLimited,
            BuildHashIndexInt,
            HashJoinInt,
            ApplyArithmeticFloat
        };
    }
}

namespace AsynCUDA.ClDatabase
{
    /// <summary>
    /// A small, strongly-typed description of a single-table query: a table name, an optional predicate
    /// over one column, and an optional aggregate over a (numeric) column. This is deliberately not a
    /// full SQL parser (that is an optional future extension); it is the structured plan the
    /// <see cref="ClQueryEngine"/> translates into an OpenCL kernel pipeline.
    /// </summary>
    public sealed class ClQuery
    {
        /// <summary>Gets or sets the source table name.</summary>
        public string Table { get; set; } = string.Empty;

        /// <summary>Gets or sets the predicate kind applied as the row filter.</summary>
        public ClQueryPredicate Predicate { get; set; } = ClQueryPredicate.None;

        /// <summary>Gets or sets the column the predicate is evaluated against.</summary>
        public string PredicateColumn { get; set; } = string.Empty;

        /// <summary>Gets or sets the lower bound / equality value (range and equality predicates).</summary>
        public double PredicateMin { get; set; }

        /// <summary>Gets or sets the upper bound (range predicates).</summary>
        public double PredicateMax { get; set; }

        /// <summary>Gets or sets the search pattern (string predicates).</summary>
        public string PredicatePattern { get; set; } = string.Empty;

        /// <summary>Gets or sets the maximum edit distance (fuzzy string predicate).</summary>
        public int FuzzyMaxDistance { get; set; } = 2;

        /// <summary>Gets or sets the aggregate kind to compute over the selected rows.</summary>
        public ClQueryAggregate Aggregate { get; set; } = ClQueryAggregate.None;

        /// <summary>Gets or sets the column the aggregate is computed over.</summary>
        public string AggregateColumn { get; set; } = string.Empty;

        /// <summary>Creates a query selecting every row of a table (no filter, no aggregate).</summary>
        /// <param name="table">The table name.</param>
        /// <returns>The created <see cref="ClQuery"/>.</returns>
        public static ClQuery From(string table) => new() { Table = table };

        /// <summary>Adds an inclusive integer range predicate.</summary>
        /// <param name="column">The int column to filter.</param>
        /// <param name="min">The inclusive lower bound.</param>
        /// <param name="max">The inclusive upper bound.</param>
        /// <returns>This query for fluent chaining.</returns>
        public ClQuery WhereIntBetween(string column, int min, int max)
        {
            this.Predicate = ClQueryPredicate.IntRange;
            this.PredicateColumn = column;
            this.PredicateMin = min;
            this.PredicateMax = max;
            return this;
        }

        /// <summary>Adds an integer equality predicate.</summary>
        /// <param name="column">The int column to filter.</param>
        /// <param name="value">The value to match.</param>
        /// <returns>This query for fluent chaining.</returns>
        public ClQuery WhereIntEquals(string column, int value)
        {
            this.Predicate = ClQueryPredicate.IntEquals;
            this.PredicateColumn = column;
            this.PredicateMin = value;
            return this;
        }

        /// <summary>Adds an inclusive float range predicate.</summary>
        /// <param name="column">The float column to filter.</param>
        /// <param name="min">The inclusive lower bound.</param>
        /// <param name="max">The inclusive upper bound.</param>
        /// <returns>This query for fluent chaining.</returns>
        public ClQuery WhereFloatBetween(string column, float min, float max)
        {
            this.Predicate = ClQueryPredicate.FloatRange;
            this.PredicateColumn = column;
            this.PredicateMin = min;
            this.PredicateMax = max;
            return this;
        }

        /// <summary>Adds a substring/byte-pattern predicate over a string column.</summary>
        /// <param name="column">The string column to search.</param>
        /// <param name="pattern">The pattern to look for.</param>
        /// <returns>This query for fluent chaining.</returns>
        public ClQuery WhereStringContains(string column, string pattern)
        {
            this.Predicate = ClQueryPredicate.StringContains;
            this.PredicateColumn = column;
            this.PredicatePattern = pattern;
            return this;
        }

        /// <summary>Adds a bounded fuzzy (Levenshtein) predicate over a string column.</summary>
        /// <param name="column">The string column to search.</param>
        /// <param name="pattern">The pattern to fuzzily match.</param>
        /// <param name="maxDistance">The maximum allowed edit distance.</param>
        /// <returns>This query for fluent chaining.</returns>
        public ClQuery WhereStringFuzzy(string column, string pattern, int maxDistance)
        {
            this.Predicate = ClQueryPredicate.StringFuzzy;
            this.PredicateColumn = column;
            this.PredicatePattern = pattern;
            this.FuzzyMaxDistance = maxDistance;
            return this;
        }

        /// <summary>Sets the aggregate to a count of selected rows.</summary>
        /// <returns>This query for fluent chaining.</returns>
        public ClQuery SelectCount()
        {
            this.Aggregate = ClQueryAggregate.Count;
            return this;
        }

        /// <summary>Sets the aggregate to the sum of a float column.</summary>
        /// <param name="column">The float column to sum.</param>
        /// <returns>This query for fluent chaining.</returns>
        public ClQuery SelectSum(string column)
        {
            this.Aggregate = ClQueryAggregate.Sum;
            this.AggregateColumn = column;
            return this;
        }

        /// <summary>Sets the aggregate to the average of a float column.</summary>
        /// <param name="column">The float column to average.</param>
        /// <returns>This query for fluent chaining.</returns>
        public ClQuery SelectAverage(string column)
        {
            this.Aggregate = ClQueryAggregate.Average;
            this.AggregateColumn = column;
            return this;
        }

        /// <summary>Sets the aggregate to the minimum and maximum of a float column.</summary>
        /// <param name="column">The float column to analyze.</param>
        /// <returns>This query for fluent chaining.</returns>
        public ClQuery SelectMinMax(string column)
        {
            this.Aggregate = ClQueryAggregate.MinMax;
            this.AggregateColumn = column;
            return this;
        }
    }
}

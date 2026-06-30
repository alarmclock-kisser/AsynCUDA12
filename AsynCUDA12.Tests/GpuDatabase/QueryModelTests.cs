using AsynCUDA12.GpuDatabase;
using System.Linq;
using Shouldly;

namespace AsynCUDA12.Tests.GpuDatabase
{
    /// <summary>
    /// CPU-only tests for the query object model: the fluent <see cref="GpuQuery"/> builders, the
    /// <see cref="QueryResult"/> factory, the predicate/aggregate enums and the <see cref="KernelNames"/>
    /// registry. No GPU is required because these only assemble/inspect plain data.
    /// </summary>
    [TestClass]
    public sealed class QueryModelTests
    {
        [TestMethod]
        public void From_SetsTableAndDefaults()
        {
            GpuQuery query = GpuQuery.From("people");

            query.Table.ShouldBe("people");
            query.Predicate.ShouldBe(QueryPredicate.None);
            query.Aggregate.ShouldBe(QueryAggregate.None);
        }

        [TestMethod]
        public void WhereIntEquals_Confic_PredicateAndColumn()
        {
            GpuQuery query = GpuQuery.From("t").WhereIntEquals("age", 42);

            query.Predicate.ShouldBe(QueryPredicate.IntEquals);
            query.PredicateColumn.ShouldBe("age");
            query.PredicateMin.ShouldBe(42);
        }

        [TestMethod]
        public void WhereIntBetween_SetsRange()
        {
            GpuQuery query = GpuQuery.From("t").WhereIntBetween("age", 18, 65);

            query.Predicate.ShouldBe(QueryPredicate.IntRange);
            query.PredicateMin.ShouldBe(18);
            query.PredicateMax.ShouldBe(65);
        }

        [TestMethod]
        public void WhereFloatBetween_SetsRange()
        {
            GpuQuery query = GpuQuery.From("t").WhereFloatBetween("score", 1.5f, 9.5f);

            query.Predicate.ShouldBe(QueryPredicate.FloatRange);
            query.PredicateMin.ShouldBe(1.5);
            query.PredicateMax.ShouldBe(9.5);
        }

        [TestMethod]
        public void WhereStringContains_SetsPattern()
        {
            GpuQuery query = GpuQuery.From("t").WhereStringContains("name", "ali");

            query.Predicate.ShouldBe(QueryPredicate.StringContains);
            query.PredicateColumn.ShouldBe("name");
            query.PredicatePattern.ShouldBe("ali");
        }

        [TestMethod]
        public void WhereStringFuzzy_SetsPatternAndDistance()
        {
            GpuQuery query = GpuQuery.From("t").WhereStringFuzzy("name", "alice", 2);

            query.Predicate.ShouldBe(QueryPredicate.StringFuzzy);
            query.PredicatePattern.ShouldBe("alice");
            query.FuzzyMaxDistance.ShouldBe(2);
        }

        [TestMethod]
        public void SelectAggregates_SetAggregateAndColumn()
        {
            GpuQuery.From("t").SelectCount().Aggregate.ShouldBe(QueryAggregate.Count);
            GpuQuery.From("t").SelectSum("x").Aggregate.ShouldBe(QueryAggregate.Sum);
            GpuQuery.From("t").SelectAverage("x").Aggregate.ShouldBe(QueryAggregate.Average);

            GpuQuery minMax = GpuQuery.From("t").SelectMinMax("x");
            minMax.Aggregate.ShouldBe(QueryAggregate.MinMax);
            minMax.AggregateColumn.ShouldBe("x");
        }

        [TestMethod]
        public void FluentChaining_CombinesPredicateAndAggregate()
        {
            GpuQuery query = GpuQuery.From("people").WhereIntBetween("age", 20, 40).SelectAverage("balance");

            query.Table.ShouldBe("people");
            query.Predicate.ShouldBe(QueryPredicate.IntRange);
            query.Aggregate.ShouldBe(QueryAggregate.Average);
            query.AggregateColumn.ShouldBe("balance");
        }

        [TestMethod]
        public void QueryResult_Fail_SetsErrorAndNotSuccess()
        {
            QueryResult result = QueryResult.Fail("boom");

            result.Success.ShouldBeFalse();
            result.Error.ShouldBe("boom");
        }

        [TestMethod]
        public void QueryResult_SuccessWithValues_ExposesAllFields()
        {
            QueryResult result = new()
            {
                Success = true,
                Mask = new byte[] { 1, 0, 1 },
                Count = 2,
                Value = 3.5,
                Min = 1.0,
                Max = 9.0
            };

            result.Success.ShouldBeTrue();
            result.Mask!.Length.ShouldBe(3);
            result.Count.ShouldBe(2);
            result.Value.ShouldBe(3.5);
            result.Min.ShouldBe(1.0);
            result.Max.ShouldBe(9.0);
        }

        [TestMethod]
        public void KernelNames_All_ContainsTwelveUniqueNames()
        {
            KernelNames.All.Count.ShouldBe(12);
            KernelNames.All.Distinct().Count().ShouldBe(12);
            KernelNames.All.ShouldContain(KernelNames.FilterIntEquals);
            KernelNames.All.ShouldContain(KernelNames.HashJoinInt);
            KernelNames.All.ShouldContain(KernelNames.ApplyArithmeticFloat);
        }
    }
}

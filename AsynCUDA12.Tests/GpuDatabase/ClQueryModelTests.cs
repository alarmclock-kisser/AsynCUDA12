using System.Linq;
using AsynCUDA.ClDatabase;
using Shouldly;

namespace AsynCUDA12.Tests.GpuDatabase
{
    /// <summary>
    /// CPU-only tests for the query object model: the fluent <see cref="ClQuery"/> builders, the
    /// <see cref="ClQueryResult"/> factory, the predicate/aggregate enums and the <see cref="ClKernelNames"/>
    /// registry. No GPU is required because these only assemble/inspect plain data.
    /// </summary>
    [TestClass]
    public sealed class ClQueryModelTests
    {
        [TestMethod]
        public void From_SetsTableAndDefaults()
        {
            ClQuery query = ClQuery.From("people");

            query.Table.ShouldBe("people");
            query.Predicate.ShouldBe(ClQueryPredicate.None);
            query.Aggregate.ShouldBe(ClQueryAggregate.None);
        }

        [TestMethod]
        public void WhereIntEquals_ConfiguresPredicateAndColumn()
        {
            ClQuery query = ClQuery.From("t").WhereIntEquals("age", 42);

            query.Predicate.ShouldBe(ClQueryPredicate.IntEquals);
            query.PredicateColumn.ShouldBe("age");
            query.PredicateMin.ShouldBe(42);
        }

        [TestMethod]
        public void WhereIntBetween_SetsRange()
        {
            ClQuery query = ClQuery.From("t").WhereIntBetween("age", 18, 65);

            query.Predicate.ShouldBe(ClQueryPredicate.IntRange);
            query.PredicateMin.ShouldBe(18);
            query.PredicateMax.ShouldBe(65);
        }

        [TestMethod]
        public void WhereFloatBetween_SetsRange()
        {
            ClQuery query = ClQuery.From("t").WhereFloatBetween("score", 1.5f, 9.5f);

            query.Predicate.ShouldBe(ClQueryPredicate.FloatRange);
            query.PredicateMin.ShouldBe(1.5);
            query.PredicateMax.ShouldBe(9.5);
        }

        [TestMethod]
        public void WhereStringContains_SetsPattern()
        {
            ClQuery query = ClQuery.From("t").WhereStringContains("name", "ali");

            query.Predicate.ShouldBe(ClQueryPredicate.StringContains);
            query.PredicateColumn.ShouldBe("name");
            query.PredicatePattern.ShouldBe("ali");
        }

        [TestMethod]
        public void WhereStringFuzzy_SetsPatternAndDistance()
        {
            ClQuery query = ClQuery.From("t").WhereStringFuzzy("name", "alice", 2);

            query.Predicate.ShouldBe(ClQueryPredicate.StringFuzzy);
            query.PredicatePattern.ShouldBe("alice");
            query.FuzzyMaxDistance.ShouldBe(2);
        }

        [TestMethod]
        public void SelectAggregates_SetAggregateAndColumn()
        {
            ClQuery.From("t").SelectCount().Aggregate.ShouldBe(ClQueryAggregate.Count);
            ClQuery.From("t").SelectSum("x").Aggregate.ShouldBe(ClQueryAggregate.Sum);
            ClQuery.From("t").SelectAverage("x").Aggregate.ShouldBe(ClQueryAggregate.Average);

            ClQuery minMax = ClQuery.From("t").SelectMinMax("x");
            minMax.Aggregate.ShouldBe(ClQueryAggregate.MinMax);
            minMax.AggregateColumn.ShouldBe("x");
        }

        [TestMethod]
        public void FluentChaining_CombinesPredicateAndAggregate()
        {
            ClQuery query = ClQuery.From("people").WhereIntBetween("age", 20, 40).SelectAverage("balance");

            query.Table.ShouldBe("people");
            query.Predicate.ShouldBe(ClQueryPredicate.IntRange);
            query.Aggregate.ShouldBe(ClQueryAggregate.Average);
            query.AggregateColumn.ShouldBe("balance");
        }

        [TestMethod]
        public void ClQueryResult_Fail_SetsErrorAndNotSuccess()
        {
            ClQueryResult result = ClQueryResult.Fail("boom");

            result.Success.ShouldBeFalse();
            result.Error.ShouldBe("boom");
        }

        [TestMethod]
        public void ClQueryResult_SuccessWithValues_ExposesAllFields()
        {
            ClQueryResult result = new()
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
        public void ClKernelNames_All_ContainsTwelveUniqueNames()
        {
            ClKernelNames.All.Count.ShouldBe(12);
            ClKernelNames.All.Distinct().Count().ShouldBe(12);
            ClKernelNames.All.ShouldContain(ClKernelNames.FilterIntEquals);
            ClKernelNames.All.ShouldContain(ClKernelNames.HashJoinInt);
            ClKernelNames.All.ShouldContain(ClKernelNames.ApplyArithmeticFloat);
        }
    }
}

using AsynCUDA.ClDatabase;
using Shouldly;

namespace AsynCUDA12.Tests.GpuDatabase
{
    /// <summary>
    /// CPU-only tests for <see cref="ClMemoryPolicy"/>. The budget arithmetic is exercised through the
    /// GPU-free <c>CanAllocate(long, long)</c> overload, so no <see cref="AsynCUDA.OpenCl.OpenClService"/>
    /// is constructed and the tests run on a machine without OpenCL.
    /// </summary>
    [TestClass]
    public sealed class ClMemoryPolicyTests
    {
        [TestMethod]
        public void CanAllocate_NoBudget_AlwaysTrue()
        {
            ClMemoryPolicy policy = new() { VramBudgetBytes = 0 };

            policy.CanAllocate(currentAllocatedBytes: long.MaxValue, additionalBytes: long.MaxValue).ShouldBeTrue();
        }

        [TestMethod]
        public void CanAllocate_NegativeBudget_AlwaysTrue()
        {
            ClMemoryPolicy policy = new() { VramBudgetBytes = -1 };

            policy.CanAllocate(currentAllocatedBytes: 4096, additionalBytes: 1024).ShouldBeTrue();
        }

        [TestMethod]
        public void CanAllocate_WithinBudget_True()
        {
            ClMemoryPolicy policy = new() { VramBudgetBytes = 1000 };

            policy.CanAllocate(currentAllocatedBytes: 0, additionalBytes: 1000).ShouldBeTrue();
        }

        [TestMethod]
        public void CanAllocate_ExactlyAtBudget_True()
        {
            ClMemoryPolicy policy = new() { VramBudgetBytes = 1000 };

            policy.CanAllocate(currentAllocatedBytes: 400, additionalBytes: 600).ShouldBeTrue();
        }

        [TestMethod]
        public void CanAllocate_ExceedsBudget_False()
        {
            ClMemoryPolicy policy = new() { VramBudgetBytes = 1000 };

            policy.CanAllocate(currentAllocatedBytes: 0, additionalBytes: 1001).ShouldBeFalse();
        }

        [TestMethod]
        public void CanAllocate_ExistingAllocationPushesOverBudget_False()
        {
            ClMemoryPolicy policy = new() { VramBudgetBytes = 1000 };

            policy.CanAllocate(currentAllocatedBytes: 900, additionalBytes: 200).ShouldBeFalse();
        }

        [TestMethod]
        public void ShouldKeepResident_FollowsKeepTablesHot()
        {
            new ClMemoryPolicy { KeepTablesHot = true }.ShouldKeepResident().ShouldBeTrue();
            new ClMemoryPolicy { KeepTablesHot = false }.ShouldKeepResident().ShouldBeFalse();
        }

        [TestMethod]
        public void Defaults_KeepTablesHot_IsTrue()
        {
            new ClMemoryPolicy().KeepTablesHot.ShouldBeTrue();
            new ClMemoryPolicy().ShouldKeepResident().ShouldBeTrue();
        }
    }
}

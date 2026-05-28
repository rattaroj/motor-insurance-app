using MotorInsurance.Domain.Common;
using MotorInsurance.Domain.Enums;
using MotorInsurance.Domain.StateMachines;
using Xunit;

namespace MotorInsurance.Tests;

public class PolicyStateMachineTests
{
    [Theory]
    [InlineData(PolicyStatus.Draft, PolicyStatus.Quoted, true)]
    [InlineData(PolicyStatus.Quoted, PolicyStatus.Issued, true)]
    [InlineData(PolicyStatus.Issued, PolicyStatus.Active, true)]
    [InlineData(PolicyStatus.Active, PolicyStatus.Expired, true)]
    [InlineData(PolicyStatus.Active, PolicyStatus.Cancelled, true)]
    [InlineData(PolicyStatus.Draft, PolicyStatus.Active, false)]     // skip steps
    [InlineData(PolicyStatus.Cancelled, PolicyStatus.Active, false)] // terminal
    [InlineData(PolicyStatus.Expired, PolicyStatus.Active, false)]   // terminal
    public void Transitions_follow_rules(PolicyStatus from, PolicyStatus to, bool ok)
        => Assert.Equal(ok, PolicyStateMachine.CanTransition(from, to));

    [Fact]
    public void Illegal_transition_throws()
        => Assert.Throws<InvalidStateTransitionException>(
            () => PolicyStateMachine.EnsureTransition(PolicyStatus.Cancelled, PolicyStatus.Active));
}

public class ClaimStateMachineTests
{
    [Theory]
    [InlineData(ClaimStatus.Filed, ClaimStatus.UnderReview, true)]
    [InlineData(ClaimStatus.Assessment, ClaimStatus.Approved, true)]
    [InlineData(ClaimStatus.Assessment, ClaimStatus.Rejected, true)]
    [InlineData(ClaimStatus.Approved, ClaimStatus.Paid, true)]
    [InlineData(ClaimStatus.Assessment, ClaimStatus.Paid, false)]    // must approve first
    [InlineData(ClaimStatus.Filed, ClaimStatus.Closed, false)]
    public void Transitions_follow_rules(ClaimStatus from, ClaimStatus to, bool ok)
        => Assert.Equal(ok, ClaimStateMachine.CanTransition(from, to));
}

public class SeedConsistencyTests
{
    // Guards: enum values must match the lookup rows seeded by Liquibase.
    [Fact] public void Policy_status_count() => Assert.Equal(6, Enum.GetValues<PolicyStatus>().Length);
    [Fact] public void Claim_status_count() => Assert.Equal(7, Enum.GetValues<ClaimStatus>().Length);
    [Fact] public void Payment_status_count() => Assert.Equal(4, Enum.GetValues<PaymentStatus>().Length);
}

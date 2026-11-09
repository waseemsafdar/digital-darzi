using Application.ViewModels.Admin;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Services;
using Xunit;

namespace DigitalDarzi.UnitTests.Security;

/// <summary>
/// Tests the pure subscription rule (SubscriptionResolver.Classify): what state a business is in,
/// and whether it is locked out. No database or mocks: the rule takes the shop settings as a value.
/// </summary>
public class SubscriptionStateTests
{
    private static readonly DateTime Now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    private static ShopSettings Shop(SubscriptionStatus? status, DateTime? trialEnds = null, DateTime? paidEnds = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Shop", null, "PKR", "Rs", "Asia/Karachi", true, status, trialEnds, paidEnds, null);

    // Rule: a trial that has ended drops the business to the free plan when one exists.
    // Why:  a tailor locked out of his customers' measurements leaves instead of paying; on the
    //       free plan he keeps working within limits and sees the upgrade offer.
    [Fact]
    public void Classify_EndedTrial_WithFreePlan_IsFreeAndNotLocked()
        => SubscriptionResolver.Classify(Shop(SubscriptionStatus.Trial, trialEnds: Now.AddDays(-1)), freePlanExists: true, Now)
            .Should().Be((SubscriptionStates.Free, (string?)null));

    // Rule: an ended paid period also drops to the free plan rather than locking.
    // Why:  a late renewal must not stop the shop from handing over finished orders.
    [Fact]
    public void Classify_EndedPaidPeriod_WithFreePlan_IsFreeAndNotLocked()
        => SubscriptionResolver.Classify(Shop(SubscriptionStatus.Active, paidEnds: Now.AddDays(-1)), freePlanExists: true, Now)
            .Should().Be((SubscriptionStates.Free, (string?)null));

    // Rule: with no free plan configured, an ended trial still locks the business, as before.
    // Why:  a server that has not got the free plan yet must keep the old behaviour rather than
    //       silently giving everyone unlimited use.
    [Fact]
    public void Classify_EndedTrial_WithoutFreePlan_IsLocked()
    {
        var (state, reason) = SubscriptionResolver.Classify(Shop(SubscriptionStatus.Trial, trialEnds: Now.AddDays(-1)), freePlanExists: false, Now);

        state.Should().Be(SubscriptionStates.Blocked);
        reason.Should().NotBeNullOrWhiteSpace();
    }

    // Rule: suspended or cancelled businesses are locked even when a free plan exists.
    // Why:  suspension is the platform's decision (for example abuse or a dispute); falling back
    //       to the free plan would undo it.
    [Theory]
    [InlineData(SubscriptionStatus.Suspended)]
    [InlineData(SubscriptionStatus.Cancelled)]
    public void Classify_SuspendedOrCancelled_IsLockedEvenWithFreePlan(SubscriptionStatus status)
    {
        var (state, reason) = SubscriptionResolver.Classify(Shop(status), freePlanExists: true, Now);

        state.Should().Be(SubscriptionStates.Blocked);
        reason.Should().NotBeNullOrWhiteSpace();
    }

    // Rule: a running trial or paid period is reported as such, unlocked.
    [Theory]
    [InlineData(SubscriptionStatus.Trial, SubscriptionStates.Trial)]
    [InlineData(SubscriptionStatus.Active, SubscriptionStates.Active)]
    public void Classify_RunningPeriod_IsNotLocked(SubscriptionStatus status, string expected)
        => SubscriptionResolver.Classify(Shop(status, trialEnds: Now.AddDays(3), paidEnds: Now.AddDays(3)), freePlanExists: true, Now)
            .Should().Be((expected, (string?)null));

    // Rule: a business with no subscription data is never limited or locked.
    // Why:  shops created before subscriptions existed must keep working unchanged.
    [Fact]
    public void Classify_NoSubscriptionData_IsLegacyAndNotLocked()
        => SubscriptionResolver.Classify(Shop(null), freePlanExists: false, Now)
            .Should().Be((SubscriptionStates.Legacy, (string?)null));

    // Rule: the end date itself still counts as running; only a date in the past ends the period.
    [Fact]
    public void Classify_TrialEndingExactlyNow_IsStillTrial()
        => SubscriptionResolver.Classify(Shop(SubscriptionStatus.Trial, trialEnds: Now), freePlanExists: true, Now).State
            .Should().Be(SubscriptionStates.Trial);
}

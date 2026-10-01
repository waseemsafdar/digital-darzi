using Application.Orders;
using Domain.Enums;
using FluentAssertions;
using Xunit;

namespace DigitalDarzi.UnitTests.Orders;

public class OrderStateMachineTests
{
    // ───────────────────────────────────────────────────────────────
    // CanTransition / AllowedNext
    // ───────────────────────────────────────────────────────────────

    // Rule: the workflow only allows moving through the stages a tailor shop actually uses.
    // Why:  an order that can jump straight from Pending to Delivered would let staff skip
    //       every production step entirely, corrupting the status history that the Owner
    //       relies on for "what stage is my order in" reporting. (Payment is tracked
    //       separately in PaymentStatus and is not part of this state machine at all.)
    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Received, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cutting, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Delivered, false)]
    [InlineData(OrderStatus.Cutting, OrderStatus.Stitching, true)]
    [InlineData(OrderStatus.ReadyToDeliver, OrderStatus.Delivered, true)]
    [InlineData(OrderStatus.ReadyToDeliver, OrderStatus.PartiallyDelivered, true)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Cutting, false)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Pending, false)]
    public void CanTransition_VariousPairs_MatchesWorkflow(OrderStatus from, OrderStatus to, bool expected)
        => OrderStateMachine.CanTransition(from, to).Should().Be(expected);

    [Fact]
    public void CanTransition_SameStatus_ReturnsFalse()
        => OrderStateMachine.CanTransition(OrderStatus.Cutting, OrderStatus.Cutting).Should().BeFalse();

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Received)]
    [InlineData(OrderStatus.Cutting)]
    [InlineData(OrderStatus.Stitching)]
    [InlineData(OrderStatus.Embroidery)]
    [InlineData(OrderStatus.Finishing)]
    [InlineData(OrderStatus.TrialReady)]
    [InlineData(OrderStatus.TrialDone)]
    [InlineData(OrderStatus.ReadyToDeliver)]
    public void AllowedNext_EveryNonTerminalStatus_CanReachCancelled(OrderStatus from)
        => OrderStateMachine.AllowedNext(from).Should().Contain(OrderStatus.Cancelled,
            "a shop must always be able to cancel an order that has not yet been handed over");

    // Rule: PartiallyDelivered is the one non-terminal status that CANNOT reach Cancelled.
    // Why:  once some items have already left the shop, cancelling "the rest" of the order
    //       would leave those delivered garments unaccounted for on a cancelled order — the
    //       only way forward from here is finishing the delivery, not cancelling it.
    [Fact]
    public void AllowedNext_PartiallyDelivered_CannotReachCancelled()
    {
        OrderStateMachine.AllowedNext(OrderStatus.PartiallyDelivered).Should().NotContain(OrderStatus.Cancelled);
        OrderStateMachine.AllowedNext(OrderStatus.PartiallyDelivered).Should().BeEquivalentTo(new[] { OrderStatus.Delivered });
    }

    [Fact]
    public void AllowedNext_Delivered_IsEmpty()
        => OrderStateMachine.AllowedNext(OrderStatus.Delivered).Should().BeEmpty();

    [Fact]
    public void AllowedNext_Cancelled_IsEmpty()
        => OrderStateMachine.AllowedNext(OrderStatus.Cancelled).Should().BeEmpty();

    // Rule: FullyPaid is a legacy value from before payment state was tracked separately.
    // Why:  orders created by the old code still carry this status in the database; the API
    //       must still let staff move them forward instead of getting permanently stuck.
    [Fact]
    public void AllowedNext_LegacyFullyPaid_AllowsEveryNonLegacyStatus()
    {
        var next = OrderStateMachine.AllowedNext(OrderStatus.FullyPaid);

        next.Should().NotContain(OrderStatus.FullyPaid);
        next.Should().Contain(OrderStatus.Delivered);
        next.Should().Contain(OrderStatus.Cancelled);
        next.Should().Contain(OrderStatus.Pending);
    }

    // ───────────────────────────────────────────────────────────────
    // IsSettable
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void IsSettable_LegacyFullyPaid_ReturnsFalse()
        => OrderStateMachine.IsSettable(OrderStatus.FullyPaid).Should().BeFalse(
            "payment completion is tracked in PaymentStatus now, not as an order status the API sets");

    [Fact]
    public void IsSettable_OrdinaryStatus_ReturnsTrue()
        => OrderStateMachine.IsSettable(OrderStatus.Cutting).Should().BeTrue();

    // Rule: an out-of-range integer cast to OrderStatus is never settable.
    // Why:  a malformed or future-client request body can carry any int in the status field;
    //       without this guard it would sail past IsSettable and hit the "already this status"
    //       or transition-table logic with a value no switch expression recognises.
    [Fact]
    public void IsSettable_UndefinedEnumValue_ReturnsFalse()
        => OrderStateMachine.IsSettable((OrderStatus)999).Should().BeFalse();

    [Fact]
    public void Validate_UndefinedEnumValue_ReturnsSettableError()
        => OrderStateMachine.Validate(OrderStatus.Pending, (OrderStatus)999, notes: null, force: false)
            .Should().Contain("cannot be set");

    // ───────────────────────────────────────────────────────────────
    // IsReversal
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void IsReversal_MovingBackward_ReturnsTrue()
        => OrderStateMachine.IsReversal(OrderStatus.Stitching, OrderStatus.Cutting).Should().BeTrue();

    [Fact]
    public void IsReversal_MovingForward_ReturnsFalse()
        => OrderStateMachine.IsReversal(OrderStatus.Cutting, OrderStatus.Stitching).Should().BeFalse();

    [Fact]
    public void IsReversal_SameStatus_ReturnsFalse()
        => OrderStateMachine.IsReversal(OrderStatus.Stitching, OrderStatus.Stitching).Should().BeFalse(
            "a status compared to itself is neither forward nor backward progress");

    [Fact]
    public void IsReversal_ToCancelled_ReturnsFalse()
        => OrderStateMachine.IsReversal(OrderStatus.Stitching, OrderStatus.Cancelled).Should().BeFalse(
            "cancellation has its own rank (-1) and is not a 'backward' production step");

    // ───────────────────────────────────────────────────────────────
    // Validate
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_LegalForwardMove_ReturnsNull()
        => OrderStateMachine.Validate(OrderStatus.Cutting, OrderStatus.Stitching, notes: null, force: false)
            .Should().BeNull();

    [Fact]
    public void Validate_SameStatus_ReturnsMessage()
        => OrderStateMachine.Validate(OrderStatus.Cutting, OrderStatus.Cutting, notes: null, force: false)
            .Should().Contain("already");

    [Fact]
    public void Validate_FromTerminalStatus_ReturnsMessage()
        => OrderStateMachine.Validate(OrderStatus.Delivered, OrderStatus.Cutting, notes: null, force: false)
            .Should().Contain("cannot change status");

    // Rule: a delivered or cancelled order cannot silently become anything else.
    // Why:  this is the specific defect the workflow rewrite closed — the old code accepted
    //       Cancelled -> Delivered with no history record, which would let a cancelled order
    //       be marked handed over and hide a refund that was never actually processed.
    [Fact]
    public void Validate_CancelledToDelivered_IsRejected_EvenWithForce()
    {
        // force only relaxes the transition table for a NON-terminal current status
        OrderStateMachine.Validate(OrderStatus.Cancelled, OrderStatus.Delivered, notes: "override", force: true)
            .Should().Contain("cannot change status");
    }

    [Fact]
    public void Validate_IllegalNonAdjacentJump_ReturnsAllowedList()
    {
        var error = OrderStateMachine.Validate(OrderStatus.Pending, OrderStatus.Delivered, notes: null, force: false);

        error.Should().Contain("Cannot move an order from Pending to Delivered");
        error.Should().Contain("Allowed next:");
    }

    [Fact]
    public void Validate_BackwardMoveWithoutNotes_RequiresNote()
        => OrderStateMachine.Validate(OrderStatus.Stitching, OrderStatus.Cutting, notes: null, force: false)
            .Should().Contain("note");

    [Fact]
    public void Validate_BackwardMoveWithWhitespaceOnlyNotes_StillRequiresNote()
        // IsNullOrWhiteSpace, not IsNullOrEmpty: a note of just spaces carries no explanation
        // and must be rejected the same as no note at all.
        => OrderStateMachine.Validate(OrderStatus.Stitching, OrderStatus.Cutting, notes: "   ", force: false)
            .Should().Contain("note");

    [Fact]
    public void Validate_BackwardMoveWithNotes_Allowed()
        => OrderStateMachine.Validate(OrderStatus.Stitching, OrderStatus.Cutting, notes: "fabric defect found", force: false)
            .Should().BeNull();

    [Fact]
    public void Validate_ForceWithoutNotes_RequiresNote()
        => OrderStateMachine.Validate(OrderStatus.Pending, OrderStatus.ReadyToDeliver, notes: null, force: true)
            .Should().Contain("A note is required when forcing");

    [Fact]
    public void Validate_ForceWithWhitespaceOnlyNotes_StillRequiresNote()
        => OrderStateMachine.Validate(OrderStatus.Pending, OrderStatus.ReadyToDeliver, notes: "   ", force: true)
            .Should().Contain("A note is required when forcing");

    [Fact]
    public void Validate_ForceWithNotes_BypassesTransitionTable()
        // Pending -> ReadyToDeliver is not in the normal transition table, but force lets a
        // supervisor make the jump anyway (e.g. correcting a data-entry mistake), with a note.
        => OrderStateMachine.Validate(OrderStatus.Pending, OrderStatus.ReadyToDeliver, notes: "manual correction", force: true)
            .Should().BeNull();

    [Fact]
    public void Validate_ForceToLegacyStatus_StillRejected()
        => OrderStateMachine.Validate(OrderStatus.Pending, OrderStatus.FullyPaid, notes: "note", force: true)
            .Should().Contain("PaymentStatus");

    // ───────────────────────────────────────────────────────────────
    // ShouldAutoAdvance — drives the "order status follows production" behavior
    // ───────────────────────────────────────────────────────────────

    // Rule: starting a stage's work should move the order forward, never backward.
    // Why:  if a karigar starts recutting on an item after Stitching has already begun on
    //       another item in the same order, the order must not regress to Cutting and hide
    //       that stitching is ahead. Stitching -> Cutting is legal in the transition table
    //       (recuts happen), so only the rank check can be the thing refusing this move —
    //       a case where the table alone would wrongly allow it.
    [Fact]
    public void ShouldAutoAdvance_TableAllowsMoveButRankIsBehind_DoesNotMoveBackward()
    {
        OrderStateMachine.CanTransition(OrderStatus.Stitching, OrderStatus.Cutting).Should().BeTrue(
            "this test only proves the rank guard if the transition table would otherwise allow the move");

        var result = OrderStateMachine.ShouldAutoAdvance(OrderStatus.Stitching, ProductionStage.Cutting, out var target);

        result.Should().BeFalse();
        target.Should().Be(OrderStatus.Cutting, "the target is still computed even when the move is refused");
    }

    [Fact]
    public void ShouldAutoAdvance_StageAheadOfCurrentStatus_Advances()
    {
        var result = OrderStateMachine.ShouldAutoAdvance(OrderStatus.Pending, ProductionStage.Cutting, out var target);

        result.Should().BeTrue();
        target.Should().Be(OrderStatus.Cutting);
    }

    // Rule: a stage target the transition table does not allow from the current status never
    //       auto-advances, even though its rank is numerically ahead.
    // Why:  Pending has no direct route to Embroidery in the workflow (Cutting/Stitching come
    //       first) — proves the CanTransition check does real work, not just the rank check.
    [Fact]
    public void ShouldAutoAdvance_RankAheadButTableDisallows_DoesNotAdvance()
    {
        OrderStateMachine.CanTransition(OrderStatus.Pending, OrderStatus.Embroidery).Should().BeFalse(
            "this test only proves the table guard if rank alone would otherwise allow the move");

        OrderStateMachine.ShouldAutoAdvance(OrderStatus.Pending, ProductionStage.Embroidery, out _).Should().BeFalse();
    }

    // Rule: a terminal or post-production status never auto-advances from stage work.
    // Why:  a stray or late stage log must never pull a cancelled or already-delivered order
    //       back into production. NOTE: verified by hand (not by this suite) that IsInProduction
    //       cannot currently be isolated as a mutation-catchable guard here — for every status it
    //       would exclude, either the rank check (TrialReady..Delivered all outrank every stage
    //       target) or CanTransition's empty Cancelled transition list already blocks the same
    //       input. It stays in the code as an explicit invariant in case the transition table
    //       ever changes shape, but no test today can prove its removal would break anything.
    [Fact]
    public void ShouldAutoAdvance_CancelledOrder_NeverAdvances()
    {
        OrderStateMachine.IsInProduction(OrderStatus.Cancelled).Should().BeFalse();

        OrderStateMachine.ShouldAutoAdvance(OrderStatus.Cancelled, ProductionStage.Cutting, out _).Should().BeFalse(
            "a cancelled order must never be pulled back into production by a stray stage log");
    }

    [Fact]
    public void ShouldAutoAdvance_DeliveredOrder_ReturnsFalse()
        => OrderStateMachine.ShouldAutoAdvance(OrderStatus.Delivered, ProductionStage.Cutting, out _)
            .Should().BeFalse("a delivered order's status must not change just because a log was recorded against it");

    // Rule: FullyPaid (the legacy status) needs no special case — every stage target outranks
    //       it and every transition out of it is allowed, so the ordinary rank and table checks
    //       already move it forward on their own.
    [Fact]
    public void ShouldAutoAdvance_LegacyFullyPaid_AdvancesViaTheOrdinaryChecks()
    {
        var result = OrderStateMachine.ShouldAutoAdvance(OrderStatus.FullyPaid, ProductionStage.Cutting, out var target);

        result.Should().BeTrue();
        target.Should().Be(OrderStatus.Cutting);
    }

    [Theory]
    [InlineData(ProductionStage.Cutting, OrderStatus.Cutting)]
    [InlineData(ProductionStage.Stitching, OrderStatus.Stitching)]
    [InlineData(ProductionStage.Embroidery, OrderStatus.Embroidery)]
    [InlineData(ProductionStage.Ironing, OrderStatus.Finishing)]
    [InlineData(ProductionStage.Finishing, OrderStatus.Finishing)]
    [InlineData(ProductionStage.QualityCheck, OrderStatus.Finishing)]
    public void StatusForStage_MapsEveryProductionStage(ProductionStage stage, OrderStatus expected)
        => OrderStateMachine.StatusForStage(stage).Should().Be(expected);

    // ───────────────────────────────────────────────────────────────
    // CancellationStageFor
    // ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(OrderStatus.Pending, CancellationStage.BeforeCutting)]
    [InlineData(OrderStatus.Received, CancellationStage.BeforeCutting)]
    [InlineData(OrderStatus.Cutting, CancellationStage.AfterCutting)]
    [InlineData(OrderStatus.Stitching, CancellationStage.AfterStitching)]
    [InlineData(OrderStatus.ReadyToDeliver, CancellationStage.AfterStitching)]
    public void CancellationStageFor_RecordsWhereCancellationHappened(OrderStatus status, CancellationStage expected)
        => OrderStateMachine.CancellationStageFor(status).Should().Be(expected);

    [Theory]
    [InlineData(OrderStatus.Delivered, true)]
    [InlineData(OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Cutting, false)]
    [InlineData(OrderStatus.Pending, false)]
    public void IsTerminal_MatchesDeliveredAndCancelledOnly(OrderStatus status, bool expected)
        => OrderStateMachine.IsTerminal(status).Should().Be(expected);
}

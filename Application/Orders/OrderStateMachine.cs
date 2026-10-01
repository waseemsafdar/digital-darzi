using Domain.Enums;

namespace Application.Orders;

/// <summary>
/// The tailoring order workflow. One table decides every legal status change so that the
/// API, the automatic stage-driven advance and the delivery/cancel flows cannot disagree.
///
/// Forward path:
/// Pending → Received → Cutting → Stitching → (Embroidery) → Finishing → (TrialReady → TrialDone)
/// → ReadyToDeliver → (PartiallyDelivered) → Delivered.
/// Every non-terminal status may move to Cancelled, EXCEPT PartiallyDelivered: once some items
/// have left the shop, cancelling the rest would leave delivered garments unaccounted for, so a
/// partially delivered order can only proceed to Delivered. Delivered and Cancelled are terminal.
/// FullyPaid is a legacy value: an order still carrying it may move to any non-legacy status.
/// </summary>
public static class OrderStateMachine
{
    private static readonly IReadOnlyDictionary<OrderStatus, OrderStatus[]> Transitions =
        new Dictionary<OrderStatus, OrderStatus[]>
        {
            [OrderStatus.Pending]            = new[] { OrderStatus.Received, OrderStatus.Cutting, OrderStatus.Stitching, OrderStatus.Cancelled },
            [OrderStatus.Received]           = new[] { OrderStatus.Cutting, OrderStatus.Stitching, OrderStatus.Cancelled },
            [OrderStatus.Cutting]            = new[] { OrderStatus.Stitching, OrderStatus.Embroidery, OrderStatus.Finishing, OrderStatus.Cancelled },
            [OrderStatus.Stitching]          = new[] { OrderStatus.Embroidery, OrderStatus.Finishing, OrderStatus.TrialReady, OrderStatus.ReadyToDeliver, OrderStatus.Cutting, OrderStatus.Cancelled },
            [OrderStatus.Embroidery]         = new[] { OrderStatus.Stitching, OrderStatus.Finishing, OrderStatus.TrialReady, OrderStatus.ReadyToDeliver, OrderStatus.Cancelled },
            [OrderStatus.Finishing]          = new[] { OrderStatus.TrialReady, OrderStatus.ReadyToDeliver, OrderStatus.Stitching, OrderStatus.Cancelled },
            [OrderStatus.TrialReady]         = new[] { OrderStatus.TrialDone, OrderStatus.Cancelled },
            [OrderStatus.TrialDone]          = new[] { OrderStatus.Stitching, OrderStatus.Finishing, OrderStatus.ReadyToDeliver, OrderStatus.Cancelled },
            [OrderStatus.ReadyToDeliver]     = new[] { OrderStatus.PartiallyDelivered, OrderStatus.Delivered, OrderStatus.Stitching, OrderStatus.Finishing, OrderStatus.Cancelled },
            [OrderStatus.PartiallyDelivered] = new[] { OrderStatus.Delivered },
            [OrderStatus.Delivered]          = Array.Empty<OrderStatus>(),
            [OrderStatus.Cancelled]          = Array.Empty<OrderStatus>(),
        };

    /// <summary>Position along the forward path; used to detect a backwards move.</summary>
    private static int Rank(OrderStatus s) => s switch
    {
        OrderStatus.Pending            => 0,
        OrderStatus.Received           => 1,
        OrderStatus.Cutting            => 2,
        OrderStatus.Stitching          => 3,
        OrderStatus.Embroidery         => 4,
        OrderStatus.Finishing          => 5,
        OrderStatus.TrialReady         => 6,
        OrderStatus.TrialDone          => 7,
        OrderStatus.ReadyToDeliver     => 8,
        OrderStatus.PartiallyDelivered => 9,
        OrderStatus.Delivered          => 10,
        _                              => -1   // Cancelled, legacy FullyPaid
    };

    public static bool IsTerminal(OrderStatus s) => s is OrderStatus.Delivered or OrderStatus.Cancelled;

    /// <summary>Statuses in which garments are being produced (used for auto-advance).</summary>
    public static bool IsInProduction(OrderStatus s) =>
        s is OrderStatus.Pending or OrderStatus.Received or OrderStatus.Cutting or OrderStatus.Stitching
            or OrderStatus.Embroidery or OrderStatus.Finishing or OrderStatus.FullyPaid;

    /// <summary>Statuses the API accepts as a target (the legacy value is never set again).</summary>
    public static bool IsSettable(OrderStatus s) => s != OrderStatus.FullyPaid && Enum.IsDefined(s);

    public static IReadOnlyList<OrderStatus> AllowedNext(OrderStatus from)
    {
        if (from == OrderStatus.FullyPaid)
            return Enum.GetValues<OrderStatus>().Where(s => s != OrderStatus.FullyPaid).ToList();
        return Transitions.TryGetValue(from, out var next) ? next : Array.Empty<OrderStatus>();
    }

    public static bool CanTransition(OrderStatus from, OrderStatus to)
        => from != to && IsSettable(to) && AllowedNext(from).Contains(to);

    /// <summary>True when the move goes back along the forward path (notes are then mandatory).</summary>
    public static bool IsReversal(OrderStatus from, OrderStatus to)
    {
        var f = Rank(from);
        var t = Rank(to);
        return f >= 0 && t >= 0 && t < f;
    }

    /// <summary>
    /// Validates a requested change. <paramref name="force"/> lets a supervisor make any change
    /// out of a non-terminal status (except to the legacy value), with a mandatory note.
    /// Returns null when allowed, otherwise the reason.
    /// </summary>
    public static string? Validate(OrderStatus from, OrderStatus to, string? notes, bool force)
    {
        if (!IsSettable(to)) return $"'{to}' cannot be set. Payment state is tracked separately in PaymentStatus.";
        if (from == to) return $"The order is already {to}.";
        if (IsTerminal(from)) return $"A {from.ToString().ToLowerInvariant()} order cannot change status.";

        if (force)
            return string.IsNullOrWhiteSpace(notes) ? "A note is required when forcing a status change." : null;

        if (!CanTransition(from, to))
            return $"Cannot move an order from {from} to {to}. Allowed next: {string.Join(", ", AllowedNext(from))}.";

        if (IsReversal(from, to) && string.IsNullOrWhiteSpace(notes))
            return $"Moving back from {from} to {to} needs a note explaining why.";

        return null;
    }

    /// <summary>The order status a production stage corresponds to when work on it starts.</summary>
    public static OrderStatus StatusForStage(ProductionStage stage) => stage switch
    {
        ProductionStage.Cutting      => OrderStatus.Cutting,
        ProductionStage.Stitching    => OrderStatus.Stitching,
        ProductionStage.Embroidery   => OrderStatus.Embroidery,
        _                            => OrderStatus.Finishing   // Ironing, Finishing, QualityCheck
    };

    /// <summary>
    /// Whether starting work on <paramref name="stage"/> should move the order forward.
    /// Only ever advances (never moves an order backwards) and only while in production.
    /// FullyPaid (rank -1, like Cancelled) needs no special case: every stage target outranks
    /// it and every transition out of it is allowed, so the two checks below already cover it.
    /// </summary>
    public static bool ShouldAutoAdvance(OrderStatus current, ProductionStage stage, out OrderStatus target)
    {
        target = StatusForStage(stage);
        if (!IsInProduction(current)) return false;
        return Rank(target) > Rank(current) && CanTransition(current, target);
    }

    /// <summary>Cancellation bucket recorded on the order, from the status it was cancelled in.</summary>
    public static CancellationStage CancellationStageFor(OrderStatus status) => status switch
    {
        OrderStatus.Pending or OrderStatus.Received => CancellationStage.BeforeCutting,
        OrderStatus.Cutting                         => CancellationStage.AfterCutting,
        _                                           => CancellationStage.AfterStitching
    };
}

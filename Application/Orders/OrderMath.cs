using Domain.Enums;

namespace Application.Orders;

/// <summary>
/// Order money rules in one place.
/// GrandTotal  = Σ(item price × qty + fabric charge) − discount + Σ alteration charges.
/// AmountPaid  = Σ payment lines (refund lines are negative).
/// BalanceDue  = GrandTotal − AmountPaid, never below zero for an active order.
/// </summary>
public static class OrderMath
{
    public static decimal LineTotal(decimal price, int qty, decimal fabricCharge) => price * qty + fabricCharge;

    public static decimal GrandTotal(decimal subTotal, decimal discount, decimal alterationCharges)
        => subTotal - discount + alterationCharges;

    public static decimal Balance(decimal grandTotal, decimal amountPaid)
        => Math.Max(0, grandTotal - amountPaid);

    public static PaymentStatus PaymentStatusFor(decimal grandTotal, decimal amountPaid, bool anyRefund)
    {
        if (anyRefund && amountPaid <= 0) return PaymentStatus.Refunded;
        // A fully discounted or alteration-free-of-charge order has nothing outstanding, even
        // before any payment line exists — it must not sit as "Unpaid" forever and prompt staff
        // to chase a customer who owes nothing.
        if (grandTotal <= 0) return PaymentStatus.Paid;
        if (amountPaid <= 0) return PaymentStatus.Unpaid;
        if (amountPaid >= grandTotal) return PaymentStatus.Paid;
        return PaymentStatus.Partial;
    }

    /// <summary>Returns null when the discount is acceptable, otherwise the reason.</summary>
    public static string? ValidateDiscount(decimal subTotal, decimal discount)
    {
        if (discount < 0) return "Discount cannot be negative.";
        if (discount > subTotal) return $"Discount ({discount:N2}) cannot exceed the subtotal ({subTotal:N2}).";
        return null;
    }

    /// <summary>Loyalty points and tier. Tier thresholds are cumulative points.</summary>
    public const int PointsPerOrder = 10;

    public static string LoyaltyTier(int points) => points switch
    {
        < 100  => "Bronze",
        < 500  => "Silver",
        < 1000 => "Gold",
        _      => "Platinum"
    };
}

using Application.Orders;
using Domain.Enums;
using FluentAssertions;
using Xunit;

namespace DigitalDarzi.UnitTests.Orders;

public class OrderMathTests
{
    // ───────────────────────────────────────────────────────────────
    // LineTotal / GrandTotal
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void LineTotal_PriceQtyAndFabricCharge_SumsCorrectly()
        => OrderMath.LineTotal(price: 1500m, qty: 3, fabricCharge: 800m).Should().Be(5300m);

    [Fact]
    public void LineTotal_ZeroFabricCharge_IsJustPriceTimesQty()
        => OrderMath.LineTotal(price: 2000m, qty: 2, fabricCharge: 0m).Should().Be(4000m);

    // Rule: GrandTotal = subtotal - discount + alteration charges.
    // Why:  the pre-fix code recomputed GrandTotal on every order update and silently dropped
    //       any alteration charge that had been added earlier, undercharging the customer by
    //       exactly the alteration amount on their final bill.
    [Fact]
    public void GrandTotal_IncludesAlterationCharges()
        => OrderMath.GrandTotal(subTotal: 5000m, discount: 500m, alterationCharges: 300m).Should().Be(4800m);

    [Fact]
    public void GrandTotal_NoAlterations_IsSubtotalMinusDiscount()
        => OrderMath.GrandTotal(subTotal: 5000m, discount: 500m, alterationCharges: 0m).Should().Be(4500m);

    // ───────────────────────────────────────────────────────────────
    // Balance
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void Balance_PartiallyPaid_ReturnsRemainder()
        => OrderMath.Balance(grandTotal: 5000m, amountPaid: 3000m).Should().Be(2000m);

    // Rule: balance never goes negative, even if the customer overpays.
    // Why:  a negative "balance due" would render as a credit owed to the customer on receipts
    //       and reports, which is not how the shop's ledger is supposed to read.
    [Fact]
    public void Balance_OverpaidState_ClampsToZero()
        => OrderMath.Balance(grandTotal: 5000m, amountPaid: 5200m).Should().Be(0m);

    [Fact]
    public void Balance_ExactPayment_IsZero()
        => OrderMath.Balance(grandTotal: 5000m, amountPaid: 5000m).Should().Be(0m);

    // ───────────────────────────────────────────────────────────────
    // PaymentStatusFor
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void PaymentStatusFor_NoPayment_ReturnsUnpaid()
        => OrderMath.PaymentStatusFor(grandTotal: 5000m, amountPaid: 0m, anyRefund: false).Should().Be(PaymentStatus.Unpaid);

    [Fact]
    public void PaymentStatusFor_SomePayment_ReturnsPartial()
        => OrderMath.PaymentStatusFor(grandTotal: 5000m, amountPaid: 2000m, anyRefund: false).Should().Be(PaymentStatus.Partial);

    [Fact]
    public void PaymentStatusFor_FullPayment_ReturnsPaid()
        => OrderMath.PaymentStatusFor(grandTotal: 5000m, amountPaid: 5000m, anyRefund: false).Should().Be(PaymentStatus.Paid);

    [Fact]
    public void PaymentStatusFor_OverpaidCountsAsPaid()
        => OrderMath.PaymentStatusFor(grandTotal: 5000m, amountPaid: 5500m, anyRefund: false).Should().Be(PaymentStatus.Paid);

    // Rule: a fully refunded order (net paid back to zero, with a refund on record) reads as Refunded, not Unpaid.
    // Why:  "Unpaid" on a cancelled-and-refunded order reads to staff as "customer still owes
    //       money", which would prompt someone to chase a customer who was already refunded.
    [Fact]
    public void PaymentStatusFor_ZeroNetWithRefund_ReturnsRefunded()
        => OrderMath.PaymentStatusFor(grandTotal: 5000m, amountPaid: 0m, anyRefund: true).Should().Be(PaymentStatus.Refunded);

    [Fact]
    public void PaymentStatusFor_PartialRefundStillOwing_ReturnsPartial()
        // A partial refund that still leaves money paid (net > 0) is not "Refunded" — the order
        // still has funds against it, so Partial correctly reflects that money remains applied.
        => OrderMath.PaymentStatusFor(grandTotal: 5000m, amountPaid: 1000m, anyRefund: true).Should().Be(PaymentStatus.Partial);

    // Rule: a fully discounted order (GrandTotal <= 0) reads as Paid even before any payment line exists.
    // Why:  without this, a 100%-off order would sit as "Unpaid" forever — the customer owes
    //       nothing, but staff working the pending-balances report would see it as outstanding
    //       and could chase a customer who does not owe a rupee.
    [Fact]
    public void PaymentStatusFor_ZeroGrandTotal_ReturnsPaid_NotUnpaid()
        => OrderMath.PaymentStatusFor(grandTotal: 0m, amountPaid: 0m, anyRefund: false).Should().Be(PaymentStatus.Paid);

    [Fact]
    public void PaymentStatusFor_ZeroGrandTotalWithRefund_RefundTakesPriority()
        // A cancelled, fully-discounted order that was still refunded (e.g. the advance was
        // returned) should read as Refunded, not Paid — the refund is the more informative fact.
        => OrderMath.PaymentStatusFor(grandTotal: 0m, amountPaid: 0m, anyRefund: true).Should().Be(PaymentStatus.Refunded);

    // ───────────────────────────────────────────────────────────────
    // ValidateDiscount
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateDiscount_WithinSubtotal_ReturnsNull()
        => OrderMath.ValidateDiscount(subTotal: 5000m, discount: 1000m).Should().BeNull();

    [Fact]
    public void ValidateDiscount_ExactlyEqualToSubtotal_ReturnsNull()
        => OrderMath.ValidateDiscount(subTotal: 5000m, discount: 5000m).Should().BeNull(
            "a 100% discount is unusual but not invalid — a shop may comp an order entirely");

    // Rule: discount cannot exceed the subtotal (order boundary, operator is > not >=).
    // Why:  the pre-fix order update path never checked this, so a discount typo could push
    //       GrandTotal negative and the customer would appear to be owed money by the shop.
    [Fact]
    public void ValidateDiscount_OneOverSubtotal_ReturnsError()
        => OrderMath.ValidateDiscount(subTotal: 5000m, discount: 5000.01m).Should().Contain("cannot exceed");

    [Fact]
    public void ValidateDiscount_Negative_ReturnsError()
        => OrderMath.ValidateDiscount(subTotal: 5000m, discount: -1m).Should().Contain("negative");

    [Fact]
    public void ValidateDiscount_ExactlyZero_ReturnsNull()
        // The overwhelmingly common case — most orders carry no discount at all. If the
        // negative-check operator were ever changed from < to <=, this is what would break.
        => OrderMath.ValidateDiscount(subTotal: 5000m, discount: 0m).Should().BeNull();

    // ───────────────────────────────────────────────────────────────
    // LoyaltyTier — boundary is < not <=, so exactly-at-threshold rolls to the next tier
    // ───────────────────────────────────────────────────────────────

    // OrderMath.LoyaltyTier is the documented reference implementation of the tier thresholds
    // (see the comment in OrderCore.AdjustCustomerStatsAsync). It is not called directly in the
    // hot path: that method runs the same switch inline inside an EF Core ExecuteUpdateAsync
    // expression, because EF's SQL translator cannot invoke an arbitrary C# method there. These
    // tests pin the thresholds this class is supposed to define; keeping the inline copy in sync
    // is a manual step flagged by that comment, not verified by this suite.
    [Theory]
    [InlineData(0, "Bronze")]
    [InlineData(99, "Bronze")]
    [InlineData(100, "Silver")]
    [InlineData(499, "Silver")]
    [InlineData(500, "Gold")]
    [InlineData(999, "Gold")]
    [InlineData(1000, "Platinum")]
    [InlineData(5000, "Platinum")]
    public void LoyaltyTier_BoundaryPoints_MapToExpectedTier(int points, string expectedTier)
        => OrderMath.LoyaltyTier(points).Should().Be(expectedTier);

    [Fact]
    public void PointsPerOrder_IsTenAsDocumentedInTheGuide()
        => OrderMath.PointsPerOrder.Should().Be(10);
}

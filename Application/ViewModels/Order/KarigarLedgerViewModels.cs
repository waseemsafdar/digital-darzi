using Domain.Enums;

namespace Application.ViewModels.Order;

public class KarigarRateViewModel
{
    public Guid Id { get; set; }
    public Guid? KarigarId { get; set; }
    public string? KarigarName { get; set; }
    public GarmentType GarmentType { get; set; }
    public ProductionStage Stage { get; set; }
    public decimal Rate { get; set; }
}

public class UpsertKarigarRateViewModel
{
    /// <summary>Null = shop default for every karigar.</summary>
    public Guid? KarigarId { get; set; }
    public GarmentType GarmentType { get; set; }
    public ProductionStage Stage { get; set; }
    public decimal Rate { get; set; }
}

public class KarigarLedgerEntryViewModel
{
    public Guid Id { get; set; }
    public KarigarLedgerEntryType EntryType { get; set; }
    public decimal Amount { get; set; }
    public decimal SignedAmount { get; set; }
    public decimal RunningBalance { get; set; }
    public DateTime EntryDate { get; set; }
    public Guid? OrderItemId { get; set; }
    public string? OrderNumber { get; set; }
    public GarmentType? GarmentType { get; set; }
    public ProductionStage? Stage { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? Notes { get; set; }
}

public class KarigarLedgerViewModel
{
    public Guid KarigarId { get; set; }
    public string KarigarName { get; set; } = string.Empty;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal Earnings { get; set; }
    public decimal Advances { get; set; }
    public decimal Deductions { get; set; }
    public decimal Payments { get; set; }
    public decimal Adjustments { get; set; }
    /// <summary>Amount the shop owes the karigar now (negative = karigar owes the shop).</summary>
    public decimal ClosingBalance { get; set; }
    public List<KarigarLedgerEntryViewModel> Entries { get; set; } = new();
}

public class KarigarBalanceViewModel
{
    public Guid KarigarId { get; set; }
    public string KarigarName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public decimal TotalEarned { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal Balance { get; set; }
}

public class RecordKarigarLedgerEntryViewModel
{
    /// <summary>Payment, Advance, Deduction or Adjustment. Earnings are created by completed stage work.</summary>
    public KarigarLedgerEntryType EntryType { get; set; } = KarigarLedgerEntryType.Payment;
    /// <summary>Positive amount. For Adjustment, the sign is taken from <see cref="IsCredit"/>.</summary>
    public decimal Amount { get; set; }
    /// <summary>Adjustment only: true increases what the shop owes the karigar.</summary>
    public bool IsCredit { get; set; } = true;
    public PaymentMethod? PaymentMethod { get; set; }
    public DateTime? EntryDate { get; set; }
    public string? Notes { get; set; }
}

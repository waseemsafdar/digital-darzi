using Domain.Enums;

namespace Domain.Entities;

public class ShopExpense : BaseDBModel
{
    public Guid ShopId { get; set; }
    public string Category { get; set; } = string.Empty;   // flexible string category
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string? ReceiptRef { get; set; }
    public Guid? AddedByUserId { get; set; }

    // Navigation
    public User? AddedByUser { get; set; }
}

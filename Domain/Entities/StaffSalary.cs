using Domain.Enums;

namespace Domain.Entities;

public class StaffSalary : BaseDBModel
{
    public Guid ShopId { get; set; }
    public Guid StaffUserId { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal BaseSalary { get; set; }
    public decimal Bonus { get; set; } = 0;
    public decimal Deduction { get; set; } = 0;
    public decimal NetSalary { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string? Notes { get; set; }
    public DateTime PaidOn { get; set; } = DateTime.UtcNow;

    // Navigation
    public User? StaffUser { get; set; }
}

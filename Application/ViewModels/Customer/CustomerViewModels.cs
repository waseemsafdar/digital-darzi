using Domain.Enums;

namespace Application.ViewModels.Customer;

public class CreateCustomerViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public Gender Gender { get; set; } = Gender.Male;
    public DateTime? DateOfBirth { get; set; }
    public string? Notes { get; set; }
}

public class UpdateCustomerViewModel
{
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public Gender? Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Notes { get; set; }
    public ActiveStatus? ActiveStatus { get; set; }
}

public class CustomerSearchViewModel
{
    public string? Query { get; set; }       // name or phone search
    public string? City { get; set; }
    public Gender? Gender { get; set; }
    public ActiveStatus? ActiveStatus { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class CustomerListViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? City { get; set; }
    public Gender Gender { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalSpend { get; set; }
    public string LoyaltyTier { get; set; } = "Bronze";
    public ActiveStatus ActiveStatus { get; set; }
}

public class CustomerDetailViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public Gender Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Notes { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalSpend { get; set; }
    public int LoyaltyPoints { get; set; }
    public string LoyaltyTier { get; set; } = "Bronze";
    public ActiveStatus ActiveStatus { get; set; }
    public DateTime CreatedOn { get; set; }
}

// ── Customer Ledger ──────────────────────────────────────────────────────
public class CustomerLedgerOrderViewModel
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public decimal GrandTotal { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime DeliveryDate { get; set; }
}

public class CustomerLedgerViewModel
{
    public Guid CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public List<CustomerLedgerOrderViewModel> Orders { get; set; } = new();
    public decimal TotalGrand { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalDue { get; set; }
}

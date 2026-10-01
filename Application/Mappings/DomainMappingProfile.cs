using Application.ViewModels.Customer;
using Application.ViewModels.Finance;
using Application.ViewModels.Shop;
using Domain.Entities;
using Domain.Enums;

namespace Application.Mappings;

/// <summary>
/// The complete list of entity to view-model mappings used by the generic CRUD services.
/// This file previously held the AutoMapper profile; AutoMapper was removed (see IObjectMapper).
/// "Create" delegates build a new object; "Apply" delegates copy only the non-null members of an
/// update view model onto a tracked entity (the partial-update semantics the API always had).
/// </summary>
public sealed class MappingRegistry
{
    private readonly Dictionary<(Type, Type), Func<object, object>> _create = new();
    private readonly Dictionary<(Type, Type), Action<object, object>> _apply = new();

    public MappingRegistry()
    {
        // ── Shop ────────────────────────────────────────────────────────────
        Create<CreateShopViewModel, Shop>(s => new Shop
        {
            Name           = s.Name,
            Address        = s.Address,
            City           = s.City,
            Phone          = s.Phone,
            WhatsAppNumber = s.WhatsAppNumber,
            LogoUrl        = s.LogoUrl,
            Currency       = string.IsNullOrWhiteSpace(s.Currency) ? "PKR" : s.Currency,
            CurrencySymbol = string.IsNullOrWhiteSpace(s.CurrencySymbol) ? "₨" : s.CurrencySymbol,
            TimeZone       = string.IsNullOrWhiteSpace(s.TimeZone) ? Shop.DefaultTimeZone : s.TimeZone,
            ActiveStatus   = ActiveStatus.Active
        });
        Apply<UpdateShopViewModel, Shop>((s, d) =>
        {
            if (s.Name != null) d.Name = s.Name;
            if (s.Address != null) d.Address = s.Address;
            if (s.City != null) d.City = s.City;
            if (s.Phone != null) d.Phone = s.Phone;
            if (s.WhatsAppNumber != null) d.WhatsAppNumber = s.WhatsAppNumber;
            if (s.LogoUrl != null) d.LogoUrl = s.LogoUrl;
            if (s.Currency != null) d.Currency = s.Currency;
            if (s.CurrencySymbol != null) d.CurrencySymbol = s.CurrencySymbol;
            if (s.TimeZone != null) d.TimeZone = s.TimeZone;
            if (s.NotificationsEnabled.HasValue) d.NotificationsEnabled = s.NotificationsEnabled.Value;
            if (s.ActiveStatus.HasValue) d.ActiveStatus = s.ActiveStatus.Value;
        });

        // ── Customer ────────────────────────────────────────────────────────
        Create<CreateCustomerViewModel, Customer>(s => new Customer
        {
            Name                = s.Name.Trim(),
            Phone               = s.Phone.Trim(),
            WhatsAppNumber      = s.WhatsAppNumber,
            Email               = s.Email,
            Address             = s.Address,
            City                = s.City,
            Gender              = s.Gender,
            DateOfBirth         = s.DateOfBirth,
            Notes               = s.Notes,
            CustomerType        = s.CustomerType,
            PreferredKarigarId  = s.PreferredKarigarId,
            PreferredStyles     = s.PreferredStyles,
            NotificationsOptOut = s.NotificationsOptOut,
            ActiveStatus        = ActiveStatus.Active
        });
        Apply<UpdateCustomerViewModel, Customer>((s, d) =>
        {
            if (s.Name != null) d.Name = s.Name.Trim();
            if (s.Phone != null) d.Phone = s.Phone.Trim();
            if (s.WhatsAppNumber != null) d.WhatsAppNumber = s.WhatsAppNumber;
            if (s.Email != null) d.Email = s.Email;
            if (s.Address != null) d.Address = s.Address;
            if (s.City != null) d.City = s.City;
            if (s.Gender.HasValue) d.Gender = s.Gender.Value;
            if (s.DateOfBirth.HasValue) d.DateOfBirth = s.DateOfBirth;
            if (s.Notes != null) d.Notes = s.Notes;
            if (s.CustomerType.HasValue) d.CustomerType = s.CustomerType.Value;
            if (s.PreferredKarigarId.HasValue) d.PreferredKarigarId = s.PreferredKarigarId;
            if (s.PreferredStyles != null) d.PreferredStyles = s.PreferredStyles;
            if (s.NotificationsOptOut.HasValue) d.NotificationsOptOut = s.NotificationsOptOut.Value;
            if (s.ActiveStatus.HasValue) d.ActiveStatus = s.ActiveStatus.Value;
        });
        Create<Customer, CustomerDetailViewModel>(c => new CustomerDetailViewModel
        {
            Id                  = c.Id,
            Name                = c.Name,
            Phone               = c.Phone,
            WhatsAppNumber      = c.WhatsAppNumber,
            Email               = c.Email,
            Address             = c.Address,
            City                = c.City,
            Gender              = c.Gender,
            DateOfBirth         = c.DateOfBirth,
            Notes               = c.Notes,
            CustomerType        = c.CustomerType,
            PreferredKarigarId  = c.PreferredKarigarId,
            PreferredStyles     = c.PreferredStyles,
            NotificationsOptOut = c.NotificationsOptOut,
            TotalOrders         = c.TotalOrders,
            TotalSpend          = c.TotalSpend,
            LoyaltyPoints       = c.LoyaltyPoints,
            LoyaltyTier         = c.LoyaltyTier,
            ActiveStatus        = c.ActiveStatus,
            CreatedOn           = c.CreatedOn
        });

        // ── ShopExpense ─────────────────────────────────────────────────────
        Create<CreateShopExpenseViewModel, ShopExpense>(s => new ShopExpense
        {
            Category      = s.Category.Trim(),
            Description   = s.Description,
            Amount        = s.Amount,
            ExpenseDate   = s.ExpenseDate,
            PaymentMethod = s.PaymentMethod,
            ReceiptRef    = s.ReceiptRef,
            ActiveStatus  = ActiveStatus.Active
        });
        Apply<UpdateShopExpenseViewModel, ShopExpense>((s, d) =>
        {
            if (s.Category != null) d.Category = s.Category.Trim();
            if (s.Description != null) d.Description = s.Description;
            if (s.Amount.HasValue) d.Amount = s.Amount.Value;
            if (s.ExpenseDate.HasValue) d.ExpenseDate = s.ExpenseDate.Value;
            if (s.PaymentMethod.HasValue) d.PaymentMethod = s.PaymentMethod.Value;
            if (s.ReceiptRef != null) d.ReceiptRef = s.ReceiptRef;
        });
        Create<ShopExpense, ShopExpenseDetailViewModel>(e => new ShopExpenseDetailViewModel
        {
            Id            = e.Id,
            ShopId        = e.ShopId,
            Category      = e.Category,
            Description   = e.Description,
            Amount        = e.Amount,
            ExpenseDate   = e.ExpenseDate,
            PaymentMethod = e.PaymentMethod,
            ReceiptRef    = e.ReceiptRef,
            AddedByUserId = e.AddedByUserId,
            AddedByName   = e.AddedByUser != null ? e.AddedByUser.Name : string.Empty,
            CreatedOn     = e.CreatedOn
        });

        // ── StaffSalary ─────────────────────────────────────────────────────
        Create<RecordStaffSalaryViewModel, StaffSalary>(s => new StaffSalary
        {
            StaffUserId   = s.StaffUserId,
            Month         = s.Month,
            Year          = s.Year,
            BaseSalary    = s.BaseSalary,
            Bonus         = s.Bonus,
            Deduction     = s.Deduction,
            NetSalary     = s.BaseSalary + s.Bonus - s.Deduction,
            AmountPaid    = s.AmountPaid ?? (s.BaseSalary + s.Bonus - s.Deduction),
            PaymentMethod = s.PaymentMethod,
            Notes         = s.Notes,
            ActiveStatus  = ActiveStatus.Active
        });
        Apply<UpdateStaffSalaryViewModel, StaffSalary>((s, d) =>
        {
            if (s.BaseSalary.HasValue) d.BaseSalary = s.BaseSalary.Value;
            if (s.Bonus.HasValue) d.Bonus = s.Bonus.Value;
            if (s.Deduction.HasValue) d.Deduction = s.Deduction.Value;
            if (s.PaymentMethod.HasValue) d.PaymentMethod = s.PaymentMethod.Value;
            if (s.Notes != null) d.Notes = s.Notes;
        });
        Create<StaffSalary, StaffSalaryDetailViewModel>(s => new StaffSalaryDetailViewModel
        {
            Id            = s.Id,
            ShopId        = s.ShopId,
            StaffUserId   = s.StaffUserId,
            StaffName     = s.StaffUser != null ? s.StaffUser.Name : string.Empty,
            Month         = s.Month,
            Year          = s.Year,
            BaseSalary    = s.BaseSalary,
            Bonus         = s.Bonus,
            Deduction     = s.Deduction,
            NetSalary     = s.NetSalary,
            AmountPaid    = s.AmountPaid,
            Status        = s.Status,
            PaymentMethod = s.PaymentMethod,
            Notes         = s.Notes,
            PaidOn        = s.PaidOn
        });

        // ── StaffAttendance ─────────────────────────────────────────────────
        Create<RecordAttendanceViewModel, StaffAttendance>(s => new StaffAttendance
        {
            StaffUserId  = s.StaffUserId,
            Date         = s.Date,
            Status       = s.Status,
            CheckIn      = s.CheckIn,
            CheckOut     = s.CheckOut,
            Notes        = s.Notes,
            ActiveStatus = ActiveStatus.Active
        });
        Apply<UpdateAttendanceViewModel, StaffAttendance>((s, d) =>
        {
            if (s.Status.HasValue) d.Status = s.Status.Value;
            if (s.CheckIn.HasValue) d.CheckIn = s.CheckIn;
            if (s.CheckOut.HasValue) d.CheckOut = s.CheckOut;
            if (s.Notes != null) d.Notes = s.Notes;
        });
        Create<StaffAttendance, AttendanceDetailViewModel>(a => new AttendanceDetailViewModel
        {
            Id          = a.Id,
            ShopId      = a.ShopId,
            StaffUserId = a.StaffUserId,
            StaffName   = a.StaffUser != null ? a.StaffUser.Name : string.Empty,
            Date        = a.Date,
            Status      = a.Status,
            CheckIn     = a.CheckIn,
            CheckOut    = a.CheckOut,
            Notes       = a.Notes,
            CreatedOn   = a.CreatedOn
        });
    }

    /// <summary>All registered (source, destination) create pairs, enumerated by the mapping test.</summary>
    public IReadOnlyCollection<(Type Source, Type Destination)> CreatePairs => _create.Keys;

    /// <summary>All registered (source, destination) update pairs, enumerated by the mapping test.</summary>
    public IReadOnlyCollection<(Type Source, Type Destination)> ApplyPairs => _apply.Keys;

    internal Func<object, object>? FindCreate(Type source, Type dest)
    {
        if (dest.IsAssignableFrom(source)) return s => s;                 // identity: DTO already shaped
        for (var t = source; t != null; t = t.BaseType)                    // allow subclasses and proxies
            if (_create.TryGetValue((t, dest), out var f)) return f;
        return null;
    }

    internal Action<object, object>? FindApply(Type source, Type dest)
    {
        for (var t = dest; t != null; t = t.BaseType)
            if (_apply.TryGetValue((source, t), out var f)) return f;
        return null;
    }

    private void Create<TSource, TDest>(Func<TSource, TDest> map) where TDest : notnull
        => _create[(typeof(TSource), typeof(TDest))] = s => map((TSource)s);

    private void Apply<TSource, TDest>(Action<TSource, TDest> map)
        => _apply[(typeof(TSource), typeof(TDest))] = (s, d) => map((TSource)s, (TDest)d);
}

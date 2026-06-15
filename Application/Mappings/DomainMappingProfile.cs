using Application.ViewModels.Customer;
using Application.ViewModels.Finance;
using Application.ViewModels.Shop;
using AutoMapper;
using Domain.Entities;

namespace Application.Mappings;

/// <summary>
/// AutoMapper profile — maps all Domain entities to/from their ViewModels.
/// Mirrors MobilePosApi mapping approach.
/// </summary>
public class DomainMappingProfile : Profile
{
    public DomainMappingProfile()
    {
        // ── Shop ────────────────────────────────────────────────────────────
        CreateMap<CreateShopViewModel, Shop>()
            .ForMember(d => d.Id,         o => o.MapFrom(_ => Guid.NewGuid()))
            .ForMember(d => d.ActiveStatus,o => o.MapFrom(_ => Domain.Enums.ActiveStatus.Active))
            .ForMember(d => d.CreatedOn,  o => o.MapFrom(_ => DateTime.UtcNow));

        CreateMap<UpdateShopViewModel, Shop>()
            .ForAllMembers(o => o.Condition((src, dest, srcMember) => srcMember != null));

        CreateMap<Shop, ShopDetailViewModel>();

        // ── Customer ────────────────────────────────────────────────────────
        CreateMap<CreateCustomerViewModel, Customer>()
            .ForMember(d => d.Id,          o => o.MapFrom(_ => Guid.NewGuid()))
            .ForMember(d => d.ActiveStatus, o => o.MapFrom(_ => Domain.Enums.ActiveStatus.Active))
            .ForMember(d => d.CreatedOn,   o => o.MapFrom(_ => DateTime.UtcNow));

        CreateMap<UpdateCustomerViewModel, Customer>()
            .ForAllMembers(o => o.Condition((src, dest, srcMember) => srcMember != null));

        CreateMap<Customer, CustomerDetailViewModel>();

        // ── ShopExpense ──────────────────────────────────────────────────────
        CreateMap<CreateShopExpenseViewModel, ShopExpense>()
            .ForMember(d => d.Id,        o => o.MapFrom(_ => Guid.NewGuid()))
            .ForMember(d => d.CreatedOn, o => o.MapFrom(_ => DateTime.UtcNow));

        CreateMap<UpdateShopExpenseViewModel, ShopExpense>()
            .ForAllMembers(o => o.Condition((src, dest, srcMember) => srcMember != null));

        CreateMap<ShopExpense, ShopExpenseDetailViewModel>()
            .ForMember(d => d.AddedByName, o => o.MapFrom(s => s.AddedByUser != null ? s.AddedByUser.Name : string.Empty));

        // ── StaffSalary ──────────────────────────────────────────────────────
        CreateMap<RecordStaffSalaryViewModel, StaffSalary>()
            .ForMember(d => d.Id,        o => o.MapFrom(_ => Guid.NewGuid()))
            .ForMember(d => d.NetSalary, o => o.MapFrom(s => s.BaseSalary + s.Bonus - s.Deduction))
            .ForMember(d => d.PaidOn,    o => o.MapFrom(_ => DateTime.UtcNow))
            .ForMember(d => d.CreatedOn, o => o.MapFrom(_ => DateTime.UtcNow));

        CreateMap<UpdateStaffSalaryViewModel, StaffSalary>()
            .ForAllMembers(o => o.Condition((src, dest, srcMember) => srcMember != null));

        CreateMap<StaffSalary, StaffSalaryDetailViewModel>()
            .ForMember(d => d.StaffName, o => o.MapFrom(s => s.StaffUser != null ? s.StaffUser.Name : string.Empty));

        // ── StaffAttendance ──────────────────────────────────────────────────
        CreateMap<RecordAttendanceViewModel, StaffAttendance>()
            .ForMember(d => d.Id,        o => o.MapFrom(_ => Guid.NewGuid()))
            .ForMember(d => d.CreatedOn, o => o.MapFrom(_ => DateTime.UtcNow));

        CreateMap<UpdateAttendanceViewModel, StaffAttendance>()
            .ForAllMembers(o => o.Condition((src, dest, srcMember) => srcMember != null));

        CreateMap<StaffAttendance, AttendanceDetailViewModel>()
            .ForMember(d => d.StaffName, o => o.MapFrom(s => s.StaffUser != null ? s.StaffUser.Name : string.Empty));
    }
}

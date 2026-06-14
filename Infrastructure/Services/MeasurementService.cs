using Application.Common;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.ViewModels.Measurement;
using Domain.Entities;
using Domain.Enums;
using System.Text.Json;

namespace Infrastructure.Services;

public class MeasurementService : IMeasurementService
{
    private readonly IMeasurementRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public MeasurementService(IMeasurementRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    // ── Fields ──────────────────────────────────────────────────────────────
    public async Task<ApiResponse<List<MeasurementFieldViewModel>>> GetFieldsAsync(CancellationToken ct = default)
        => ApiResponse<List<MeasurementFieldViewModel>>.Ok(await _repo.GetFieldsAsync(ct));

    public async Task<ApiResponse<MeasurementFieldViewModel>> CreateFieldAsync(CreateMeasurementFieldViewModel vm, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var field = new MeasurementField
        {
            Id          = Guid.NewGuid(),
            TenantId    = _currentUser.TenantId,
            BranchId    = _currentUser.ShopId,
            ShopId      = _currentUser.ShopId,
            Name        = vm.Name,
            Unit        = vm.Unit,
            SortOrder   = vm.SortOrder,
            ActiveStatus= ActiveStatus.Active,
            CreatedBy   = _currentUser.UserId,
            CreatedOn   = now,
            UpdatedBy   = _currentUser.UserId,
            UpdatedOn   = now
        };

        await _repo.CreateFieldAsync(field, ct);
        return ApiResponse<MeasurementFieldViewModel>.Ok(new MeasurementFieldViewModel
        {
            Id = field.Id, Name = field.Name, Unit = field.Unit, SortOrder = field.SortOrder, IsSystemField = false
        });
    }

    public async Task<ApiResponse<MeasurementFieldViewModel>> UpdateFieldAsync(Guid id, UpdateMeasurementFieldViewModel vm, CancellationToken ct = default)
    {
        var field = await _repo.GetFieldByIdAsync(id, ct);
        if (field == null) return ApiResponse<MeasurementFieldViewModel>.Fail("Field not found.");
        if (field.TenantId == Guid.Empty) return ApiResponse<MeasurementFieldViewModel>.Fail("System fields cannot be modified.");

        if (vm.Name != null) field.Name = vm.Name;
        if (vm.Unit.HasValue) field.Unit = vm.Unit.Value;
        if (vm.SortOrder.HasValue) field.SortOrder = vm.SortOrder.Value;
        field.UpdatedBy = _currentUser.UserId;
        field.UpdatedOn = DateTime.UtcNow;

        await _repo.UpdateFieldAsync(field, ct);
        return ApiResponse<MeasurementFieldViewModel>.Ok(new MeasurementFieldViewModel
        {
            Id = field.Id, Name = field.Name, Unit = field.Unit, SortOrder = field.SortOrder, IsSystemField = false
        });
    }

    public async Task<ApiResponse<object>> DeleteFieldAsync(Guid id, CancellationToken ct = default)
    {
        var field = await _repo.GetFieldByIdAsync(id, ct);
        if (field == null) return ApiResponse<object>.Fail("Field not found.");
        if (field.TenantId == Guid.Empty) return ApiResponse<object>.Fail("System fields cannot be deleted.");
        await _repo.DeleteFieldAsync(id, ct);
        return ApiResponse<object>.Ok((object?)null, "Field deleted.");
    }

    // ── Templates ────────────────────────────────────────────────────────────
    public async Task<ApiResponse<List<TemplateListViewModel>>> GetTemplatesAsync(CancellationToken ct = default)
        => ApiResponse<List<TemplateListViewModel>>.Ok(await _repo.GetTemplatesAsync(ct));

    public async Task<ApiResponse<TemplateDetailViewModel>> GetTemplateDetailAsync(Guid id, CancellationToken ct = default)
    {
        var detail = await _repo.GetTemplateDetailAsync(id, ct);
        return detail == null
            ? ApiResponse<TemplateDetailViewModel>.Fail("Template not found.")
            : ApiResponse<TemplateDetailViewModel>.Ok(detail);
    }

    public async Task<ApiResponse<TemplateDetailViewModel>> CreateTemplateAsync(CreateTemplateViewModel vm, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var template = new MeasurementTemplate
        {
            Id               = Guid.NewGuid(),
            TenantId         = _currentUser.TenantId,
            BranchId         = _currentUser.ShopId,
            ShopId           = _currentUser.ShopId,
            Name             = vm.Name,
            GarmentType      = vm.GarmentType,
            IsDefault        = vm.IsDefault,
            IsSystemTemplate = false,
            DisplayOrder     = vm.DisplayOrder,
            ActiveStatus     = ActiveStatus.Active,
            CreatedBy        = _currentUser.UserId,
            CreatedOn        = now,
            UpdatedBy        = _currentUser.UserId,
            UpdatedOn        = now
        };

        foreach (var f in vm.Fields)
        {
            template.TemplateFields.Add(new TemplateField
            {
                Id                = Guid.NewGuid(),
                TenantId          = _currentUser.TenantId,
                BranchId          = _currentUser.ShopId,
                TemplateId        = template.Id,
                MeasurementFieldId= f.MeasurementFieldId,
                IsRequired        = f.IsRequired,
                SortOrder         = f.SortOrder,
                CreatedBy         = _currentUser.UserId,
                CreatedOn         = now
            });
        }

        await _repo.CreateTemplateAsync(template, ct);
        var detail = await _repo.GetTemplateDetailAsync(template.Id, ct);
        return ApiResponse<TemplateDetailViewModel>.Ok(detail!);
    }

    public async Task<ApiResponse<TemplateDetailViewModel>> UpdateTemplateAsync(Guid id, UpdateTemplateViewModel vm, CancellationToken ct = default)
    {
        var template = await _repo.GetTemplateByIdAsync(id, ct);
        if (template == null) return ApiResponse<TemplateDetailViewModel>.Fail("Template not found.");
        if (template.IsSystemTemplate) return ApiResponse<TemplateDetailViewModel>.Fail("System templates cannot be modified.");

        if (vm.Name != null) template.Name = vm.Name;
        if (vm.GarmentType.HasValue) template.GarmentType = vm.GarmentType;
        if (vm.IsDefault.HasValue) template.IsDefault = vm.IsDefault.Value;
        if (vm.DisplayOrder.HasValue) template.DisplayOrder = vm.DisplayOrder.Value;
        template.UpdatedBy = _currentUser.UserId;
        template.UpdatedOn = DateTime.UtcNow;

        if (vm.Fields != null)
        {
            template.TemplateFields.Clear();
            var now = DateTime.UtcNow;
            foreach (var f in vm.Fields)
            {
                template.TemplateFields.Add(new TemplateField
                {
                    Id = Guid.NewGuid(), TenantId = _currentUser.TenantId, BranchId = _currentUser.ShopId,
                    TemplateId = template.Id, MeasurementFieldId = f.MeasurementFieldId,
                    IsRequired = f.IsRequired, SortOrder = f.SortOrder,
                    CreatedBy = _currentUser.UserId, CreatedOn = now
                });
            }
        }

        await _repo.UpdateTemplateAsync(template, ct);
        var detail = await _repo.GetTemplateDetailAsync(template.Id, ct);
        return ApiResponse<TemplateDetailViewModel>.Ok(detail!);
    }

    public async Task<ApiResponse<object>> DeleteTemplateAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _repo.GetTemplateByIdAsync(id, ct);
        if (template == null) return ApiResponse<object>.Fail("Template not found.");
        if (template.IsSystemTemplate) return ApiResponse<object>.Fail("System templates cannot be deleted.");
        await _repo.DeleteTemplateAsync(id, ct);
        return ApiResponse<object>.Ok((object?)null, "Template deleted.");
    }

    // ── Profiles ─────────────────────────────────────────────────────────────
    public async Task<ApiResponse<List<MeasurementProfileListViewModel>>> GetCustomerProfilesAsync(Guid customerId, CancellationToken ct = default)
        => ApiResponse<List<MeasurementProfileListViewModel>>.Ok(await _repo.GetCustomerProfilesAsync(customerId, ct));

    public async Task<ApiResponse<MeasurementProfileDetailViewModel>> GetProfileDetailAsync(Guid id, CancellationToken ct = default)
    {
        var detail = await _repo.GetProfileDetailAsync(id, ct);
        return detail == null
            ? ApiResponse<MeasurementProfileDetailViewModel>.Fail("Profile not found.")
            : ApiResponse<MeasurementProfileDetailViewModel>.Ok(detail);
    }

    public async Task<ApiResponse<MeasurementProfileDetailViewModel>> SaveProfileAsync(SaveMeasurementProfileViewModel vm, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var profile = new MeasurementProfile
        {
            Id             = Guid.NewGuid(),
            TenantId       = _currentUser.TenantId,
            BranchId       = _currentUser.ShopId,
            CustomerId     = vm.CustomerId,
            TemplateId     = vm.TemplateId,
            ProfileName    = vm.ProfileName,
            FieldValuesJson= JsonSerializer.Serialize(vm.Values),
            Notes          = vm.Notes,
            CreatedBy      = _currentUser.UserId,
            CreatedOn      = now,
            UpdatedBy      = _currentUser.UserId,
            UpdatedOn      = now
        };

        await _repo.SaveProfileAsync(profile, ct);
        var detail = await _repo.GetProfileDetailAsync(profile.Id, ct);
        return ApiResponse<MeasurementProfileDetailViewModel>.Ok(detail!);
    }

    public async Task<ApiResponse<MeasurementProfileDetailViewModel>> UpdateProfileAsync(Guid id, SaveMeasurementProfileViewModel vm, CancellationToken ct = default)
    {
        var profile = await _repo.GetProfileByIdAsync(id, ct);
        if (profile == null) return ApiResponse<MeasurementProfileDetailViewModel>.Fail("Profile not found.");

        profile.TemplateId     = vm.TemplateId;
        profile.ProfileName    = vm.ProfileName;
        profile.FieldValuesJson= JsonSerializer.Serialize(vm.Values);
        profile.Notes          = vm.Notes;
        profile.UpdatedBy      = _currentUser.UserId;
        profile.UpdatedOn      = DateTime.UtcNow;

        await _repo.UpdateProfileAsync(profile, ct);
        var detail = await _repo.GetProfileDetailAsync(profile.Id, ct);
        return ApiResponse<MeasurementProfileDetailViewModel>.Ok(detail!);
    }

    public async Task<ApiResponse<object>> DeleteProfileAsync(Guid id, CancellationToken ct = default)
    {
        var profile = await _repo.GetProfileByIdAsync(id, ct);
        if (profile == null) return ApiResponse<object>.Fail("Profile not found.");
        await _repo.DeleteProfileAsync(id, ct);
        return ApiResponse<object>.Ok((object?)null, "Profile deleted.");
    }
}

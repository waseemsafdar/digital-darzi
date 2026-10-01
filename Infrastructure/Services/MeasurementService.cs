using System.Text.Json;
using Application.Common;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.ViewModels.Measurement;
using Domain.Entities;
using Domain.Enums;

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
        if (await _repo.FieldNameExistsAsync(vm.Name, null, ct))
            return ApiResponse<MeasurementFieldViewModel>.Conflict($"A measurement field named '{vm.Name}' already exists.");

        var field = new MeasurementField
        {
            Name         = vm.Name.Trim(),
            Unit         = vm.Unit,
            ValueType    = vm.ValueType,
            SortOrder    = vm.SortOrder,
            ActiveStatus = ActiveStatus.Active
        };
        await _repo.CreateFieldAsync(field, ct);
        return ApiResponse<MeasurementFieldViewModel>.Ok(ToViewModel(field));
    }

    public async Task<ApiResponse<MeasurementFieldViewModel>> UpdateFieldAsync(Guid id, UpdateMeasurementFieldViewModel vm, CancellationToken ct = default)
    {
        var field = await _repo.GetFieldByIdAsync(id, ct);
        if (field == null) return ApiResponse<MeasurementFieldViewModel>.NotFound("Field not found.");
        if (vm.Name != null && await _repo.FieldNameExistsAsync(vm.Name, id, ct))
            return ApiResponse<MeasurementFieldViewModel>.Conflict($"A measurement field named '{vm.Name}' already exists.");

        if (vm.Name != null) field.Name = vm.Name.Trim();
        if (vm.Unit.HasValue) field.Unit = vm.Unit.Value;
        if (vm.ValueType.HasValue) field.ValueType = vm.ValueType.Value;
        if (vm.SortOrder.HasValue) field.SortOrder = vm.SortOrder.Value;

        await _repo.UpdateFieldAsync(field, ct);
        return ApiResponse<MeasurementFieldViewModel>.Ok(ToViewModel(field));
    }

    public async Task<ApiResponse<object>> DeleteFieldAsync(Guid id, CancellationToken ct = default)
    {
        var field = await _repo.GetFieldByIdAsync(id, ct);
        if (field == null) return ApiResponse<object>.NotFound("Field not found.");
        await _repo.DeleteFieldAsync(id, ct);
        return ApiResponse<object>.Ok(null, "Field deleted.");
    }

    // ── Templates ────────────────────────────────────────────────────────────
    public async Task<ApiResponse<List<TemplateListViewModel>>> GetTemplatesAsync(CancellationToken ct = default)
        => ApiResponse<List<TemplateListViewModel>>.Ok(await _repo.GetTemplatesAsync(ct));

    public async Task<ApiResponse<TemplateDetailViewModel>> GetTemplateDetailAsync(Guid id, CancellationToken ct = default)
    {
        var detail = await _repo.GetTemplateDetailAsync(id, ct);
        return detail == null
            ? ApiResponse<TemplateDetailViewModel>.NotFound("Template not found.")
            : ApiResponse<TemplateDetailViewModel>.Ok(detail);
    }

    public async Task<ApiResponse<TemplateDetailViewModel>> CreateTemplateAsync(CreateTemplateViewModel vm, CancellationToken ct = default)
    {
        var fieldError = await ValidateTemplateFieldsAsync(vm.Fields, ct);
        if (fieldError != null) return fieldError;

        var template = new MeasurementTemplate
        {
            Id               = Guid.NewGuid(),
            Name             = vm.Name.Trim(),
            GarmentType      = vm.GarmentType,
            IsDefault        = vm.IsDefault,
            IsSystemTemplate = false,
            DisplayOrder     = vm.DisplayOrder,
            ActiveStatus     = ActiveStatus.Active
        };
        foreach (var f in vm.Fields)
            template.TemplateFields.Add(new TemplateField
            {
                Id = Guid.NewGuid(), TemplateId = template.Id, MeasurementFieldId = f.MeasurementFieldId,
                IsRequired = f.IsRequired, SortOrder = f.SortOrder
            });

        await _repo.CreateTemplateAsync(template, ct);
        return ApiResponse<TemplateDetailViewModel>.Ok((await _repo.GetTemplateDetailAsync(template.Id, ct))!);
    }

    public async Task<ApiResponse<TemplateDetailViewModel>> UpdateTemplateAsync(Guid id, UpdateTemplateViewModel vm, CancellationToken ct = default)
    {
        var template = await _repo.GetTemplateByIdAsync(id, ct);
        if (template == null) return ApiResponse<TemplateDetailViewModel>.NotFound("Template not found.");
        if (template.IsSystemTemplate) return ApiResponse<TemplateDetailViewModel>.Fail("System templates cannot be modified.");

        if (vm.Fields != null)
        {
            var fieldError = await ValidateTemplateFieldsAsync(vm.Fields, ct);
            if (fieldError != null) return fieldError;
        }

        if (vm.Name != null) template.Name = vm.Name.Trim();
        if (vm.GarmentType.HasValue) template.GarmentType = vm.GarmentType;
        if (vm.IsDefault.HasValue) template.IsDefault = vm.IsDefault.Value;
        if (vm.DisplayOrder.HasValue) template.DisplayOrder = vm.DisplayOrder.Value;

        if (vm.Fields != null)
        {
            template.TemplateFields.Clear();
            foreach (var f in vm.Fields)
                template.TemplateFields.Add(new TemplateField
                {
                    Id = Guid.NewGuid(), TenantId = template.TenantId, BranchId = template.BranchId, TemplateId = template.Id,
                    MeasurementFieldId = f.MeasurementFieldId, IsRequired = f.IsRequired, SortOrder = f.SortOrder
                });
        }

        await _repo.UpdateTemplateAsync(template, ct);
        return ApiResponse<TemplateDetailViewModel>.Ok((await _repo.GetTemplateDetailAsync(template.Id, ct))!);
    }

    public async Task<ApiResponse<object>> DeleteTemplateAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _repo.GetTemplateByIdAsync(id, ct);
        if (template == null) return ApiResponse<object>.NotFound("Template not found.");
        if (template.IsSystemTemplate) return ApiResponse<object>.Fail("System templates cannot be deleted.");
        await _repo.DeleteTemplateAsync(id, ct);
        return ApiResponse<object>.Ok(null, "Template deleted.");
    }

    // ── Profiles ─────────────────────────────────────────────────────────────
    public async Task<ApiResponse<List<MeasurementProfileListViewModel>>> GetCustomerProfilesAsync(Guid customerId, CancellationToken ct = default)
        => ApiResponse<List<MeasurementProfileListViewModel>>.Ok(await _repo.GetCustomerProfilesAsync(customerId, ct));

    public async Task<ApiResponse<MeasurementProfileDetailViewModel>> GetProfileDetailAsync(Guid id, CancellationToken ct = default)
    {
        var detail = await _repo.GetProfileDetailAsync(id, ct);
        return detail == null
            ? ApiResponse<MeasurementProfileDetailViewModel>.NotFound("Profile not found.")
            : ApiResponse<MeasurementProfileDetailViewModel>.Ok(detail);
    }

    public async Task<ApiResponse<MeasurementProfileDetailViewModel>> SaveProfileAsync(SaveMeasurementProfileViewModel vm, CancellationToken ct = default)
    {
        if (!await _repo.CustomerExistsAsync(vm.CustomerId, ct))
            return ApiResponse<MeasurementProfileDetailViewModel>.NotFound("Customer not found.");
        var error = await ValidateValuesAsync(vm, ct);
        if (error != null) return error;

        var profile = new MeasurementProfile
        {
            Id              = Guid.NewGuid(),
            CustomerId      = vm.CustomerId,
            TemplateId      = vm.TemplateId,
            ProfileName     = vm.ProfileName,
            FieldValuesJson = JsonSerializer.Serialize(vm.Values),
            TextValuesJson  = vm.TextValues is { Count: > 0 } ? JsonSerializer.Serialize(vm.TextValues) : null,
            Notes           = vm.Notes,
            CurrentVersion  = 1
        };
        _repo.AddVersion(NewVersion(profile, 1));
        await _repo.SaveProfileAsync(profile, ct);
        return ApiResponse<MeasurementProfileDetailViewModel>.Ok((await _repo.GetProfileDetailAsync(profile.Id, ct))!);
    }

    /// <summary>
    /// Updates the profile and appends a new immutable version. Orders placed earlier keep the
    /// snapshot they were created with; a profile created before versioning gets its original
    /// values preserved as version 1 on first edit.
    /// </summary>
    public async Task<ApiResponse<MeasurementProfileDetailViewModel>> UpdateProfileAsync(Guid id, SaveMeasurementProfileViewModel vm, CancellationToken ct = default)
    {
        var profile = await _repo.GetProfileByIdAsync(id, ct);
        if (profile == null) return ApiResponse<MeasurementProfileDetailViewModel>.NotFound("Profile not found.");
        if (vm.CustomerId != Guid.Empty && vm.CustomerId != profile.CustomerId)
            return ApiResponse<MeasurementProfileDetailViewModel>.Fail("A profile cannot be moved to a different customer.");
        vm.CustomerId = profile.CustomerId;
        var error = await ValidateValuesAsync(vm, ct);
        if (error != null) return error;

        if (!await _repo.HasVersionsAsync(profile.Id, ct))
            _repo.AddVersion(NewVersion(profile, profile.CurrentVersion));   // preserve pre-versioning values

        profile.TemplateId      = vm.TemplateId;
        profile.ProfileName     = vm.ProfileName;
        profile.FieldValuesJson = JsonSerializer.Serialize(vm.Values);
        profile.TextValuesJson  = vm.TextValues is { Count: > 0 } ? JsonSerializer.Serialize(vm.TextValues) : null;
        profile.Notes           = vm.Notes;
        profile.CurrentVersion += 1;
        _repo.AddVersion(NewVersion(profile, profile.CurrentVersion));

        await _repo.UpdateProfileAsync(profile, ct);
        return ApiResponse<MeasurementProfileDetailViewModel>.Ok((await _repo.GetProfileDetailAsync(profile.Id, ct))!);
    }

    public async Task<ApiResponse<object>> DeleteProfileAsync(Guid id, CancellationToken ct = default)
    {
        var profile = await _repo.GetProfileByIdAsync(id, ct);
        if (profile == null) return ApiResponse<object>.NotFound("Profile not found.");
        await _repo.DeleteProfileAsync(id, ct);
        return ApiResponse<object>.Ok(null, "Profile deleted.");
    }

    public async Task<ApiResponse<List<MeasurementProfileVersionViewModel>>> GetProfileVersionsAsync(Guid id, CancellationToken ct = default)
    {
        var profile = await _repo.GetProfileByIdAsync(id, ct);
        if (profile == null) return ApiResponse<List<MeasurementProfileVersionViewModel>>.NotFound("Profile not found.");
        return ApiResponse<List<MeasurementProfileVersionViewModel>>.Ok(await _repo.GetProfileVersionsAsync(id, ct));
    }

    // ── Validation ───────────────────────────────────────────────────────────

    /// <summary>
    /// Values must belong to the template; numbers go to Number fields and text to Text fields;
    /// every field the template marks required must be present; numbers must be 0–1000.
    /// </summary>
    private async Task<ApiResponse<MeasurementProfileDetailViewModel>?> ValidateValuesAsync(SaveMeasurementProfileViewModel vm, CancellationToken ct)
    {
        var template = await _repo.GetTemplateByIdAsync(vm.TemplateId, ct);
        if (template == null) return ApiResponse<MeasurementProfileDetailViewModel>.NotFound("Template not found.");

        var templateFieldIds = template.TemplateFields.Select(f => f.MeasurementFieldId).ToHashSet();
        var texts = vm.TextValues ?? new Dictionary<Guid, string>();
        var fields = await _repo.GetFieldsByIdAsync(templateFieldIds.Concat(vm.Values.Keys).Concat(texts.Keys), ct);

        var errors = new List<string>();
        foreach (var id in vm.Values.Keys.Concat(texts.Keys).Where(id => !templateFieldIds.Contains(id)).Distinct())
            errors.Add($"Field {fields.GetValueOrDefault(id)?.Name ?? id.ToString()} is not part of template '{template.Name}'.");

        foreach (var (id, value) in vm.Values)
        {
            if (fields.TryGetValue(id, out var f) && f.ValueType == MeasurementValueType.Text)
                errors.Add($"{f.Name} takes text, not a number.");
            if (value < 0 || value > 1000)
                errors.Add($"{fields.GetValueOrDefault(id)?.Name ?? id.ToString()} must be between 0 and 1000.");
        }
        foreach (var id in texts.Keys)
            if (fields.TryGetValue(id, out var f) && f.ValueType == MeasurementValueType.Number)
                errors.Add($"{f.Name} takes a number, not text.");

        foreach (var required in template.TemplateFields.Where(tf => tf.IsRequired))
        {
            var present = vm.Values.ContainsKey(required.MeasurementFieldId)
                || (texts.TryGetValue(required.MeasurementFieldId, out var t) && !string.IsNullOrWhiteSpace(t));
            if (!present)
                errors.Add($"{fields.GetValueOrDefault(required.MeasurementFieldId)?.Name ?? "A required field"} is required for '{template.Name}'.");
        }

        return errors.Count == 0 ? null : ApiResponse<MeasurementProfileDetailViewModel>.Fail(errors);
    }

    private async Task<ApiResponse<TemplateDetailViewModel>?> ValidateTemplateFieldsAsync(List<TemplateFieldItemViewModel> items, CancellationToken ct)
    {
        if (items.Select(i => i.MeasurementFieldId).Distinct().Count() != items.Count)
            return ApiResponse<TemplateDetailViewModel>.Fail("A field is listed twice in the template.");
        var found = await _repo.GetFieldsByIdAsync(items.Select(i => i.MeasurementFieldId), ct);
        return found.Count == items.Count ? null : ApiResponse<TemplateDetailViewModel>.NotFound("One or more measurement fields were not found.");
    }

    private MeasurementProfileVersion NewVersion(MeasurementProfile profile, int versionNo) => new()
    {
        ProfileId       = profile.Id,
        VersionNo       = versionNo,
        TemplateId      = profile.TemplateId,
        FieldValuesJson = profile.FieldValuesJson,
        TextValuesJson  = profile.TextValuesJson,
        Notes           = profile.Notes,
        TenantId        = profile.TenantId,
        BranchId        = profile.BranchId,
        CreatedBy       = _currentUser.UserId
    };

    private static MeasurementFieldViewModel ToViewModel(MeasurementField f) => new()
    {
        Id = f.Id, Name = f.Name, Unit = f.Unit, ValueType = f.ValueType, SortOrder = f.SortOrder, IsSystemField = false
    };
}

using System.Text.Json;
using Application.Common;
using Application.Interfaces.Services;
using Application.ViewModels.Operations;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public interface IGarmentAttributeService
{
    Task<ApiResponse<List<GarmentAttributeViewModel>>> GetAsync(GarmentType? garmentType, CancellationToken ct = default);
    Task<ApiResponse<GarmentAttributeViewModel>> CreateAsync(SaveGarmentAttributeViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<GarmentAttributeViewModel>> UpdateAsync(Guid id, SaveGarmentAttributeViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<object>> SeedDefaultsAsync(CancellationToken ct = default);
}

public sealed class GarmentAttributeService : IGarmentAttributeService
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantSetupService _setup;
    private readonly ICurrentUserService _currentUser;

    public GarmentAttributeService(ApplicationDbContext db, ITenantSetupService setup, ICurrentUserService currentUser)
    {
        _db = db;
        _setup = setup;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<List<GarmentAttributeViewModel>>> GetAsync(GarmentType? garmentType, CancellationToken ct = default)
    {
        var q = _db.GarmentAttributes.AsNoTracking();
        if (garmentType.HasValue) q = q.Where(a => a.GarmentType == null || a.GarmentType == garmentType);
        var rows = await q.OrderBy(a => a.GarmentType).ThenBy(a => a.SortOrder).ThenBy(a => a.Name).ToListAsync(ct);
        return ApiResponse<List<GarmentAttributeViewModel>>.Ok(rows.Select(ToViewModel).ToList());
    }

    public async Task<ApiResponse<GarmentAttributeViewModel>> CreateAsync(SaveGarmentAttributeViewModel vm, CancellationToken ct = default)
    {
        var name = vm.Name.Trim();
        if (await _db.GarmentAttributes.AnyAsync(a => a.GarmentType == vm.GarmentType && a.Name == name, ct))
            return ApiResponse<GarmentAttributeViewModel>.Conflict($"'{name}' already exists for this garment type.");
        var row = new GarmentAttribute();
        Apply(row, vm);
        _db.GarmentAttributes.Add(row);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<GarmentAttributeViewModel>.Ok(ToViewModel(row));
    }

    public async Task<ApiResponse<GarmentAttributeViewModel>> UpdateAsync(Guid id, SaveGarmentAttributeViewModel vm, CancellationToken ct = default)
    {
        var row = await _db.GarmentAttributes.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (row == null) return ApiResponse<GarmentAttributeViewModel>.NotFound("Attribute not found.");
        var name = vm.Name.Trim();
        if (await _db.GarmentAttributes.AnyAsync(a => a.Id != id && a.GarmentType == vm.GarmentType && a.Name == name, ct))
            return ApiResponse<GarmentAttributeViewModel>.Conflict($"'{name}' already exists for this garment type.");
        Apply(row, vm);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<GarmentAttributeViewModel>.Ok(ToViewModel(row));
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var row = await _db.GarmentAttributes.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (row == null) return ApiResponse<object>.NotFound("Attribute not found.");
        row.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(null, "Attribute deleted. Existing orders keep their values.");
    }

    /// <summary>Back-fills the starter catalogue (and expense categories) for a branch created before they existed.</summary>
    public async Task<ApiResponse<object>> SeedDefaultsAsync(CancellationToken ct = default)
    {
        await _setup.SetupNewTenantAsync(_currentUser.TenantId, _currentUser.ShopId, _currentUser.UserId, ct);
        return ApiResponse<object>.Ok(null, "Defaults added where the branch had none.");
    }

    private static void Apply(GarmentAttribute row, SaveGarmentAttributeViewModel vm)
    {
        row.GarmentType = vm.GarmentType;
        row.Name = vm.Name.Trim();
        var options = vm.Options.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        row.OptionsJson = options.Count > 0 ? JsonSerializer.Serialize(options) : null;
        row.IsRequired = vm.IsRequired;
        row.SortOrder = vm.SortOrder;
    }

    private static GarmentAttributeViewModel ToViewModel(GarmentAttribute a) => new()
    {
        Id = a.Id, GarmentType = a.GarmentType, Name = a.Name, IsRequired = a.IsRequired, SortOrder = a.SortOrder,
        Options = string.IsNullOrWhiteSpace(a.OptionsJson) ? new() : JsonSerializer.Deserialize<List<string>>(a.OptionsJson) ?? new()
    };
}

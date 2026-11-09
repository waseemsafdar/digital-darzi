using Application.Common;
using Application.ViewModels.Operations;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

/// <summary>
/// The current branch's garment types: the built-in list merged with the shop's own rows
/// (renames, order, hidden flags and custom types). Read through the branch query filter.
/// </summary>
public sealed class GarmentTypeCatalogue
{
    private readonly Dictionary<int, GarmentTypeViewModel> _byCode;

    private GarmentTypeCatalogue(List<GarmentTypeViewModel> all)
    {
        All = all;
        _byCode = all.ToDictionary(t => t.Code);
    }

    /// <summary>Every known type, hidden ones included, in display order.</summary>
    public IReadOnlyList<GarmentTypeViewModel> All { get; }

    public static async Task<GarmentTypeCatalogue> LoadAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        var rows = await db.GarmentTypeOptions.AsNoTracking().ToListAsync(ct);
        var list = new List<GarmentTypeViewModel>();

        for (var i = 0; i < GarmentTypeDefaults.BuiltIn.Count; i++)
        {
            var (code, name) = GarmentTypeDefaults.BuiltIn[i];
            var row = rows.FirstOrDefault(r => r.Code == code);
            list.Add(new GarmentTypeViewModel
            {
                Code = code, DefaultName = name, IsBuiltIn = true,
                Name = row?.Name is { Length: > 0 } n ? n : name,
                IsHidden = row?.IsHidden ?? false,
                SortOrder = row?.SortOrder ?? (i + 1) * 10
            });
        }
        foreach (var row in rows.Where(r => !GarmentTypeDefaults.IsBuiltIn(r.Code)))
            list.Add(new GarmentTypeViewModel { Code = row.Code, Name = row.Name, IsHidden = row.IsHidden, SortOrder = row.SortOrder });

        return new GarmentTypeCatalogue(list.OrderBy(t => t.SortOrder).ThenBy(t => t.Code).ToList());
    }

    public bool Exists(int code) => _byCode.ContainsKey(code);

    public bool IsHidden(int code) => _byCode.TryGetValue(code, out var t) && t.IsHidden;

    /// <summary>The shop's name for a code; never throws for an unknown or deleted code.</summary>
    public string NameOf(int code) => _byCode.TryGetValue(code, out var t) ? t.Name
        : Enum.IsDefined(typeof(GarmentType), code) ? ((GarmentType)code).ToString() : "Garment";

    public string NameOf(GarmentType code) => NameOf((int)code);

    /// <summary>Null when the code can be used on a new record, otherwise the reason it cannot.</summary>
    public string? CheckUsable(int code, bool allowHidden = false)
    {
        if (!Exists(code)) return $"Garment type {code} does not exist in this shop.";
        if (!allowHidden && IsHidden(code)) return $"'{NameOf(code)}' is hidden. Show it again under Garment types to use it.";
        return null;
    }
}

public interface IGarmentTypeService
{
    Task<ApiResponse<List<GarmentTypeViewModel>>> GetAsync(bool includeHidden, CancellationToken ct = default);
    Task<ApiResponse<GarmentTypeViewModel>> CreateAsync(CreateGarmentTypeViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<GarmentTypeViewModel>> UpdateAsync(int code, UpdateGarmentTypeViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<List<GarmentTypeViewModel>>> ReorderAsync(ReorderGarmentTypesViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<object>> DeleteAsync(int code, CancellationToken ct = default);
}

public sealed class GarmentTypeService : IGarmentTypeService
{
    private readonly ApplicationDbContext _db;
    public GarmentTypeService(ApplicationDbContext db) => _db = db;

    public async Task<ApiResponse<List<GarmentTypeViewModel>>> GetAsync(bool includeHidden, CancellationToken ct = default)
    {
        var catalogue = await GarmentTypeCatalogue.LoadAsync(_db, ct);
        var list = catalogue.All.Where(t => includeHidden || !t.IsHidden).Select(Copy).ToList();
        if (includeHidden)
        {
            var usage = await _db.OrderItems.AsNoTracking()
                .GroupBy(i => i.GarmentType).Select(g => new { Code = (int)g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Code, x => x.Count, ct);
            foreach (var t in list) t.UsedByItems = usage.GetValueOrDefault(t.Code);
        }
        return ApiResponse<List<GarmentTypeViewModel>>.Ok(list);
    }

    public async Task<ApiResponse<GarmentTypeViewModel>> CreateAsync(CreateGarmentTypeViewModel vm, CancellationToken ct = default)
    {
        var name = Clean(vm.Name);
        var catalogue = await GarmentTypeCatalogue.LoadAsync(_db, ct);
        if (NameTaken(catalogue, name, exceptCode: null))
            return ApiResponse<GarmentTypeViewModel>.Conflict($"'{name}' already exists. Show it again if it is hidden.");

        // Deleted rows are included so a deleted custom code is never handed out again:
        // old orders that used it must not suddenly show the new name.
        var maxCode = await _db.GarmentTypeOptions.IgnoreQueryFilters()
            .Where(r => r.TenantId == _db.CurrentTenantId && r.BranchId == _db.CurrentShopId)
            .MaxAsync(r => (int?)r.Code, ct) ?? 0;
        var row = new GarmentTypeOption
        {
            Code = Math.Max(GarmentTypeDefaults.FirstCustomCode, maxCode + 1),
            Name = name,
            SortOrder = (catalogue.All.Count == 0 ? 0 : catalogue.All.Max(t => t.SortOrder)) + 10
        };
        _db.GarmentTypeOptions.Add(row);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<GarmentTypeViewModel>.Ok(new GarmentTypeViewModel { Code = row.Code, Name = row.Name, SortOrder = row.SortOrder, UsedByItems = 0 });
    }

    public async Task<ApiResponse<GarmentTypeViewModel>> UpdateAsync(int code, UpdateGarmentTypeViewModel vm, CancellationToken ct = default)
    {
        var catalogue = await GarmentTypeCatalogue.LoadAsync(_db, ct);
        var current = catalogue.All.FirstOrDefault(t => t.Code == code);
        if (current == null) return ApiResponse<GarmentTypeViewModel>.NotFound("Garment type not found.");

        var name = Clean(vm.Name);
        if (NameTaken(catalogue, name, exceptCode: code))
            return ApiResponse<GarmentTypeViewModel>.Conflict($"'{name}' already exists.");
        if (vm.IsHidden && !current.IsHidden && catalogue.All.Count(t => !t.IsHidden) <= 1)
            return ApiResponse<GarmentTypeViewModel>.Fail("Keep at least one garment type visible.");

        var row = await RowForAsync(code, current, ct);
        row.Name = name;
        row.IsHidden = vm.IsHidden;
        await _db.SaveChangesAsync(ct);

        current = Copy(current);
        current.Name = name;
        current.IsHidden = vm.IsHidden;
        return ApiResponse<GarmentTypeViewModel>.Ok(current);
    }

    public async Task<ApiResponse<List<GarmentTypeViewModel>>> ReorderAsync(ReorderGarmentTypesViewModel vm, CancellationToken ct = default)
    {
        var catalogue = await GarmentTypeCatalogue.LoadAsync(_db, ct);
        var unknown = vm.Codes.Where(c => !catalogue.Exists(c)).ToList();
        if (unknown.Count > 0) return ApiResponse<List<GarmentTypeViewModel>>.NotFound($"Unknown garment type: {string.Join(", ", unknown)}.");

        var ordered = vm.Codes.Concat(catalogue.All.Select(t => t.Code).Where(c => !vm.Codes.Contains(c))).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var current = catalogue.All.First(t => t.Code == ordered[i]);
            var sort = (i + 1) * 10;
            if (current.SortOrder == sort) continue;
            var row = await RowForAsync(current.Code, current, ct);
            row.SortOrder = sort;
        }
        await _db.SaveChangesAsync(ct);
        return await GetAsync(includeHidden: true, ct);
    }

    public async Task<ApiResponse<object>> DeleteAsync(int code, CancellationToken ct = default)
    {
        if (GarmentTypeDefaults.IsBuiltIn(code))
            return ApiResponse<object>.Fail("Built-in garment types cannot be deleted. Hide it instead.");
        var row = await _db.GarmentTypeOptions.FirstOrDefaultAsync(r => r.Code == code, ct);
        if (row == null) return ApiResponse<object>.NotFound("Garment type not found.");

        var garment = (GarmentType)code;
        var used = await _db.OrderItems.AnyAsync(i => i.GarmentType == garment, ct)
                   || await _db.KarigarRates.AnyAsync(r => r.GarmentType == garment, ct)
                   || await _db.GarmentAttributes.AnyAsync(a => a.GarmentType == garment, ct)
                   || await _db.MeasurementTemplates.AnyAsync(t => t.GarmentType == garment, ct);
        if (used)
            return ApiResponse<object>.Conflict($"'{row.Name}' is used by orders, rates or styles. Hide it instead so old records keep their name.");

        row.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(null, "Garment type deleted.");
    }

    /// <summary>The shop's row for a code, creating the override row for a built-in type on first change.</summary>
    private async Task<GarmentTypeOption> RowForAsync(int code, GarmentTypeViewModel current, CancellationToken ct)
    {
        var row = await _db.GarmentTypeOptions.FirstOrDefaultAsync(r => r.Code == code, ct);
        if (row != null) return row;

        // A built-in override that was soft-deleted earlier still holds the unique (branch, code) slot.
        row = await _db.GarmentTypeOptions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.TenantId == _db.CurrentTenantId && r.BranchId == _db.CurrentShopId && r.Code == code, ct);
        if (row != null)
        {
            row.IsDeleted = false;
        }
        else
        {
            row = new GarmentTypeOption { Code = code };
            _db.GarmentTypeOptions.Add(row);
        }
        row.Name = current.Name;
        row.IsHidden = current.IsHidden;
        row.SortOrder = current.SortOrder;
        return row;
    }

    private static bool NameTaken(GarmentTypeCatalogue catalogue, string name, int? exceptCode) =>
        catalogue.All.Any(t => t.Code != exceptCode && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

    private static string Clean(string name) => string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static GarmentTypeViewModel Copy(GarmentTypeViewModel t) => new()
    {
        Code = t.Code, Name = t.Name, DefaultName = t.DefaultName, IsBuiltIn = t.IsBuiltIn,
        IsHidden = t.IsHidden, SortOrder = t.SortOrder, UsedByItems = t.UsedByItems
    };
}

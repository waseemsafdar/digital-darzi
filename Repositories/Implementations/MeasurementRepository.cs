using System.Text.Json;
using Application.Common;
using Application.Interfaces.Repositories;
using Application.ViewModels.Measurement;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Implementations;

/// <summary>
/// Measurement catalogue and customer profiles. Every query runs through the tenant + branch
/// filter. (The list endpoints previously used IgnoreQueryFilters with no tenant predicate and
/// returned every tenant's fields and templates.) Fields and templates deactivated by a delete
/// are hidden from the lists but stay resolvable for historical profiles and orders.
/// </summary>
public class MeasurementRepository : IMeasurementRepository
{
    private readonly ApplicationDbContext _db;
    public MeasurementRepository(ApplicationDbContext db) => _db = db;

    // ── Fields ──────────────────────────────────────────────────────────────
    public async Task<List<MeasurementFieldViewModel>> GetFieldsAsync(CancellationToken ct = default)
        => await _db.MeasurementFields.AsNoTracking()
            .Where(f => f.ActiveStatus == ActiveStatus.Active)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.Name)
            .Select(f => new MeasurementFieldViewModel
            {
                Id            = f.Id,
                Name          = f.Name,
                Unit          = f.Unit,
                ValueType     = f.ValueType,
                SortOrder     = f.SortOrder,
                IsSystemField = false
            })
            .ToListAsync(ct);

    public async Task<MeasurementField?> GetFieldByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.MeasurementFields.FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<Dictionary<Guid, MeasurementField>> GetFieldsByIdAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var list = ids.Distinct().ToList();
        return await _db.MeasurementFields.AsNoTracking().Where(f => list.Contains(f.Id)).ToDictionaryAsync(f => f.Id, ct);
    }

    public async Task<bool> FieldNameExistsAsync(string name, Guid? excludeId, CancellationToken ct = default)
        => await _db.MeasurementFields.AnyAsync(f => EF.Functions.ILike(f.Name, Like.Escape(name.Trim()), Like.EscapeChar)
            && (excludeId == null || f.Id != excludeId), ct);

    public async Task<MeasurementField> CreateFieldAsync(MeasurementField field, CancellationToken ct = default)
    {
        _db.MeasurementFields.Add(field);
        await _db.SaveChangesAsync(ct);
        return field;
    }

    public async Task UpdateFieldAsync(MeasurementField field, CancellationToken ct = default)
    {
        if (_db.Entry(field).State == EntityState.Detached) _db.MeasurementFields.Update(field);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteFieldAsync(Guid id, CancellationToken ct = default)
    {
        var f = await GetFieldByIdAsync(id, ct);
        if (f == null) return;
        f.ActiveStatus = ActiveStatus.Inactive;
        await _db.SaveChangesAsync(ct);
    }

    // ── Templates ────────────────────────────────────────────────────────────
    public async Task<List<TemplateListViewModel>> GetTemplatesAsync(CancellationToken ct = default)
        => await _db.MeasurementTemplates.AsNoTracking()
            .Where(t => t.ActiveStatus == ActiveStatus.Active)
            .OrderBy(t => t.DisplayOrder).ThenBy(t => t.Name)
            .Select(t => new TemplateListViewModel
            {
                Id               = t.Id,
                Name             = t.Name,
                GarmentType      = t.GarmentType,
                IsDefault        = t.IsDefault,
                IsSystemTemplate = t.IsSystemTemplate,
                DisplayOrder     = t.DisplayOrder,
                FieldCount       = t.TemplateFields.Count
            })
            .ToListAsync(ct);

    public async Task<TemplateDetailViewModel?> GetTemplateDetailAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _db.MeasurementTemplates.AsNoTracking()
            .Include(x => x.TemplateFields).ThenInclude(tf => tf.MeasurementField)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t == null) return null;

        return new TemplateDetailViewModel
        {
            Id               = t.Id,
            Name             = t.Name,
            GarmentType      = t.GarmentType,
            IsDefault        = t.IsDefault,
            IsSystemTemplate = t.IsSystemTemplate,
            DisplayOrder     = t.DisplayOrder,
            Fields = t.TemplateFields.OrderBy(tf => tf.SortOrder).Select(tf => new TemplateFieldDetailViewModel
            {
                MeasurementFieldId = tf.MeasurementFieldId,
                FieldName          = tf.MeasurementField?.Name ?? string.Empty,
                Unit               = tf.MeasurementField?.Unit ?? MeasurementUnit.Inch,
                ValueType          = tf.MeasurementField?.ValueType ?? MeasurementValueType.Number,
                IsRequired         = tf.IsRequired,
                SortOrder          = tf.SortOrder
            }).ToList()
        };
    }

    public async Task<MeasurementTemplate?> GetTemplateByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.MeasurementTemplates.Include(t => t.TemplateFields).FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<MeasurementTemplate> CreateTemplateAsync(MeasurementTemplate template, CancellationToken ct = default)
    {
        _db.MeasurementTemplates.Add(template);
        await _db.SaveChangesAsync(ct);
        return template;
    }

    public async Task UpdateTemplateAsync(MeasurementTemplate template, CancellationToken ct = default)
    {
        // The service loads the template tracked with its fields and replaces the child rows;
        // the change tracker turns that into deletes and inserts.
        if (_db.Entry(template).State == EntityState.Detached) _db.MeasurementTemplates.Update(template);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteTemplateAsync(Guid id, CancellationToken ct = default)
    {
        var t = await GetTemplateByIdAsync(id, ct);
        if (t == null) return;
        t.ActiveStatus = ActiveStatus.Inactive;
        await _db.SaveChangesAsync(ct);
    }

    // ── Profiles ─────────────────────────────────────────────────────────────
    public async Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken ct = default)
        => await _db.Customers.AnyAsync(c => c.Id == customerId, ct);

    public async Task<List<MeasurementProfileListViewModel>> GetCustomerProfilesAsync(Guid customerId, CancellationToken ct = default)
        => await _db.MeasurementProfiles.AsNoTracking()
            .Where(p => p.CustomerId == customerId)
            .OrderByDescending(p => p.UpdatedOn)
            .Select(p => new MeasurementProfileListViewModel
            {
                Id             = p.Id,
                TemplateId     = p.TemplateId,
                TemplateName   = p.Template != null ? p.Template.Name : string.Empty,
                ProfileName    = p.ProfileName,
                GarmentType    = p.Template != null ? p.Template.GarmentType : null,
                CurrentVersion = p.CurrentVersion,
                UpdatedOn      = p.UpdatedOn
            })
            .ToListAsync(ct);

    public async Task<MeasurementProfileDetailViewModel?> GetProfileDetailAsync(Guid id, CancellationToken ct = default)
    {
        var p = await _db.MeasurementProfiles.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Template)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p == null) return null;

        var numbers = Read<decimal>(p.FieldValuesJson);
        var texts = Read<string>(p.TextValuesJson);
        var names = await FieldNamesAsync(numbers.Keys.Concat(texts.Keys), ct);

        return new MeasurementProfileDetailViewModel
        {
            Id             = p.Id,
            CustomerId     = p.CustomerId,
            CustomerName   = p.Customer?.Name ?? string.Empty,
            TemplateId     = p.TemplateId,
            TemplateName   = p.Template?.Name ?? string.Empty,
            ProfileName    = p.ProfileName,
            GarmentType    = p.Template?.GarmentType,
            Values         = ByName(numbers, names),
            TextValues     = ByName(texts, names),
            ValuesById     = numbers,
            TextValuesById = texts,
            CurrentVersion = p.CurrentVersion,
            Notes          = p.Notes,
            UpdatedOn      = p.UpdatedOn
        };
    }

    public async Task<MeasurementProfile?> GetProfileByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.MeasurementProfiles.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<MeasurementProfile> SaveProfileAsync(MeasurementProfile profile, CancellationToken ct = default)
    {
        _db.MeasurementProfiles.Add(profile);
        await _db.SaveChangesAsync(ct);
        return profile;
    }

    public async Task UpdateProfileAsync(MeasurementProfile profile, CancellationToken ct = default)
    {
        if (_db.Entry(profile).State == EntityState.Detached) _db.MeasurementProfiles.Update(profile);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Soft delete — orders keep referring to the profile they were measured from.</summary>
    public async Task DeleteProfileAsync(Guid id, CancellationToken ct = default)
    {
        var p = await GetProfileByIdAsync(id, ct);
        if (p == null) return;
        p.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
    }

    public Task<bool> HasVersionsAsync(Guid profileId, CancellationToken ct = default)
        => _db.MeasurementProfileVersions.AnyAsync(v => v.ProfileId == profileId, ct);

    public void AddVersion(MeasurementProfileVersion version) => _db.MeasurementProfileVersions.Add(version);

    public async Task<List<MeasurementProfileVersionViewModel>> GetProfileVersionsAsync(Guid profileId, CancellationToken ct = default)
    {
        var rows = await _db.MeasurementProfileVersions.AsNoTracking()
            .Where(v => v.ProfileId == profileId)
            .OrderByDescending(v => v.VersionNo)
            .ToListAsync(ct);

        var all = rows.SelectMany(r => Read<decimal>(r.FieldValuesJson).Keys.Concat(Read<string>(r.TextValuesJson).Keys));
        var names = await FieldNamesAsync(all, ct);
        return rows.Select(r => new MeasurementProfileVersionViewModel
        {
            VersionNo  = r.VersionNo,
            TemplateId = r.TemplateId,
            Values     = ByName(Read<decimal>(r.FieldValuesJson), names),
            TextValues = ByName(Read<string>(r.TextValuesJson), names),
            Notes      = r.Notes,
            CreatedBy  = r.CreatedBy,
            CreatedOn  = r.CreatedOn
        }).ToList();
    }

    private async Task<Dictionary<Guid, string>> FieldNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0) return new();
        return await _db.MeasurementFields.AsNoTracking().Where(f => list.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.Name, ct);
    }

    private static Dictionary<string, T> ByName<T>(Dictionary<Guid, T> values, IReadOnlyDictionary<Guid, string> names)
    {
        var result = new Dictionary<string, T>();
        foreach (var (id, value) in values)
        {
            var key = names.GetValueOrDefault(id, id.ToString());
            if (result.ContainsKey(key)) key = $"{key} ({id.ToString()[..8]})";   // two fields with the same name
            result[key] = value;
        }
        return result;
    }

    internal static Dictionary<Guid, T> Read<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<Guid, T>>(json) ?? new(); }
        catch (JsonException) { return new(); }
    }
}

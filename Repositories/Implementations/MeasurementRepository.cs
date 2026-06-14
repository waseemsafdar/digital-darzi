using Application.Common;
using Application.Interfaces.Repositories;
using Application.ViewModels.Measurement;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Repositories.Implementations;

public class MeasurementRepository : IMeasurementRepository
{
    private readonly ApplicationDbContext _db;
    public MeasurementRepository(ApplicationDbContext db) => _db = db;

    // ── Fields ──────────────────────────────────────────────────────────────
    public async Task<List<MeasurementFieldViewModel>> GetFieldsAsync(CancellationToken ct = default)
        => await _db.MeasurementFields
            .IgnoreQueryFilters()
            .OrderBy(f => f.SortOrder)
            .Select(f => new MeasurementFieldViewModel
            {
                Id           = f.Id,
                Name         = f.Name,
                Unit         = f.Unit,
                SortOrder    = f.SortOrder,
                IsSystemField= f.TenantId == Guid.Empty
            })
            .ToListAsync(ct);

    public async Task<MeasurementField?> GetFieldByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.MeasurementFields.FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<MeasurementField> CreateFieldAsync(MeasurementField field, CancellationToken ct = default)
    {
        _db.MeasurementFields.Add(field);
        await _db.SaveChangesAsync(ct);
        return field;
    }

    public async Task UpdateFieldAsync(MeasurementField field, CancellationToken ct = default)
    {
        _db.MeasurementFields.Update(field);
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
        => await _db.MeasurementTemplates
            .IgnoreQueryFilters()
            .OrderBy(t => t.DisplayOrder)
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
        var t = await _db.MeasurementTemplates
            .IgnoreQueryFilters()
            .Include(x => x.TemplateFields)
                .ThenInclude(tf => tf.MeasurementField)
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
                IsRequired         = tf.IsRequired,
                SortOrder          = tf.SortOrder
            }).ToList()
        };
    }

    public async Task<MeasurementTemplate?> GetTemplateByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.MeasurementTemplates
            .Include(t => t.TemplateFields)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<MeasurementTemplate> CreateTemplateAsync(MeasurementTemplate template, CancellationToken ct = default)
    {
        _db.MeasurementTemplates.Add(template);
        await _db.SaveChangesAsync(ct);
        return template;
    }

    public async Task UpdateTemplateAsync(MeasurementTemplate template, CancellationToken ct = default)
    {
        _db.MeasurementTemplates.Update(template);
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
    public async Task<List<MeasurementProfileListViewModel>> GetCustomerProfilesAsync(Guid customerId, CancellationToken ct = default)
        => await _db.MeasurementProfiles
            .Include(p => p.Template)
            .Where(p => p.CustomerId == customerId)
            .OrderByDescending(p => p.UpdatedOn)
            .Select(p => new MeasurementProfileListViewModel
            {
                Id           = p.Id,
                TemplateId   = p.TemplateId,
                TemplateName = p.Template != null ? p.Template.Name : string.Empty,
                ProfileName  = p.ProfileName,
                GarmentType  = p.Template != null ? p.Template.GarmentType : null,
                UpdatedOn    = p.UpdatedOn
            })
            .ToListAsync(ct);

    public async Task<MeasurementProfileDetailViewModel?> GetProfileDetailAsync(Guid id, CancellationToken ct = default)
    {
        var p = await _db.MeasurementProfiles
            .Include(x => x.Customer)
            .Include(x => x.Template)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p == null) return null;

        // Deserialize JSONB field values
        Dictionary<Guid, decimal> rawValues = p.FieldValuesJson != null
            ? JsonSerializer.Deserialize<Dictionary<Guid, decimal>>(p.FieldValuesJson) ?? new()
            : new();

        // Build field-name→value map using template fields
        var fieldIds = rawValues.Keys.ToList();
        var fields = await _db.MeasurementFields
            .IgnoreQueryFilters()
            .Where(f => fieldIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.Name, ct);

        return new MeasurementProfileDetailViewModel
        {
            Id           = p.Id,
            CustomerId   = p.CustomerId,
            CustomerName = p.Customer?.Name ?? string.Empty,
            TemplateId   = p.TemplateId,
            TemplateName = p.Template?.Name ?? string.Empty,
            ProfileName  = p.ProfileName,
            GarmentType  = p.Template?.GarmentType,
            Values       = rawValues.ToDictionary(
                               kv => fields.GetValueOrDefault(kv.Key, kv.Key.ToString()),
                               kv => kv.Value),
            Notes        = p.Notes,
            UpdatedOn    = p.UpdatedOn
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
        _db.MeasurementProfiles.Update(profile);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteProfileAsync(Guid id, CancellationToken ct = default)
    {
        var p = await GetProfileByIdAsync(id, ct);
        if (p == null) return;
        _db.MeasurementProfiles.Remove(p);
        await _db.SaveChangesAsync(ct);
    }
}

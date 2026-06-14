using Application.Common;
using Application.ViewModels.Measurement;
using Domain.Entities;

namespace Application.Interfaces.Repositories;

public interface IMeasurementRepository
{
    // ── Fields ──────────────────────────────────────────────────────────────
    Task<List<MeasurementFieldViewModel>> GetFieldsAsync(CancellationToken ct = default);
    Task<MeasurementField?> GetFieldByIdAsync(Guid id, CancellationToken ct = default);
    Task<MeasurementField> CreateFieldAsync(MeasurementField field, CancellationToken ct = default);
    Task UpdateFieldAsync(MeasurementField field, CancellationToken ct = default);
    Task DeleteFieldAsync(Guid id, CancellationToken ct = default);

    // ── Templates ────────────────────────────────────────────────────────────
    Task<List<TemplateListViewModel>> GetTemplatesAsync(CancellationToken ct = default);
    Task<TemplateDetailViewModel?> GetTemplateDetailAsync(Guid id, CancellationToken ct = default);
    Task<MeasurementTemplate?> GetTemplateByIdAsync(Guid id, CancellationToken ct = default);
    Task<MeasurementTemplate> CreateTemplateAsync(MeasurementTemplate template, CancellationToken ct = default);
    Task UpdateTemplateAsync(MeasurementTemplate template, CancellationToken ct = default);
    Task DeleteTemplateAsync(Guid id, CancellationToken ct = default);

    // ── Profiles ─────────────────────────────────────────────────────────────
    Task<List<MeasurementProfileListViewModel>> GetCustomerProfilesAsync(Guid customerId, CancellationToken ct = default);
    Task<MeasurementProfileDetailViewModel?> GetProfileDetailAsync(Guid id, CancellationToken ct = default);
    Task<MeasurementProfile?> GetProfileByIdAsync(Guid id, CancellationToken ct = default);
    Task<MeasurementProfile> SaveProfileAsync(MeasurementProfile profile, CancellationToken ct = default);
    Task UpdateProfileAsync(MeasurementProfile profile, CancellationToken ct = default);
    Task DeleteProfileAsync(Guid id, CancellationToken ct = default);
}

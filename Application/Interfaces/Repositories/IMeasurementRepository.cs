using Application.ViewModels.Measurement;
using Domain.Entities;

namespace Application.Interfaces.Repositories;

public interface IMeasurementRepository
{
    // ── Fields ──────────────────────────────────────────────────────────────
    Task<List<MeasurementFieldViewModel>> GetFieldsAsync(CancellationToken ct = default);
    Task<MeasurementField?> GetFieldByIdAsync(Guid id, CancellationToken ct = default);
    Task<Dictionary<Guid, MeasurementField>> GetFieldsByIdAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<MeasurementField> CreateFieldAsync(MeasurementField field, CancellationToken ct = default);
    Task UpdateFieldAsync(MeasurementField field, CancellationToken ct = default);
    Task DeleteFieldAsync(Guid id, CancellationToken ct = default);
    Task<bool> FieldNameExistsAsync(string name, Guid? excludeId, CancellationToken ct = default);

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
    Task<List<MeasurementProfileVersionViewModel>> GetProfileVersionsAsync(Guid profileId, CancellationToken ct = default);
    Task<bool> HasVersionsAsync(Guid profileId, CancellationToken ct = default);
    void AddVersion(MeasurementProfileVersion version);
    Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken ct = default);
}

using Application.Common;
using Application.ViewModels.Measurement;

namespace Application.Interfaces.Services;

public interface IMeasurementService
{
    // Fields
    Task<ApiResponse<List<MeasurementFieldViewModel>>> GetFieldsAsync(CancellationToken ct = default);
    Task<ApiResponse<MeasurementFieldViewModel>> CreateFieldAsync(CreateMeasurementFieldViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<MeasurementFieldViewModel>> UpdateFieldAsync(Guid id, UpdateMeasurementFieldViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<object>> DeleteFieldAsync(Guid id, CancellationToken ct = default);

    // Templates
    Task<ApiResponse<List<TemplateListViewModel>>> GetTemplatesAsync(CancellationToken ct = default);
    Task<ApiResponse<TemplateDetailViewModel>> GetTemplateDetailAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<TemplateDetailViewModel>> CreateTemplateAsync(CreateTemplateViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<TemplateDetailViewModel>> UpdateTemplateAsync(Guid id, UpdateTemplateViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<object>> DeleteTemplateAsync(Guid id, CancellationToken ct = default);

    // Profiles
    Task<ApiResponse<List<MeasurementProfileListViewModel>>> GetCustomerProfilesAsync(Guid customerId, CancellationToken ct = default);
    Task<ApiResponse<MeasurementProfileDetailViewModel>> GetProfileDetailAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<MeasurementProfileDetailViewModel>> SaveProfileAsync(SaveMeasurementProfileViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<MeasurementProfileDetailViewModel>> UpdateProfileAsync(Guid id, SaveMeasurementProfileViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<object>> DeleteProfileAsync(Guid id, CancellationToken ct = default);
}

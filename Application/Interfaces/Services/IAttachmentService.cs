using Application.Common;
using Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace Application.Interfaces.Services;

public class AttachmentViewModel
{
    public Guid Id { get; set; }
    public AttachmentType AttachmentType { get; set; }
    public Guid EntityId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? Description { get; set; }
    /// <summary>Authorised download URL (requires the bearer token).</summary>
    public string Url { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
}

public sealed record AttachmentFile(Stream Content, string ContentType, string FileName);

/// <summary>
/// Files linked to an order, order item, expense or customer. Files are stored under a private
/// key and only ever served through an authorised, tenant-filtered endpoint.
/// </summary>
public interface IAttachmentService
{
    Task<ApiResponse<AttachmentViewModel>> UploadAsync(AttachmentType type, Guid entityId, IFormFile file, string? description, CancellationToken ct = default);
    Task<ApiResponse<List<AttachmentViewModel>>> ListAsync(AttachmentType type, Guid entityId, CancellationToken ct = default);
    Task<ApiResponse<object>> DeleteAsync(Guid attachmentId, CancellationToken ct = default);
    Task<AttachmentFile?> OpenAsync(Guid attachmentId, CancellationToken ct = default);
}

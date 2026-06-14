using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace Application.Interfaces.Services;

/// <summary>
/// Global attachment handling – upload, retrieve, and delete files linked to any entity.
/// Mirrors the pattern used in the pilot/global API.
/// </summary>
public interface IAttachmentService
{
    /// <summary>
    /// Uploads a file and creates an Attachment record.
    /// </summary>
    Task<Attachment> AddAsync(
        Guid entityId,
        AttachmentType type,
        IFormFile file,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all attachments for a given entity/type pair.
    /// </summary>
    Task<IReadOnlyList<Attachment>> GetByEntityAsync(
        Guid entityId,
        AttachmentType type,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an attachment (both DB row and stored file).
    /// </summary>
    Task DeleteAsync(
        Guid attachmentId,
        CancellationToken cancellationToken = default);
}

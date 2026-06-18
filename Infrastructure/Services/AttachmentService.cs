using Application.Interfaces.Services;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services;

public class AttachmentService : IAttachmentService
{
    public Task<Attachment> AddAsync(Guid entityId, AttachmentType type, IFormFile file, CancellationToken cancellationToken = default)
    {
        // Placeholder for real attachment logic (e.g. AWS S3, Azure Blob, Local File System)
        return Task.FromResult(new Attachment
        {
            Id = Guid.NewGuid(),
            EntityId = entityId,
            AttachmentType = type,
            ImageUrl = $"/uploads/{Guid.NewGuid()}_{file.FileName}",
            Description = file.FileName,
            CreatedOn = DateTime.UtcNow
        });
    }

    public Task DeleteAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        // Placeholder
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Attachment>> GetByEntityAsync(Guid entityId, AttachmentType type, CancellationToken cancellationToken = default)
    {
        // Placeholder
        return Task.FromResult<IReadOnlyList<Attachment>>(new List<Attachment>());
    }
}

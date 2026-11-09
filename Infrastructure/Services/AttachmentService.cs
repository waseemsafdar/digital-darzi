using Application.Common;
using Application.Interfaces.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;

/// <summary>Replaces the earlier placeholder, which returned a fake URL and stored nothing.</summary>
public class AttachmentService : IAttachmentService
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;
    private readonly FileStorageOptions _options;
    private readonly ICurrentUserService _currentUser;
    private readonly IPlanLimitService _limits;

    public AttachmentService(ApplicationDbContext db, IFileStorage storage, IOptions<FileStorageOptions> options, ICurrentUserService currentUser,
        IPlanLimitService limits)
    {
        _db = db;
        _storage = storage;
        _options = options.Value;
        _currentUser = currentUser;
        _limits = limits;
    }

    public async Task<ApiResponse<AttachmentViewModel>> UploadAsync(AttachmentType type, Guid entityId, IFormFile file, string? description,
        AttachmentKind kind = AttachmentKind.General, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(kind)) return ApiResponse<AttachmentViewModel>.Fail("Unknown photo kind.");
        if (type is AttachmentType.Order or AttachmentType.OrderItem
            && await _limits.CheckFeatureAsync(PlanFeature.Photos, ct) is { } planError)
            return ApiResponse<AttachmentViewModel>.PaymentRequired(planError);
        if (file.Length == 0) return ApiResponse<AttachmentViewModel>.Fail("The file is empty.");
        if (file.Length > _options.MaxFileBytes)
            return ApiResponse<AttachmentViewModel>.Fail($"The file is larger than {_options.MaxFileBytes / (1024 * 1024)} MB.");
        if (!_options.AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            return ApiResponse<AttachmentViewModel>.Fail($"Only these file types are accepted: {string.Join(", ", _options.AllowedContentTypes)}.");
        if (!await EntityExistsAsync(type, entityId, ct))
            return ApiResponse<AttachmentViewModel>.NotFound($"{type} not found.");

        var id = Guid.NewGuid();
        var extension = Path.GetExtension(file.FileName);
        if (extension.Length > 10) extension = string.Empty;
        var key = $"{_currentUser.TenantId:N}/{_currentUser.ShopId:N}/{type.ToString().ToLowerInvariant()}/{id:N}{extension}";

        await using (var stream = file.OpenReadStream())
            await _storage.SaveAsync(key, stream, ct);

        var attachment = new Attachment
        {
            Id             = id,
            EntityId       = entityId,
            AttachmentType = type,
            Kind           = kind,
            ImageUrl       = $"/api/attachments/{id}/file",
            Description    = description,
            FileName       = Path.GetFileName(file.FileName),
            ContentType    = file.ContentType,
            SizeBytes      = file.Length,
            StorageKey     = key
        };
        _db.Attachments.Add(attachment);
        if (type == AttachmentType.Order) _db.Entry(attachment).Property("OrderId").CurrentValue = entityId;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            await _storage.DeleteAsync(key, ct);   // don't leave an orphan file behind
            throw;
        }
        return ApiResponse<AttachmentViewModel>.Ok(ToViewModel(attachment), "File uploaded.");
    }

    public async Task<ApiResponse<List<AttachmentViewModel>>> ListAsync(AttachmentType type, Guid entityId, CancellationToken ct = default)
        => ApiResponse<List<AttachmentViewModel>>.Ok((await _db.Attachments.AsNoTracking()
                .Where(a => a.AttachmentType == type && a.EntityId == entityId)
                .OrderBy(a => a.CreatedOn)
                .ToListAsync(ct))
            .Select(ToViewModel).ToList());

    public async Task<ApiResponse<object>> DeleteAsync(Guid attachmentId, CancellationToken ct = default)
    {
        var attachment = await _db.Attachments.FirstOrDefaultAsync(a => a.Id == attachmentId, ct);
        if (attachment == null) return ApiResponse<object>.NotFound("Attachment not found.");
        attachment.IsDeleted = true;   // soft delete: the file stays for the audit trail
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(null, "Attachment deleted.");
    }

    public async Task<AttachmentFile?> OpenAsync(Guid attachmentId, CancellationToken ct = default)
    {
        var attachment = await _db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == attachmentId, ct);
        if (attachment?.StorageKey == null) return null;
        var stream = await _storage.OpenReadAsync(attachment.StorageKey, ct);
        return stream == null ? null
            : new AttachmentFile(stream, attachment.ContentType ?? "application/octet-stream", attachment.FileName ?? "file");
    }

    private Task<bool> EntityExistsAsync(AttachmentType type, Guid id, CancellationToken ct) => type switch
    {
        AttachmentType.Order     => _db.Orders.AnyAsync(o => o.Id == id, ct),
        AttachmentType.OrderItem => _db.OrderItems.AnyAsync(i => i.Id == id, ct),
        AttachmentType.Expense   => _db.ShopExpenses.AnyAsync(e => e.Id == id, ct),
        AttachmentType.Customer  => _db.Customers.AnyAsync(c => c.Id == id, ct),
        _                        => Task.FromResult(false)
    };

    private static AttachmentViewModel ToViewModel(Attachment a) => new()
    {
        Id = a.Id, AttachmentType = a.AttachmentType, Kind = a.Kind, EntityId = a.EntityId ?? Guid.Empty,
        FileName = a.FileName ?? string.Empty, ContentType = a.ContentType ?? string.Empty, SizeBytes = a.SizeBytes,
        Description = a.Description, Url = $"/api/attachments/{a.Id}/file", CreatedOn = a.CreatedOn
    };
}

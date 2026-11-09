using Domain.Enums;

namespace Domain.Entities;

public class Attachment : BaseDBModel
{
    public Guid? EntityId { get; set; }
    public AttachmentType AttachmentType { get; set; } = AttachmentType.Order;
    public AttachmentKind Kind { get; set; } = AttachmentKind.General;
    public string ImageUrl { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public long SizeBytes { get; set; }

    /// <summary>Storage key (relative path) — never exposed to clients; files are streamed through an authorised endpoint.</summary>
    public string? StorageKey { get; set; }
}

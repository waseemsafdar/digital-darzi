using Domain.Enums;

namespace Domain.Entities;

public class Attachment : BaseDBModel
{
    public Guid? EntityId { get; set; }
    public AttachmentType AttachmentType { get; set; } = AttachmentType.Order;
    public string ImageUrl { get; set; } = string.Empty;
    public string? Description { get; set; }
}

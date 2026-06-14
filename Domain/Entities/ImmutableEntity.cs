using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

/// <summary>
/// Append-only entity — never updated after creation. Used for audit trails and snapshots.
/// </summary>
public abstract class ImmutableEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
}

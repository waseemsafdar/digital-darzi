using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

/// <summary>
/// Lightweight base for junction/link tables that don't need full audit.
/// </summary>
public abstract class LightEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
}

using Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

public abstract class BaseDBModel
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid ClientId { get; set; }
    public long Sr { get; set; }
    public bool IsDeleted { get; set; } = false;
    public ActiveStatus ActiveStatus { get; set; } = ActiveStatus.Active;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public Guid UpdatedBy { get; set; }
    public DateTime UpdatedOn { get; set; } = DateTime.UtcNow;
}

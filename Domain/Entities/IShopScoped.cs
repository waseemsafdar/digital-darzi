namespace Domain.Entities;

/// <summary>
/// Entities that carry the legacy <c>ShopId</c> column alongside <c>BranchId</c>.
/// Both hold the same value (the branch the row belongs to). The DbContext stamps
/// <c>ShopId</c> from the current branch when a new row leaves it empty.
/// </summary>
public interface IShopScoped
{
    Guid ShopId { get; set; }
}

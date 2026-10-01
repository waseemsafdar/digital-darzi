using Application.Common;
using Application.Interfaces.Services;
using Application.ViewModels.Operations;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public interface IInventoryService
{
    Task<ApiResponse<List<SupplierViewModel>>> GetSuppliersAsync(CancellationToken ct = default);
    Task<ApiResponse<SupplierViewModel>> SaveSupplierAsync(Guid? id, SaveSupplierViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<object>> DeleteSupplierAsync(Guid id, CancellationToken ct = default);

    Task<ApiResponse<PagedResult<InventoryItemViewModel>>> SearchItemsAsync(InventorySearchViewModel filter, CancellationToken ct = default);
    Task<ApiResponse<InventoryItemViewModel>> GetItemAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<InventoryItemViewModel>> SaveItemAsync(Guid? id, SaveInventoryItemViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<object>> DeleteItemAsync(Guid id, CancellationToken ct = default);

    Task<ApiResponse<PagedResult<InventoryMovementViewModel>>> GetMovementsAsync(Guid? itemId, Guid? orderId, int page, int pageSize, CancellationToken ct = default);
    Task<ApiResponse<InventoryItemViewModel>> RecordPurchaseAsync(Guid itemId, RecordPurchaseViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<InventoryItemViewModel>> IssueToOrderAsync(Guid itemId, IssueToOrderViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<InventoryItemViewModel>> ReturnFromOrderAsync(Guid itemId, ReturnFromOrderViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<InventoryItemViewModel>> AdjustAsync(Guid itemId, StockAdjustmentViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<InventoryItemViewModel>> TransferAsync(Guid itemId, StockTransferViewModel vm, CancellationToken ct = default);
}

/// <summary>
/// Stock kept as an append-only movement ledger. Every movement updates the item's cached
/// quantity in the same SaveChanges; the item row carries an xmin token so two simultaneous
/// issues cannot both pass the stock check.
/// </summary>
public sealed class InventoryService : IInventoryService
{
    private const int MaxAttempts = 3;
    private readonly ApplicationDbContext _db;
    private readonly IBusinessCalendar _calendar;
    private readonly ICurrentUserService _currentUser;

    public InventoryService(ApplicationDbContext db, IBusinessCalendar calendar, ICurrentUserService currentUser)
    {
        _db = db;
        _calendar = calendar;
        _currentUser = currentUser;
    }

    // ── Suppliers ────────────────────────────────────────────────────────────

    public async Task<ApiResponse<List<SupplierViewModel>>> GetSuppliersAsync(CancellationToken ct = default)
        => ApiResponse<List<SupplierViewModel>>.Ok(await _db.Suppliers.AsNoTracking().OrderBy(s => s.Name)
            .Select(s => new SupplierViewModel { Id = s.Id, Name = s.Name, Phone = s.Phone, Address = s.Address, Notes = s.Notes })
            .ToListAsync(ct));

    public async Task<ApiResponse<SupplierViewModel>> SaveSupplierAsync(Guid? id, SaveSupplierViewModel vm, CancellationToken ct = default)
    {
        Supplier? row;
        if (id.HasValue)
        {
            row = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct);
            if (row == null) return ApiResponse<SupplierViewModel>.NotFound("Supplier not found.");
        }
        else
        {
            row = new Supplier();
            _db.Suppliers.Add(row);
        }
        row.Name = vm.Name.Trim();
        row.Phone = vm.Phone;
        row.Address = vm.Address;
        row.Notes = vm.Notes;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<SupplierViewModel>.Ok(new SupplierViewModel { Id = row.Id, Name = row.Name, Phone = row.Phone, Address = row.Address, Notes = row.Notes });
    }

    public async Task<ApiResponse<object>> DeleteSupplierAsync(Guid id, CancellationToken ct = default)
    {
        var row = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (row == null) return ApiResponse<object>.NotFound("Supplier not found.");
        row.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(null, "Supplier deleted.");
    }

    // ── Items ────────────────────────────────────────────────────────────────

    public async Task<ApiResponse<PagedResult<InventoryItemViewModel>>> SearchItemsAsync(InventorySearchViewModel filter, CancellationToken ct = default)
    {
        var (page, pageSize) = Paging.Normalize(filter.Page, filter.PageSize);
        var q = _db.InventoryItems.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var pattern = Like.Contains(filter.Query);
            q = q.Where(i => EF.Functions.ILike(i.Name, pattern, Like.EscapeChar)
                || (i.Sku != null && EF.Functions.ILike(i.Sku, pattern, Like.EscapeChar))
                || (i.Color != null && EF.Functions.ILike(i.Color, pattern, Like.EscapeChar)));
        }
        if (filter.Category.HasValue) q = q.Where(i => i.Category == filter.Category);
        if (filter.LowStockOnly == true) q = q.Where(i => i.QuantityOnHand <= i.ReorderLevel);

        var total = await q.CountAsync(ct);
        var rows = await q.OrderBy(i => i.Name).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(i => new { Item = i, SupplierName = i.Supplier != null ? i.Supplier.Name : null })
            .ToListAsync(ct);
        return ApiResponse<PagedResult<InventoryItemViewModel>>.Ok(PagedResult<InventoryItemViewModel>.From(
            rows.Select(r => ToViewModel(r.Item, r.SupplierName)).ToList(), total, page, pageSize));
    }

    public async Task<ApiResponse<InventoryItemViewModel>> GetItemAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _db.InventoryItems.AsNoTracking().Include(i => i.Supplier).FirstOrDefaultAsync(i => i.Id == id, ct);
        return item == null
            ? ApiResponse<InventoryItemViewModel>.NotFound("Inventory item not found.")
            : ApiResponse<InventoryItemViewModel>.Ok(ToViewModel(item, item.Supplier?.Name));
    }

    public async Task<ApiResponse<InventoryItemViewModel>> SaveItemAsync(Guid? id, SaveInventoryItemViewModel vm, CancellationToken ct = default)
    {
        if (vm.SupplierId.HasValue && !await _db.Suppliers.AnyAsync(s => s.Id == vm.SupplierId, ct))
            return ApiResponse<InventoryItemViewModel>.NotFound("Supplier not found.");
        var sku = string.IsNullOrWhiteSpace(vm.Sku) ? null : vm.Sku.Trim();
        if (sku != null && await _db.InventoryItems.AnyAsync(i => i.Sku == sku && i.Id != id, ct))
            return ApiResponse<InventoryItemViewModel>.Conflict($"SKU '{sku}' is already used by another item.");

        InventoryItem? item;
        if (id.HasValue)
        {
            item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == id, ct);
            if (item == null) return ApiResponse<InventoryItemViewModel>.NotFound("Inventory item not found.");
        }
        else
        {
            item = new InventoryItem();
            _db.InventoryItems.Add(item);
        }

        item.Name = vm.Name.Trim();
        item.Sku = sku;
        item.Category = vm.Category;
        item.Unit = vm.Unit;
        item.Color = vm.Color;
        item.Design = vm.Design;
        item.Width = vm.Width;
        item.Location = vm.Location;
        item.SupplierId = vm.SupplierId;
        item.PurchasePrice = vm.PurchasePrice;
        item.SellingPrice = vm.SellingPrice;
        item.ReorderLevel = vm.ReorderLevel;   // quantity is only ever changed by movements

        await _db.SaveChangesAsync(ct);
        return await GetItemAsync(item.Id, ct);
    }

    public async Task<ApiResponse<object>> DeleteItemAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (item == null) return ApiResponse<object>.NotFound("Inventory item not found.");
        if (item.QuantityOnHand != 0)
            return ApiResponse<object>.Fail("The item still has stock. Adjust it to zero (with a reason) before deleting.");
        item.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(null, "Item deleted.");
    }

    // ── Movements ────────────────────────────────────────────────────────────

    public async Task<ApiResponse<PagedResult<InventoryMovementViewModel>>> GetMovementsAsync(Guid? itemId, Guid? orderId, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize, 50);
        var q = _db.InventoryMovements.AsNoTracking();
        if (itemId.HasValue) q = q.Where(m => m.ItemId == itemId);
        if (orderId.HasValue) q = q.Where(m => m.OrderId == orderId);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(m => m.MovementDate).ThenByDescending(m => m.CreatedOn)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(m => new InventoryMovementViewModel
            {
                Id = m.Id, ItemId = m.ItemId, ItemName = m.Item.Name, MovementType = m.MovementType, Quantity = m.Quantity,
                UnitCost = m.UnitCost, MovementDate = m.MovementDate, OrderId = m.OrderId, OrderItemId = m.OrderItemId,
                SupplierId = m.SupplierId, CounterpartBranchId = m.CounterpartBranchId, Reference = m.Reference,
                Notes = m.Notes, CreatedBy = m.CreatedBy
            })
            .ToListAsync(ct);
        return ApiResponse<PagedResult<InventoryMovementViewModel>>.Ok(PagedResult<InventoryMovementViewModel>.From(items, total, page, pageSize));
    }

    public Task<ApiResponse<InventoryItemViewModel>> RecordPurchaseAsync(Guid itemId, RecordPurchaseViewModel vm, CancellationToken ct = default)
        => WithRetryAsync(async () =>
        {
            if (vm.Quantity <= 0) return ApiResponse<InventoryItemViewModel>.Fail("Quantity must be greater than zero.");
            if (vm.UnitCost < 0) return ApiResponse<InventoryItemViewModel>.Fail("Unit cost cannot be negative.");
            var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == itemId, ct);
            if (item == null) return ApiResponse<InventoryItemViewModel>.NotFound("Inventory item not found.");
            var supplierId = vm.SupplierId ?? item.SupplierId;
            if (vm.SupplierId.HasValue && !await _db.Suppliers.AnyAsync(s => s.Id == vm.SupplierId, ct))
                return ApiResponse<InventoryItemViewModel>.NotFound("Supplier not found.");

            Guid? expenseId = null;
            if (vm.RecordAsExpense && vm.UnitCost > 0)
            {
                var expense = new ShopExpense
                {
                    Category      = await SuppliesCategoryAsync(ct),
                    Description   = $"Stock purchase: {vm.Quantity:0.###} {item.Unit} {item.Name}",
                    Amount        = Math.Round(vm.Quantity * vm.UnitCost, 2),
                    ExpenseDate   = await _calendar.TodayAsync(ct),
                    PaymentMethod = vm.PaymentMethod,
                    ReceiptRef    = vm.Reference,
                    AddedByUserId = _currentUser.UserId
                };
                _db.ShopExpenses.Add(expense);
                expenseId = expense.Id;
            }

            AddMovement(item, InventoryMovementType.Purchase, vm.Quantity, vm.UnitCost, m =>
            {
                m.SupplierId = supplierId;
                m.Reference = vm.Reference;
                m.Notes = vm.Notes;
                m.ExpenseId = expenseId;
            });
            item.PurchasePrice = vm.UnitCost;
            await _db.SaveChangesAsync(ct);
            return await GetItemAsync(item.Id, ct);
        }, ct);

    public Task<ApiResponse<InventoryItemViewModel>> IssueToOrderAsync(Guid itemId, IssueToOrderViewModel vm, CancellationToken ct = default)
        => WithRetryAsync(async () =>
        {
            if (vm.Quantity <= 0) return ApiResponse<InventoryItemViewModel>.Fail("Quantity must be greater than zero.");
            var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == itemId, ct);
            if (item == null) return ApiResponse<InventoryItemViewModel>.NotFound("Inventory item not found.");
            var orderItem = await _db.OrderItems.AsNoTracking()
                .Where(i => i.Id == vm.OrderItemId).Select(i => new { i.Id, i.OrderId, OrderStatus = i.Order.Status }).FirstOrDefaultAsync(ct);
            if (orderItem == null) return ApiResponse<InventoryItemViewModel>.NotFound("Order item not found.");
            if (orderItem.OrderStatus == OrderStatus.Cancelled) return ApiResponse<InventoryItemViewModel>.Fail("The order is cancelled.");
            if (item.QuantityOnHand < vm.Quantity)
                return ApiResponse<InventoryItemViewModel>.Fail($"Only {item.QuantityOnHand:0.###} {item.Unit} of {item.Name} in stock.");

            AddMovement(item, InventoryMovementType.IssueToOrder, -vm.Quantity, item.PurchasePrice, m =>
            {
                m.OrderId = orderItem.OrderId;
                m.OrderItemId = orderItem.Id;
                m.Notes = vm.Notes;
            });
            await _db.SaveChangesAsync(ct);
            return await GetItemAsync(item.Id, ct);
        }, ct);

    public Task<ApiResponse<InventoryItemViewModel>> ReturnFromOrderAsync(Guid itemId, ReturnFromOrderViewModel vm, CancellationToken ct = default)
        => WithRetryAsync(async () =>
        {
            if (vm.Quantity <= 0) return ApiResponse<InventoryItemViewModel>.Fail("Quantity must be greater than zero.");
            var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == itemId, ct);
            if (item == null) return ApiResponse<InventoryItemViewModel>.NotFound("Inventory item not found.");

            var netIssued = -await _db.InventoryMovements
                .Where(m => m.ItemId == itemId && m.OrderItemId == vm.OrderItemId
                    && (m.MovementType == InventoryMovementType.IssueToOrder || m.MovementType == InventoryMovementType.ReturnFromOrder))
                .SumAsync(m => (decimal?)m.Quantity, ct) ?? 0;
            if (vm.Quantity > netIssued)
                return ApiResponse<InventoryItemViewModel>.Fail($"Only {netIssued:0.###} {item.Unit} was issued to this order item and not yet returned.");

            var orderId = await _db.OrderItems.Where(i => i.Id == vm.OrderItemId).Select(i => (Guid?)i.OrderId).FirstOrDefaultAsync(ct);
            AddMovement(item, InventoryMovementType.ReturnFromOrder, vm.Quantity, item.PurchasePrice, m =>
            {
                m.OrderId = orderId;
                m.OrderItemId = vm.OrderItemId;
                m.Notes = vm.Notes;
            });
            await _db.SaveChangesAsync(ct);
            return await GetItemAsync(item.Id, ct);
        }, ct);

    public Task<ApiResponse<InventoryItemViewModel>> AdjustAsync(Guid itemId, StockAdjustmentViewModel vm, CancellationToken ct = default)
        => WithRetryAsync(async () =>
        {
            if (vm.MovementType is not (InventoryMovementType.Adjustment or InventoryMovementType.Waste or InventoryMovementType.SupplierReturn))
                return ApiResponse<InventoryItemViewModel>.Fail("Use Adjustment, Waste or SupplierReturn here; purchases, issues and transfers have their own endpoints.");
            if (vm.Quantity == 0) return ApiResponse<InventoryItemViewModel>.Fail("Quantity cannot be zero.");
            if (string.IsNullOrWhiteSpace(vm.Notes)) return ApiResponse<InventoryItemViewModel>.Fail("A reason is required for stock adjustments.");
            var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == itemId, ct);
            if (item == null) return ApiResponse<InventoryItemViewModel>.NotFound("Inventory item not found.");

            var signed = vm.MovementType == InventoryMovementType.Adjustment ? vm.Quantity : -Math.Abs(vm.Quantity);
            if (item.QuantityOnHand + signed < 0)
                return ApiResponse<InventoryItemViewModel>.Fail($"Stock cannot go below zero (on hand: {item.QuantityOnHand:0.###}).");

            AddMovement(item, vm.MovementType, signed, item.PurchasePrice, m => m.Notes = vm.Notes);
            await _db.SaveChangesAsync(ct);
            return await GetItemAsync(item.Id, ct);
        }, ct);

    public Task<ApiResponse<InventoryItemViewModel>> TransferAsync(Guid itemId, StockTransferViewModel vm, CancellationToken ct = default)
        => WithRetryAsync(async () =>
        {
            if (vm.Quantity <= 0) return ApiResponse<InventoryItemViewModel>.Fail("Quantity must be greater than zero.");
            if (vm.TargetShopId == _currentUser.ShopId) return ApiResponse<InventoryItemViewModel>.Fail("Choose a different branch.");
            if (!await _db.Shops.AnyAsync(s => s.Id == vm.TargetShopId, ct))   // tenant-filtered
                return ApiResponse<InventoryItemViewModel>.NotFound("Target branch not found.");

            var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == itemId, ct);
            if (item == null) return ApiResponse<InventoryItemViewModel>.NotFound("Inventory item not found.");
            if (item.QuantityOnHand < vm.Quantity)
                return ApiResponse<InventoryItemViewModel>.Fail($"Only {item.QuantityOnHand:0.###} {item.Unit} in stock.");

            // Match the same item in the target branch (by SKU, else by name/category/colour) or create it.
            var target = await _db.TenantWide<InventoryItem>()
                .Where(i => i.BranchId == vm.TargetShopId
                    && (item.Sku != null ? i.Sku == item.Sku : i.Name == item.Name && i.Category == item.Category && i.Color == item.Color))
                .FirstOrDefaultAsync(ct);
            if (target == null)
            {
                target = new InventoryItem
                {
                    TenantId = item.TenantId, BranchId = vm.TargetShopId, Name = item.Name, Sku = item.Sku, Category = item.Category,
                    Unit = item.Unit, Color = item.Color, Design = item.Design, Width = item.Width, PurchasePrice = item.PurchasePrice,
                    SellingPrice = item.SellingPrice, ReorderLevel = item.ReorderLevel
                };
                _db.InventoryItems.Add(target);
            }

            AddMovement(item, InventoryMovementType.TransferOut, -vm.Quantity, item.PurchasePrice, m =>
            {
                m.CounterpartBranchId = vm.TargetShopId;
                m.Notes = vm.Notes;
            });
            AddMovement(target, InventoryMovementType.TransferIn, vm.Quantity, item.PurchasePrice, m =>
            {
                m.TenantId = item.TenantId;
                m.BranchId = vm.TargetShopId;
                m.CounterpartBranchId = _currentUser.ShopId;
                m.Notes = vm.Notes;
            });
            await _db.SaveChangesAsync(ct);
            return await GetItemAsync(item.Id, ct);
        }, ct);

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void AddMovement(InventoryItem item, InventoryMovementType type, decimal signedQty, decimal unitCost, Action<InventoryMovement> configure)
    {
        var movement = new InventoryMovement
        {
            ItemId       = item.Id,
            MovementType = type,
            Quantity     = signedQty,
            UnitCost     = unitCost,
            MovementDate = _calendar.UtcNow
        };
        configure(movement);
        _db.InventoryMovements.Add(movement);
        item.QuantityOnHand += signedQty;
    }

    private async Task<string> SuppliesCategoryAsync(CancellationToken ct)
        => await _db.ExpenseCategories.AsNoTracking()
               .Where(c => c.Name == "Supplies").Select(c => c.Name).FirstOrDefaultAsync(ct)
           ?? "Supplies";

    private async Task<ApiResponse<InventoryItemViewModel>> WithRetryAsync(Func<Task<ApiResponse<InventoryItemViewModel>>> operation, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try { return await operation(); }
            catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts) { _db.ChangeTracker.Clear(); }
        }
    }

    private static InventoryItemViewModel ToViewModel(InventoryItem i, string? supplierName) => new()
    {
        Id = i.Id, Name = i.Name, Sku = i.Sku, Category = i.Category, Unit = i.Unit, Color = i.Color, Design = i.Design,
        Width = i.Width, Location = i.Location, SupplierId = i.SupplierId, SupplierName = supplierName,
        PurchasePrice = i.PurchasePrice, SellingPrice = i.SellingPrice, ReorderLevel = i.ReorderLevel,
        QuantityOnHand = i.QuantityOnHand, StockValue = Math.Round(i.QuantityOnHand * i.PurchasePrice, 2),
        IsLowStock = i.QuantityOnHand <= i.ReorderLevel
    };
}

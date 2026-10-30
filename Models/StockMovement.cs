
namespace InventorySystem.Backend.Models;

/// <summary>
/// Stock ledger entry. Current stock is calculated from posted movements,
/// not edited directly on Product.
/// </summary>
public sealed class StockMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public BusinessTenant Tenant { get; set; } = null!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public decimal QuantityChange { get; set; }

    public string Type { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string? Note { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Guid? CreatedByUserId { get; set; }
}

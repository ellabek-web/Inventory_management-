namespace InventorySystem.Backend.Models;
public sealed class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public BusinessTenant Tenant { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string UnitOfMeasure { get; set; } = "piece";
    public decimal UnitCost { get; set; }
    public decimal SellingPrice { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
}


namespace InventorySystem.Backend.Models;

public sealed class Sale
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public string Reference { get; set; } = string.Empty;

    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    public string Status { get; set; } = "Draft";

    public decimal TotalAmount { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
        = DateTimeOffset.UtcNow;

    public Guid? CreatedByUserId { get; set; }

    public ICollection<SaleItem> Items { get; set; }
        = new List<SaleItem>();

    public ICollection<SalePayment> Payments { get; set; }
        = new List<SalePayment>();
}

public sealed class SaleItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SaleId { get; set; }

    public Sale Sale { get; set; } = null!;

    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }
}

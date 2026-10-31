namespace InventorySystem.Backend.DTOs;

public sealed class CreateSaleRequest
{
    public Guid WarehouseId { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    public List<CreateSaleItemRequest> Items { get; set; } = new();
}

public sealed class CreateSaleItemRequest
{
    public Guid ProductId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }
}
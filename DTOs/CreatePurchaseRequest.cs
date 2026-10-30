
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Backend.DTOs;

public sealed class CreatePurchaseRequest
{
    [Required]
    [StringLength(100)]
    public string Reference { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string SupplierName { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public List<CreatePurchaseItemRequest> Items { get; set; } = [];
}

public sealed class CreatePurchaseItemRequest
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(typeof(decimal), "0.001", "999999999999")]
    public decimal Quantity { get; set; }

    [Range(typeof(decimal), "0", "999999999999")]
    public decimal UnitCost { get; set; }
}

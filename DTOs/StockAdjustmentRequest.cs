
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Backend.DTOs;

public sealed class StockAdjustmentRequest
{
    [Required]
    public Guid ProductId { get; set; }

    [Required]
    public Guid WarehouseId { get; set; }

    [Required]
    [RegularExpression("Increase|Decrease",
        ErrorMessage = "Direction must be Increase or Decrease.")]
    public string Direction { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.001", "999999999999")]
    public decimal Quantity { get; set; }

    [StringLength(100)]
    public string? Reference { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}

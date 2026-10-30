
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Backend.DTOs;

public sealed class CreateProductRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Sku { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Barcode { get; set; }

    [Required]
    [StringLength(50)]
    public string UnitOfMeasure { get; set; } = "piece";

    [Range(typeof(decimal), "0", "999999999999")]
    public decimal UnitCost { get; set; }

    [Range(typeof(decimal), "0", "999999999999")]
    public decimal SellingPrice { get; set; }
}


using System.ComponentModel.DataAnnotations;
using InventorySystem.Backend.Validation;

namespace InventorySystem.Backend.DTOs;

public sealed class UpdateProductRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(80, MinimumLength = 1)]
    public string Sku { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Barcode { get; set; }

    [Required]
    [StringLength(40, MinimumLength = 1)]
    public string UnitOfMeasure { get; set; } = "piece";

    [Range(typeof(decimal), "0", "999999999999")]
    [DecimalPrecision(2)]
    public decimal UnitCost { get; set; }

    [Range(typeof(decimal), "0", "999999999999")]
    [DecimalPrecision(2)]
    public decimal SellingPrice { get; set; }
}

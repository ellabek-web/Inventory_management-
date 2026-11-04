
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Backend.DTOs;

public sealed class RecordSalePaymentRequest
{
    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(40)]
    public string PaymentMethod { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Reference { get; set; }
}

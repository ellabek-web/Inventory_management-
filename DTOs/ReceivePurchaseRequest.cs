
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Backend.DTOs;

public sealed class ReceivePurchaseRequest
{
    [Required]
    public Guid WarehouseId { get; set; }
}


namespace InventorySystem.Backend.Models;

public sealed class SalePayment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SaleId { get; set; }

    public Sale Sale { get; set; } = null!;

    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = string.Empty;

    public string? Reference { get; set; }

    public DateTimeOffset ReceivedAtUtc { get; set; }
        = DateTimeOffset.UtcNow;

    public Guid? ReceivedByUserId { get; set; }
}

namespace InventorySystem.Backend.Models;
public sealed class BusinessTenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BusinessName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<AppUser> Users { get; set; } = new List<AppUser>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<Warehouse> Warehouses { get; set; } = new List<Warehouse>();
}

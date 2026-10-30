namespace InventorySystem.Backend.Models;
public sealed class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public BusinessTenant Tenant { get; set; } = null!;
    public string Email { get; set; } = string.Empty;
    // Store a proven password hash only, never a plain-text password.
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Staff";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

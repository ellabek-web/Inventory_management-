
using System.Security.Claims;
using InventorySystem.Backend.Data;
using InventorySystem.Backend.DTOs;
using InventorySystem.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Backend.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly InventoryDbContext _db;
    private readonly IPasswordHasher<AppUser> _passwordHasher;

    public UsersController(
        InventoryDbContext db,
        IPasswordHasher<AppUser> passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    [HttpPost("staff")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateStaff(
        [FromBody] CreateStaffRequest request,
        CancellationToken cancellationToken)
    {
        var tenantValue = User.FindFirstValue("tenant_id");

        if (!Guid.TryParse(tenantValue, out var tenantId))
        {
            return Unauthorized(new
            {
                message = "The authenticated user's tenant is invalid."
            });
        }

        var email = request.Email.Trim().ToUpperInvariant();

        var emailExists = await _db.AppUsers.AnyAsync(
            u => u.TenantId == tenantId && u.Email == email,
            cancellationToken);

        if (emailExists)
        {
            return Conflict(new
            {
                message = "A user with this email already exists in your business."
            });
        }

        var staff = new AppUser
        {
            TenantId = tenantId,
            Email = email,
            Role = "Staff",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        staff.PasswordHash = _passwordHasher.HashPassword(
            staff,
            request.Password);

        _db.AppUsers.Add(staff);

        await _db.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, new
        {
            message = "Staff account created successfully.",
            userId = staff.Id,
            tenantId = staff.TenantId,
            email = staff.Email,
            role = staff.Role,
            isActive = staff.IsActive
        });
    }
}

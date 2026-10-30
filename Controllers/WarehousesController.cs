
using InventorySystem.Backend.Data;
using InventorySystem.Backend.DTOs;
using InventorySystem.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Backend.Controllers;

[ApiController]
[Route("api/warehouses")]
[Authorize]
public sealed class WarehousesController : ControllerBase
{
    private readonly InventoryDbContext _db;

    public WarehousesController(InventoryDbContext db)
    {
        _db = db;
    }

    private bool TryGetTenantId(out Guid tenantId)
    {
        tenantId = Guid.Empty;

        var claim = User.FindFirst("tenant_id");

        return claim is not null &&
               Guid.TryParse(claim.Value, out tenantId);
    }

    // GET: /api/warehouses
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (!TryGetTenantId(out var tenantId))
            return Unauthorized();

        var warehouses = await _db.Warehouses
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId)
            .OrderBy(w => w.Name)
            .Select(w => new
            {
                w.Id,
                w.Name,
                w.Code,
                w.LocationDescription,
                w.IsActive
            })
            .ToListAsync();

        return Ok(warehouses);
    }

    // POST: /api/warehouses
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateWarehouseRequest request)
    {
        if (!TryGetTenantId(out var tenantId))
            return Unauthorized();

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var normalizedName = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(normalizedName) ||
            string.IsNullOrWhiteSpace(normalizedCode))
        {
            return BadRequest(new
            {
                message = "Warehouse name and code are required."
            });
        }

        var codeExists = await _db.Warehouses.AnyAsync(
            w => w.TenantId == tenantId &&
                 w.Code == normalizedCode);

        if (codeExists)
        {
            return Conflict(new
            {
                message = "A warehouse with this code already exists in your business."
            });
        }

        var tenantExists = await _db.BusinessTenants
            .AnyAsync(t => t.Id == tenantId);

        if (!tenantExists)
            return Unauthorized();

        var warehouse = new Warehouse
        {
            TenantId = tenantId,
            Name = normalizedName,
            Code = normalizedCode,
            LocationDescription =
                string.IsNullOrWhiteSpace(request.LocationDescription)
                    ? null
                    : request.LocationDescription.Trim(),
            IsActive = true
        };

        _db.Warehouses.Add(warehouse);
        await _db.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetAll),
            new { id = warehouse.Id },
            new
            {
                warehouse.Id,
                warehouse.Name,
                warehouse.Code,
                warehouse.LocationDescription,
                warehouse.IsActive
            });
    }
}

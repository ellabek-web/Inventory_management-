
using System.Security.Claims;
using InventorySystem.Backend.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Backend.Controllers;

[ApiController]
[Route("api/stock-movements")]
[Authorize]
public sealed class StockMovementsController : ControllerBase
{
    private readonly InventoryDbContext _db;

    public StockMovementsController(InventoryDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> GetStockMovements(
        [FromQuery] Guid? productId,
        [FromQuery] Guid? warehouseId,
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        // Read the tenant from the authenticated JWT.
        var tenantClaim = User.FindFirst("tenant_id")?.Value;

        if (!Guid.TryParse(tenantClaim, out var tenantId))
        {
            return Unauthorized(new
            {
                message = "The authenticated user has no valid tenant claim."
            });
        }

        // Validate pagination.
        if (page < 1)
        {
            return BadRequest(new { message = "Page must be at least 1." });
        }

        if (pageSize < 1 || pageSize > 100)
        {
            return BadRequest(new
            {
                message = "Page size must be between 1 and 100."
            });
        }

        // Validate the optional date range.
        if (fromUtc.HasValue &&
            toUtc.HasValue &&
            fromUtc.Value > toUtc.Value)
        {
            return BadRequest(new
            {
                message = "fromUtc must be earlier than or equal to toUtc."
            });
        }

        // Tenant filtering is mandatory; never accept tenantId
        // from the query string.
        var query = _db.StockMovements
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId);

        if (productId.HasValue)
        {
            query = query.Where(m => m.ProductId == productId.Value);
        }

        if (warehouseId.HasValue)
        {
            query = query.Where(m => m.WarehouseId == warehouseId.Value);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(m => m.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(m => m.CreatedAtUtc <= toUtc.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(
            totalCount / (double)pageSize);

        var movements = await query
            .OrderByDescending(m => m.CreatedAtUtc)
            .ThenByDescending(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new
            {
                m.Id,
                m.ProductId,
                ProductName = m.Product.Name,
                m.WarehouseId,
                WarehouseName = m.Warehouse.Name,
                m.QuantityChange,
                m.Type,
                m.Reference,
                m.Note,
                m.CreatedByUserId,
                m.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            page,
            pageSize,
            totalCount,
            totalPages,
            movements
        });
    }
}


using System.Security.Claims;
using InventorySystem.Backend.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Backend.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public sealed class ReportsController : ControllerBase
{
    private readonly InventoryDbContext _db;

    public ReportsController(InventoryDbContext db)
    {
        _db = db;
    }

    // GET: api/reports/stock-balance
    [HttpGet("stock-balance")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> GetStockBalanceReport(
        [FromQuery] Guid? productId,
        [FromQuery] Guid? warehouseId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate pagination
        if (page < 1)
        {
            return BadRequest(new
            {
                message = "Page must be at least 1."
            });
        }

        if (pageSize < 1 || pageSize > 100)
        {
            return BadRequest(new
            {
                message = "Page size must be between 1 and 100."
            });
        }

        // 2. Validate tenant
        var tenantValue = User.FindFirstValue("tenant_id");

        if (!Guid.TryParse(tenantValue, out var tenantId))
        {
            return Unauthorized(new
            {
                message = "The authenticated user's tenant is invalid."
            });
        }

        // 3. Get active products belonging to this tenant
        var products = _db.Products
            .AsNoTracking()
            .Where(p =>
                p.TenantId == tenantId &&
                p.IsActive);

        if (productId.HasValue)
        {
            products = products.Where(
                p => p.Id == productId.Value);
        }

        // 4. Apply product search
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();

            products = products.Where(p =>
                p.Name.Contains(searchTerm) ||
                p.Sku.Contains(searchTerm) ||
                (p.Barcode != null &&
                 p.Barcode.Contains(searchTerm)));
        }

        // 5. Get active warehouses belonging to this tenant
        var warehouses = _db.Warehouses
            .AsNoTracking()
            .Where(w =>
                w.TenantId == tenantId &&
                w.IsActive);

        if (warehouseId.HasValue)
        {
            warehouses = warehouses.Where(
                w => w.Id == warehouseId.Value);
        }

        // 6. Get stock movements for this tenant
        var movements = _db.StockMovements
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId);

        // 7. Calculate stock balance for every
        // active product and active warehouse combination.
        var balances =
            from product in products
            from warehouse in warehouses
            join movement in movements
                on new
                {
                    ProductId = product.Id,
                    WarehouseId = warehouse.Id
                }
                equals new
                {
                    movement.ProductId,
                    movement.WarehouseId
                }
                into movementGroup
            select new
            {
                ProductId = product.Id,
                ProductName = product.Name,
                SKU = product.Sku,
                Barcode = product.Barcode,
                UnitOfMeasure = product.UnitOfMeasure,
                UnitCost = product.UnitCost,
                WarehouseId = warehouse.Id,
                WarehouseName = warehouse.Name,
                WarehouseCode = warehouse.Code,
                QuantityOnHand =
                    movementGroup.Sum(
                        m => (decimal?)m.QuantityChange) ?? 0m
            };

        // 8. Count records before pagination
        var totalCount = await balances.CountAsync(
            cancellationToken);

        // 9. Apply stable ordering and pagination
        var items = await balances
            .OrderBy(b => b.ProductName)
            .ThenBy(b => b.WarehouseName)
            .ThenBy(b => b.ProductId)
            .ThenBy(b => b.WarehouseId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // 10. Return report
        return Ok(new
        {
            page,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling(
                totalCount / (double)pageSize),
            items
        });
    }
}

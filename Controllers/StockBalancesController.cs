
using System.Security.Claims;
using InventorySystem.Backend.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Backend.Controllers;

[ApiController]
[Route("api/stock-balances")]
[Authorize]
public sealed class StockBalancesController : ControllerBase
{
    private readonly InventoryDbContext _db;

    public StockBalancesController(InventoryDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> GetStockBalances(
        [FromQuery] Guid? productId,
        [FromQuery] Guid? warehouseId,
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

        // Start with active products belonging to this tenant.
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

        // Start with active warehouses belonging to this tenant.
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

        // Calculate the balance from the movement ledger.
        // A missing movement group produces a zero balance.
        var movements = _db.StockMovements
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId);

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
                WarehouseId = warehouse.Id,
                WarehouseName = warehouse.Name,
                QuantityOnHand =
                    movementGroup.Sum(
                        m => (decimal?)m.QuantityChange) ?? 0m
            };

        var result = await balances
            .OrderBy(b => b.ProductName)
            .ThenBy(b => b.WarehouseName)
            .ToListAsync(cancellationToken);

        return Ok(result);
    }
}

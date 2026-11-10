
using System.Security.Claims;
using InventorySystem.Backend.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Backend.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public sealed class DashboardController : ControllerBase
{
    private readonly InventoryDbContext _db;

    public DashboardController(InventoryDbContext db)
    {
        _db = db;
    }

    // GET: api/dashboard/summary
    [HttpGet("summary")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> GetSummary(
        CancellationToken cancellationToken = default)
    {
        // 1. Validate the authenticated user's tenant.
        var tenantValue = User.FindFirstValue("tenant_id");

        if (!Guid.TryParse(tenantValue, out var tenantId))
        {
            return Unauthorized(new
            {
                message = "The authenticated user's tenant is invalid."
            });
        }

        
    // 2. Count active products belonging to this tenant.
    var activeProductCount = await _db.Products
        .AsNoTracking()
        .CountAsync(
            p => p.TenantId == tenantId && p.IsActive,
            cancellationToken);

    // 3. Count active warehouses belonging to this tenant.
    var activeWarehouseCount = await _db.Warehouses
        .AsNoTracking()
        .CountAsync(
            w => w.TenantId == tenantId && w.IsActive,
            cancellationToken);

    // 4. Calculate stock quantity from the movement ledger.
    var stockByProductAndWarehouse = await _db.StockMovements
        .AsNoTracking()
        .Where(m =>
            m.TenantId == tenantId &&
            m.Product.IsActive &&
            m.Warehouse.IsActive)
        .GroupBy(m => new
        {
            m.ProductId,
            m.WarehouseId,
            m.Product.UnitCost
        })
        .Select(g => new
        {
            QuantityOnHand = g.Sum(m => m.QuantityChange),
            UnitCost = g.Key.UnitCost
        })
        .ToListAsync(cancellationToken);

    // 5. Calculate total quantity and inventory cost value.
    var totalQuantityOnHand = stockByProductAndWarehouse
        .Sum(x => x.QuantityOnHand);

    var totalInventoryCostValue = stockByProductAndWarehouse
        .Sum(x => x.QuantityOnHand * x.UnitCost);

        
    // 6. Calculate purchase summary.
    var receivedPurchases = _db.Purchases
        .AsNoTracking()
        .Where(p =>
            p.TenantId == tenantId &&
            p.Status == "Received");

    var receivedPurchaseCount = await receivedPurchases
        .CountAsync(cancellationToken);

    var receivedPurchaseAmount = await receivedPurchases
        .SumAsync(p => (decimal?)p.TotalAmount, cancellationToken) ?? 0m;

    var draftPurchaseCount = await _db.Purchases
        .AsNoTracking()
        .CountAsync(
            p => p.TenantId == tenantId &&
                p.Status == "Draft",
            cancellationToken);


        // 7. Calculate sales summary.
    var completedSales = _db.Sales
        .AsNoTracking()
        .Where(s =>
            s.TenantId == tenantId &&
            s.Status == "Completed");

    var completedSalesCount = await completedSales
        .CountAsync(cancellationToken);

    var completedSalesAmount = await completedSales
        .SumAsync(s => (decimal?)s.TotalAmount, cancellationToken) ?? 0m;

    var draftSalesCount = await _db.Sales
        .AsNoTracking()
        .CountAsync(
            s => s.TenantId == tenantId &&
                s.Status == "Draft",
            cancellationToken);       



        // 8. Calculate payment summary for completed sales.
    var totalPaymentsReceived = await _db.SalePayments
        .AsNoTracking()
        .Where(p => p.Sale.TenantId == tenantId &&
                    p.Sale.Status == "Completed")
        .SumAsync(
            p => (decimal?)p.Amount,
            cancellationToken) ?? 0m;

    var totalOutstandingAmount = await completedSales
        .Select(s => s.TotalAmount -
            s.Payments.Sum(p => (decimal?)p.Amount ?? 0m))
        .SumAsync(
            amount => (decimal?)amount,
            cancellationToken) ?? 0m;


    // 9. Return inventory and purchase summary.
    return Ok(new
    {
        activeProductCount,
        activeWarehouseCount,
        totalQuantityOnHand,
        totalInventoryCostValue,
        receivedPurchaseCount,
        receivedPurchaseAmount,
        draftPurchaseCount,
        completedSalesCount,
        completedSalesAmount,
        draftSalesCount,
        totalPaymentsReceived,
        totalOutstandingAmount

        
    });


    }
}

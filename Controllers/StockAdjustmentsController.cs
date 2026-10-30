
using System.Data;
using System.Security.Claims;
using InventorySystem.Backend.Data;
using InventorySystem.Backend.DTOs;
using InventorySystem.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Backend.Controllers;

[ApiController]
[Route("api/stock-adjustments")]
[Authorize]
public sealed class StockAdjustmentsController : ControllerBase
{
    private readonly InventoryDbContext _db;

    public StockAdjustmentsController(InventoryDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateAdjustment(
        [FromBody] StockAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        // Read identity and tenant from the authenticated JWT.
        var userIdValue = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        var tenantIdValue = User.FindFirstValue("tenant_id");

        if (!Guid.TryParse(userIdValue, out var userId) ||
            !Guid.TryParse(tenantIdValue, out var tenantId))
        {
            return Unauthorized(new
            {
                message = "The authenticated user's identity or tenant is invalid."
            });
        }

        if (request.Quantity <= 0)
        {
            return BadRequest(new
            {
                message = "Quantity must be greater than zero."
            });
        }

        if (request.Direction is not ("Increase" or "Decrease"))
        {
            return BadRequest(new
            {
                message = "Direction must be Increase or Decrease."
            });
        }

        // Serializable isolation helps prevent concurrent adjustments
        // from both consuming the same available stock.
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        try
        {
            // Both records must belong to the authenticated tenant.
            var productExists = await _db.Products.AnyAsync(
                p => p.Id == request.ProductId
                     && p.TenantId == tenantId
                     && p.IsActive,
                cancellationToken);

            if (!productExists)
            {
                return NotFound(new
                {
                    message = "Active product not found in your business."
                });
            }

            var warehouseExists = await _db.Warehouses.AnyAsync(
                w => w.Id == request.WarehouseId
                     && w.TenantId == tenantId
                     && w.IsActive,
                cancellationToken);

            if (!warehouseExists)
            {
                return NotFound(new
                {
                    message = "Active warehouse not found in your business."
                });
            }

            // The ledger is the source of truth for stock balances.
            var currentBalance =
                await _db.StockMovements
                    .Where(m =>
                        m.TenantId == tenantId &&
                        m.ProductId == request.ProductId &&
                        m.WarehouseId == request.WarehouseId)
                    .SumAsync(
                        m => (decimal?)m.QuantityChange,
                        cancellationToken) ?? 0m;

            var quantityChange =
                request.Direction == "Increase"
                    ? request.Quantity
                    : -request.Quantity;

            var newBalance = currentBalance + quantityChange;

            if (newBalance < 0)
            {
                return Conflict(new
                {
                    message = "Insufficient stock for this adjustment.",
                    currentBalance,
                    requestedDecrease = request.Quantity
                });
            }

            var movement = new StockMovement
            {
                TenantId = tenantId,
                ProductId = request.ProductId,
                WarehouseId = request.WarehouseId,
                QuantityChange = quantityChange,
                Type = "Adjustment",
                Reference = request.Reference,
                Note = request.Note,
                CreatedByUserId = userId,
                CreatedAtUtc = DateTime.UtcNow
            };

            _db.StockMovements.Add(movement);

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Ok(new
            {
                message = "Stock adjustment recorded successfully.",
                movementId = movement.Id,
                productId = movement.ProductId,
                warehouseId = movement.WarehouseId,
                direction = request.Direction,
                quantity = request.Quantity,
                quantityChange = movement.QuantityChange,
                previousBalance = currentBalance,
                newBalance,
                reference = movement.Reference,
                createdAtUtc = movement.CreatedAtUtc
            });
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);

            // Let ASP.NET Core's configured exception handling
            // log and handle unexpected server errors.
            throw;
        }
    }
}

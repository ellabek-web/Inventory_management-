
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
[Route("api/purchases")]
[Authorize]
public sealed class PurchasesController : ControllerBase
{
    private readonly InventoryDbContext _db;

    public PurchasesController(InventoryDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateDraft(
        [FromBody] CreatePurchaseRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
                User.FindFirst("tenant_id")?.Value,
                out var tenantId))
        {
            return Unauthorized(new
            {
                message = "The authenticated user has no valid tenant claim."
            });
        }

        var reference = request.Reference.Trim();
        var supplierName = request.SupplierName.Trim();

        if (string.IsNullOrWhiteSpace(reference) ||
            string.IsNullOrWhiteSpace(supplierName))
        {
            return BadRequest(new
            {
                message = "Reference and supplier name are required."
            });
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            return BadRequest(new
            {
                message = "At least one purchase item is required."
            });
        }

        if (request.Items.Any(item =>
                item.ProductId == Guid.Empty ||
                item.Quantity <= 0 ||
                item.UnitCost < 0))
        {
            return BadRequest(new
            {
                message = "Each item requires a product, positive quantity, and non-negative unit cost."
            });
        }

        // Avoid silently accepting duplicate product lines.
        if (request.Items
            .GroupBy(item => item.ProductId)
            .Any(group => group.Count() > 1))
        {
            return BadRequest(new
            {
                message = "Each product may appear only once in a purchase."
            });
        }

        // Calculate and validate the total on the server.
        decimal totalAmount;

        try
        {
            totalAmount = request.Items.Sum(item =>
                checked(item.Quantity * item.UnitCost));

            if (totalAmount > 9999999999999999.99m)
            {
                return BadRequest(new
                {
                    message = "The purchase total exceeds the supported amount."
                });
            }
        }
        catch (OverflowException)
        {
            return BadRequest(new
            {
                message = "The purchase total exceeds the supported amount."
            });
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var referenceExists = await _db.Purchases.AnyAsync(
            purchase =>
                purchase.TenantId == tenantId &&
                purchase.Reference == reference,
            cancellationToken);

        if (referenceExists)
        {
            return Conflict(new
            {
                message = "A purchase with this reference already exists."
            });
        }

        var productIds = request.Items
            .Select(item => item.ProductId)
            .ToList();

        var products = await _db.Products
            .Where(product =>
                product.TenantId == tenantId &&
                product.IsActive &&
                productIds.Contains(product.Id))
            .Select(product => product.Id)
            .ToListAsync(cancellationToken);

        if (products.Count != productIds.Count)
        {
            return BadRequest(new
            {
                message = "One or more products do not exist, are inactive, or do not belong to your business."
            });
        }

        var purchase = new Purchase
        {
            TenantId = tenantId,
            Reference = reference,
            SupplierName = supplierName,
            Status = "Draft",
            TotalAmount = totalAmount,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Items = request.Items.Select(item => new PurchaseItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitCost = item.UnitCost
            }).ToList()
        };

        _db.Purchases.Add(purchase);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Created(
            $"/api/purchases/{purchase.Id}",
            new
            {
                purchase.Id,
                purchase.Reference,
                purchase.SupplierName,
                purchase.Status,
                purchase.TotalAmount,
                purchase.CreatedAtUtc,
                Items = purchase.Items.Select(item => new
                {
                    item.Id,
                    item.ProductId,
                    item.Quantity,
                    item.UnitCost,
                    LineTotal = item.Quantity * item.UnitCost
                })
            });
    }
  




   
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
                User.FindFirst("tenant_id")?.Value,
                out var tenantId))
        {
            return Unauthorized(new
            {
                message = "The authenticated user has no valid tenant claim."
            });
        }

        var purchase = await _db.Purchases
            .AsNoTracking()
            .Where(p =>
                p.Id == id &&
                p.TenantId == tenantId)
            .Select(p => new
            {
                p.Id,
                p.Reference,
                p.SupplierName,
                p.Status,
                p.TotalAmount,
                p.CreatedAtUtc,
                Items = p.Items.Select(item => new
                {
                    item.Id,
                    item.ProductId,
                    ProductName = item.Product.Name,
                    item.Quantity,
                    item.UnitCost,
                    LineTotal = item.Quantity * item.UnitCost
                })
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (purchase is null)
        {
            return NotFound(new
            {
                message = "Purchase not found."
            });
        }

        return Ok(purchase);
    }





    [HttpPost("{id:guid}/receive")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ReceivePurchase(
        Guid id,
        [FromBody] ReceivePurchaseRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
                User.FindFirst("tenant_id")?.Value,
                out var tenantId))
        {
            return Unauthorized(new
            {
                message = "The authenticated user has no valid tenant claim."
            });
        }

        var userIdClaim =
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new
            {
                message = "The authenticated user has no valid user ID."
            });
        }

        var actorEmail =
            User.FindFirst(ClaimTypes.Email)?.Value
            ?? User.Identity?.Name
            ?? "Unknown";

        if (request.WarehouseId == Guid.Empty)
        {
            return BadRequest(new
            {
                message = "A valid warehouse ID is required."
            });
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var purchase = await _db.Purchases
            .Include(p => p.Items)
            .FirstOrDefaultAsync(
                p => p.Id == id && p.TenantId == tenantId,
                cancellationToken);

        if (purchase is null)
        {
            return NotFound(new
            {
                message = "Purchase not found."
            });
        }

        if (purchase.Status != "Draft")
        {
            return Conflict(new
            {
                message = "Only draft purchases can be received."
            });
        }

        if (purchase.Items.Count == 0)
        {
            return BadRequest(new
            {
                message = "Cannot receive a purchase without items."
            });
        }

        var warehouse = await _db.Warehouses
            .FirstOrDefaultAsync(
                w =>
                    w.Id == request.WarehouseId &&
                    w.TenantId == tenantId &&
                    w.IsActive,
                cancellationToken);

        if (warehouse is null)
        {
            return BadRequest(new
            {
                message = "Warehouse does not exist, is inactive, or does not belong to your business."
            });
        }

        var productIds = purchase.Items
            .Select(item => item.ProductId)
            .ToList();

        var validProductIds = await _db.Products
            .Where(p =>
                p.TenantId == tenantId &&
                p.IsActive &&
                productIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (validProductIds.Count != productIds.Count)
        {
            return BadRequest(new
            {
                message = "One or more purchase products are no longer active or valid for this business."
            });
        }

        var now = DateTimeOffset.UtcNow;

        foreach (var item in purchase.Items)
        {
            _db.StockMovements.Add(new StockMovement
            {
                TenantId = tenantId,
                ProductId = item.ProductId,
                WarehouseId = warehouse.Id,
                QuantityChange = item.Quantity,
                Type = "Purchase",
                Reference = purchase.Reference,
                Note = $"Stock received from purchase {purchase.Reference}",
                CreatedByUserId = userId,
                CreatedAtUtc = now
            });
        }

        purchase.Status = "Received";

        _db.AuditEntries.Add(new AuditEntry
        {
            TenantId = tenantId,
            ActorUserId = userId,
            ActorEmail = actorEmail,
            Action = "PurchaseReceived",
            EntityName = "Purchase",
            EntityId = purchase.Id.ToString(),
            Details = $"Purchase {purchase.Reference} received into warehouse {warehouse.Name} ({warehouse.Code}).",
            CreatedAtUtc = now
        });

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

       return Ok(new
    {
        message = "Purchase received successfully.",
        PurchaseId = purchase.Id,
        purchase.Reference,
        purchase.Status,
        WarehouseId = warehouse.Id,
        WarehouseName = warehouse.Name,
        StockMovementsCreated = purchase.Items.Count
    });
    }




    [HttpGet]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> GetPurchases(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            return BadRequest(new
            {
                message = "Page must be greater than or equal to 1."
            });
        }

        if (pageSize < 1 || pageSize > 100)
        {
            return BadRequest(new
            {
                message = "Page size must be between 1 and 100."
            });
        }

        if (fromUtc.HasValue &&
            toUtc.HasValue &&
            fromUtc.Value > toUtc.Value)
        {
            return BadRequest(new
            {
                message = "The fromUtc date cannot be later than toUtc."
            });
        }

        if (!Guid.TryParse(
                User.FindFirst("tenant_id")?.Value,
                out var tenantId))
        {
            return Unauthorized(new
            {
                message = "The authenticated user has no valid tenant claim."
            });
        }

        var query = _db.Purchases
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId);

        // Search by purchase reference or supplier name.
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();

            query = query.Where(p =>
                p.Reference.Contains(searchTerm) ||
                p.SupplierName.Contains(searchTerm));
        }

        // Filter by purchase status.
        if (!string.IsNullOrWhiteSpace(status))
        {
            var statusFilter = status.Trim();

            if (statusFilter != "Draft" &&
                statusFilter != "Received")
            {
                return BadRequest(new
                {
                    message = "Status must be Draft or Received."
                });
            }

            query = query.Where(p => p.Status == statusFilter);
        }

        // Filter by creation date.
        if (fromUtc.HasValue)
        {
            query = query.Where(p =>
                p.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(p =>
                p.CreatedAtUtc <= toUtc.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(
            totalCount / (double)pageSize);

        var purchases = await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .ThenByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.Reference,
                p.SupplierName,
                p.Status,
                p.TotalAmount,
                p.CreatedAtUtc,
                ItemCount = p.Items.Count
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            page,
            pageSize,
            totalCount,
            totalPages,
            purchases
        });
    }




}

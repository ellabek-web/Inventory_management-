
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


    
    // GET: api/reports/stock-movements
    [HttpGet("stock-movements")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> GetStockMovementReport(
        [FromQuery] Guid? productId,
        [FromQuery] Guid? warehouseId,
        [FromQuery] string? type,
        [FromQuery] string? search,
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate pagination.
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

        // 2. Validate date range.
        if (fromUtc.HasValue &&
            toUtc.HasValue &&
            fromUtc.Value > toUtc.Value)
        {
            return BadRequest(new
            {
                message = "fromUtc must be earlier than or equal to toUtc."
            });
        }

        // 3. Validate tenant from authenticated JWT.
        var tenantValue = User.FindFirstValue("tenant_id");

        if (!Guid.TryParse(tenantValue, out var tenantId))
        {
            return Unauthorized(new
            {
                message = "The authenticated user's tenant is invalid."
            });
        }

        // 4. Start with tenant-scoped stock movements.
        var query = _db.StockMovements
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId);

        // 5. Apply product filter.
        if (productId.HasValue)
        {
            query = query.Where(
                m => m.ProductId == productId.Value);
        }

        // 6. Apply warehouse filter.
        if (warehouseId.HasValue)
        {
            query = query.Where(
                m => m.WarehouseId == warehouseId.Value);
        }

        // 7. Apply movement type filter.
        if (!string.IsNullOrWhiteSpace(type))
        {
            var movementType = type.Trim();

            query = query.Where(
                m => m.Type == movementType);
        }

        // 8. Apply date filters.
        if (fromUtc.HasValue)
        {
            query = query.Where(
                m => m.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(
                m => m.CreatedAtUtc <= toUtc.Value);
        }

        // 9. Apply text search.
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();

            query = query.Where(m =>
                m.Product.Name.Contains(searchTerm) ||
                m.Product.Sku.Contains(searchTerm) ||
                (m.Reference != null &&
                 m.Reference.Contains(searchTerm)));
        }

        // 10. Count before pagination.
        var totalCount = await query.CountAsync(
            cancellationToken);

        // 11. Retrieve report records.
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
                SKU = m.Product.Sku,

                m.WarehouseId,
                WarehouseName = m.Warehouse.Name,
                WarehouseCode = m.Warehouse.Code,

                m.Type,

                Direction = m.QuantityChange > 0
                    ? "IN"
                    : m.QuantityChange < 0
                        ? "OUT"
                        : "NONE",

                m.QuantityChange,
                m.Reference,
                m.Note,
                m.CreatedByUserId,
                m.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        // 12. Return paginated report.
        return Ok(new
        {
            page,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling(
                totalCount / (double)pageSize),
            items = movements
        });
    }



  
    // GET: api/reports/purchases
    [HttpGet("purchases")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> GetPurchaseReport(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate pagination.
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

        // 2. Validate date range.
        if (fromUtc.HasValue &&
            toUtc.HasValue &&
            fromUtc.Value > toUtc.Value)
        {
            return BadRequest(new
            {
                message = "fromUtc must be earlier than or equal to toUtc."
            });
        }

        // 3. Validate tenant from JWT.
        var tenantValue = User.FindFirstValue("tenant_id");

        if (!Guid.TryParse(tenantValue, out var tenantId))
        {
            return Unauthorized(new
            {
                message = "The authenticated user's tenant is invalid."
            });
        }

        // 4. Start with tenant-scoped purchases.
        var query = _db.Purchases
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId);

        // 5. Search by reference or supplier.
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();

            query = query.Where(p =>
                p.Reference.Contains(searchTerm) ||
                p.SupplierName.Contains(searchTerm));
        }

        // 6. Filter by purchase status.
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

            query = query.Where(
                p => p.Status == statusFilter);
        }

        // 7. Apply creation date filters.
        if (fromUtc.HasValue)
        {
            query = query.Where(
                p => p.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(
                p => p.CreatedAtUtc <= toUtc.Value);
        }

        // 8. Calculate summary across ALL filtered purchases.
        var summary = await query
            .GroupBy(p => 1)
            .Select(g => new
            {
                TotalPurchaseCount = g.Count(),

                TotalPurchaseAmount = g.Sum(
                    p => p.TotalAmount),

                DraftPurchaseCount = g.Count(
                    p => p.Status == "Draft"),

                DraftPurchaseAmount = g
                    .Where(p => p.Status == "Draft")
                    .Sum(p => p.TotalAmount),

                ReceivedPurchaseCount = g.Count(
                    p => p.Status == "Received"),

                ReceivedPurchaseAmount = g
                    .Where(p => p.Status == "Received")
                    .Sum(p => p.TotalAmount)
            })
            .FirstOrDefaultAsync(cancellationToken);

        // 9. Calculate pagination metadata.
        var totalCount = summary?.TotalPurchaseCount ?? 0;

        var totalPages = (int)Math.Ceiling(
            totalCount / (double)pageSize);

        // 10. Retrieve individual purchase records.
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

                ItemCount = p.Items.Count(),

                TotalQuantity = p.Items.Sum(
                    i => (decimal?)i.Quantity) ?? 0m
            })
            .ToListAsync(cancellationToken);

        // 11. Return report and summary.
        return Ok(new
        {
            page,
            pageSize,
            totalCount,
            totalPages,

            summary = new
            {
                totalPurchaseCount = totalCount,
                totalPurchaseAmount =
                    summary?.TotalPurchaseAmount ?? 0m,

                draftPurchaseCount =
                    summary?.DraftPurchaseCount ?? 0,

                draftPurchaseAmount =
                    summary?.DraftPurchaseAmount ?? 0m,

                receivedPurchaseCount =
                    summary?.ReceivedPurchaseCount ?? 0,

                receivedPurchaseAmount =
                    summary?.ReceivedPurchaseAmount ?? 0m
            },

            items = purchases
        });
    }


   
    // GET: api/reports/sales
    [HttpGet("sales")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> GetSalesReport(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? paymentStatus,
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate pagination.
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

        // 2. Validate date range.
        if (fromUtc.HasValue &&
            toUtc.HasValue &&
            fromUtc.Value > toUtc.Value)
        {
            return BadRequest(new
            {
                message = "fromUtc must be earlier than or equal to toUtc."
            });
        }

        // 3. Validate tenant from JWT.
        var tenantValue = User.FindFirstValue("tenant_id");

        if (!Guid.TryParse(tenantValue, out var tenantId))
        {
            return Unauthorized(new
            {
                message = "The authenticated user's tenant is invalid."
            });
        }

        // 4. Build a tenant-scoped sales query.
        // Calculate payment totals for each sale.
        var query = _db.Sales
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .Select(s => new
            {
                Sale = s,

                PaidAmount = s.Payments
                    .Sum(p => (decimal?)p.Amount) ?? 0m
            });

        // 5. Search by sale reference or customer name.
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();

            query = query.Where(x =>
                x.Sale.Reference.Contains(searchTerm) ||
                (x.Sale.CustomerName != null &&
                 x.Sale.CustomerName.Contains(searchTerm)));
        }

        // 6. Filter by sale status.
        if (!string.IsNullOrWhiteSpace(status))
        {
            var statusFilter = status.Trim();

            if (statusFilter != "Draft" &&
                statusFilter != "Completed")
            {
                return BadRequest(new
                {
                    message = "Status must be Draft or Completed."
                });
            }

            query = query.Where(
                x => x.Sale.Status == statusFilter);
        }

        // 7. Filter by payment status.
        if (!string.IsNullOrWhiteSpace(paymentStatus))
        {
            var paymentStatusFilter = paymentStatus.Trim();

            if (!paymentStatusFilter.Equals(
                    "Paid",
                    StringComparison.OrdinalIgnoreCase) &&
                !paymentStatusFilter.Equals(
                    "Unpaid",
                    StringComparison.OrdinalIgnoreCase) &&
                !paymentStatusFilter.Equals(
                    "PartiallyPaid",
                    StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message = "Payment status must be Paid, Unpaid, or PartiallyPaid."
                });
            }

            if (paymentStatusFilter.Equals(
                    "Unpaid",
                    StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(
                    x => x.PaidAmount <= 0m);
            }
            else if (paymentStatusFilter.Equals(
                         "PartiallyPaid",
                         StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(
                    x => x.PaidAmount > 0m &&
                         x.PaidAmount < x.Sale.TotalAmount);
            }
            else
            {
                query = query.Where(
                    x => x.PaidAmount > 0m &&
                         x.PaidAmount >= x.Sale.TotalAmount);
            }
        }

        // 8. Apply creation date filters.
        if (fromUtc.HasValue)
        {
            query = query.Where(
                x => x.Sale.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(
                x => x.Sale.CreatedAtUtc <= toUtc.Value);
        }

        // 9. Calculate summary across ALL filtered sales.
        var summary = await query
            .GroupBy(x => 1)
            .Select(g => new
            {
                TotalSalesCount = g.Count(),

                TotalSalesAmount = g.Sum(
                    x => x.Sale.TotalAmount),

                DraftSalesCount = g.Count(
                    x => x.Sale.Status == "Draft"),

                DraftSalesAmount = g
                    .Where(x => x.Sale.Status == "Draft")
                    .Sum(x => x.Sale.TotalAmount),

                CompletedSalesCount = g.Count(
                    x => x.Sale.Status == "Completed"),

                CompletedSalesAmount = g
                    .Where(x => x.Sale.Status == "Completed")
                    .Sum(x => x.Sale.TotalAmount),

                PaidSalesCount = g.Count(
                    x => x.PaidAmount > 0m &&
                         x.PaidAmount >= x.Sale.TotalAmount),

                PaidSalesAmount = g
                    .Where(x =>
                        x.PaidAmount > 0m &&
                        x.PaidAmount >= x.Sale.TotalAmount)
                    .Sum(x => x.Sale.TotalAmount),

                UnpaidSalesCount = g.Count(
                    x => x.PaidAmount <= 0m),

                UnpaidSalesAmount = g
                    .Where(x => x.PaidAmount <= 0m)
                    .Sum(x => x.Sale.TotalAmount),

                PartiallyPaidSalesCount = g.Count(
                    x => x.PaidAmount > 0m &&
                         x.PaidAmount < x.Sale.TotalAmount),

                PartiallyPaidSalesAmount = g
                    .Where(x =>
                        x.PaidAmount > 0m &&
                        x.PaidAmount < x.Sale.TotalAmount)
                    .Sum(x => x.Sale.TotalAmount),

                TotalPaidAmount = g.Sum(
                    x => x.PaidAmount),

                TotalOutstandingAmount = g.Sum(
                    x => x.Sale.TotalAmount - x.PaidAmount)
            })
            .FirstOrDefaultAsync(cancellationToken);

        // 10. Calculate pagination metadata.
        var totalCount = summary?.TotalSalesCount ?? 0;

        var totalPages = (int)Math.Ceiling(
            totalCount / (double)pageSize);

        // 11. Retrieve individual sale records.
        var sales = await query
            .OrderByDescending(x => x.Sale.CreatedAtUtc)
            .ThenByDescending(x => x.Sale.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Sale.Id,
                x.Sale.Reference,
                x.Sale.CustomerName,
                x.Sale.CustomerPhone,
                x.Sale.Status,
                x.Sale.TotalAmount,
                x.Sale.CreatedAtUtc,

                WarehouseId = x.Sale.WarehouseId,
                WarehouseName = x.Sale.Warehouse.Name,

                ItemCount = x.Sale.Items.Count(),

                PaymentCount = x.Sale.Payments.Count(),

                x.PaidAmount,

                OutstandingAmount =
                    x.Sale.TotalAmount - x.PaidAmount
            })
            .ToListAsync(cancellationToken);

        // 12. Return report and summary.
        return Ok(new
        {
            page,
            pageSize,
            totalCount,
            totalPages,

            summary = new
            {
                totalSalesCount = totalCount,

                totalSalesAmount =
                    summary?.TotalSalesAmount ?? 0m,

                draftSalesCount =
                    summary?.DraftSalesCount ?? 0,

                draftSalesAmount =
                    summary?.DraftSalesAmount ?? 0m,

                completedSalesCount =
                    summary?.CompletedSalesCount ?? 0,

                completedSalesAmount =
                    summary?.CompletedSalesAmount ?? 0m,

                paidSalesCount =
                    summary?.PaidSalesCount ?? 0,

                paidSalesAmount =
                    summary?.PaidSalesAmount ?? 0m,

                unpaidSalesCount =
                    summary?.UnpaidSalesCount ?? 0,

                unpaidSalesAmount =
                    summary?.UnpaidSalesAmount ?? 0m,

                partiallyPaidSalesCount =
                    summary?.PartiallyPaidSalesCount ?? 0,

                partiallyPaidSalesAmount =
                    summary?.PartiallyPaidSalesAmount ?? 0m,

                totalPaidAmount =
                    summary?.TotalPaidAmount ?? 0m,

                totalOutstandingAmount =
                    summary?.TotalOutstandingAmount ?? 0m
            },

            items = sales.Select(s => new
            {
                s.Id,
                s.Reference,
                s.CustomerName,
                s.CustomerPhone,
                s.Status,
                s.TotalAmount,
                s.CreatedAtUtc,
                s.WarehouseId,
                s.WarehouseName,
                s.ItemCount,
                s.PaymentCount,
                s.PaidAmount,
                s.OutstandingAmount,

                PaymentStatus =
                    s.PaidAmount <= 0m
                        ? "Unpaid"
                        : s.PaidAmount < s.TotalAmount
                            ? "PartiallyPaid"
                            : "Paid"
            })
        });
    }


   
    // GET: api/reports/audit-trail
    [HttpGet("audit-trail")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> GetAuditTrailReport(
        [FromQuery] string? search,
        [FromQuery] string? action,
        [FromQuery] string? entityName,
        [FromQuery] Guid? actorUserId,
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate pagination.
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

        // 2. Validate date range.
        if (fromUtc.HasValue &&
            toUtc.HasValue &&
            fromUtc.Value > toUtc.Value)
        {
            return BadRequest(new
            {
                message = "fromUtc must be earlier than or equal to toUtc."
            });
        }

        // 3. Validate tenant.
        var tenantValue = User.FindFirstValue("tenant_id");

        if (!Guid.TryParse(tenantValue, out var tenantId))
        {
            return Unauthorized(new
            {
                message = "The authenticated user's tenant is invalid."
            });
        }

        // 4. Start with tenant-scoped audit records.
        var query = _db.AuditEntries
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId);

        // 5. Apply general search.
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();

            query = query.Where(x =>
                x.Action.Contains(searchTerm) ||
                x.EntityName.Contains(searchTerm) ||
                (x.EntityId != null &&
                 x.EntityId.Contains(searchTerm)) ||
                x.ActorEmail.Contains(searchTerm) ||
                (x.Details != null &&
                 x.Details.Contains(searchTerm)));
        }

        // 6. Apply action filter.
        if (!string.IsNullOrWhiteSpace(action))
        {
            var actionTerm = action.Trim();

            query = query.Where(x =>
                x.Action == actionTerm);
        }

        // 7. Apply entity filter.
        if (!string.IsNullOrWhiteSpace(entityName))
        {
            var entityTerm = entityName.Trim();

            query = query.Where(x =>
                x.EntityName == entityTerm);
        }

        // 8. Apply actor filter.
        if (actorUserId.HasValue)
        {
            query = query.Where(x =>
                x.ActorUserId == actorUserId.Value);
        }

        // 9. Apply date filters.
        if (fromUtc.HasValue)
        {
            query = query.Where(x =>
                x.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x =>
                x.CreatedAtUtc <= toUtc.Value);
        }

        // 10. Count filtered records.
        var totalCount = await query.CountAsync(
            cancellationToken);

        var totalPages = (int)Math.Ceiling(
            totalCount / (double)pageSize);

        // 11. Retrieve paginated audit records.
        var entries = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.ActorUserId,
                x.ActorEmail,
                x.Action,
                x.EntityName,
                x.EntityId,
                x.Details,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        // 12. Return report.
        return Ok(new
        {
            page,
            pageSize,
            totalCount,
            totalPages,
            items = entries
        });
    }



}
 
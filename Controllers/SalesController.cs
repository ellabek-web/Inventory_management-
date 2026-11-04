
using System.Data;
using InventorySystem.Backend.Data;
using InventorySystem.Backend.DTOs;
using InventorySystem.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;



namespace InventorySystem.Backend.Controllers;

[ApiController]
[Route("api/sales")]
[Authorize]
public sealed class SalesController : ControllerBase
{
    private readonly InventoryDbContext _db;

    public SalesController(InventoryDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateDraft(
        [FromBody] CreateSaleRequest request,
        CancellationToken cancellationToken)
    {
        // Get tenant ID from the authenticated user's JWT.
        if (!Guid.TryParse(
                User.FindFirst("tenant_id")?.Value,
                out var tenantId))
        {
            return Unauthorized(new
            {
                message = "The authenticated user has no valid tenant claim."
            });
        }

        // Validate warehouse.
        if (request.WarehouseId == Guid.Empty)
        {
            return BadRequest(new
            {
                message = "A valid warehouse ID is required."
            });
        }

        // Validate and normalize sale reference.
        if (string.IsNullOrWhiteSpace(request.Reference))
        {
            return BadRequest(new
            {
                message = "Sale reference is required."
            });
        }

        var reference = request.Reference.Trim();

        if (reference.Length > 100)
        {
            return BadRequest(new
            {
                message = "Sale reference cannot exceed 100 characters."
            });
        }

        var customerName = string.IsNullOrWhiteSpace(request.CustomerName)
            ? null
            : request.CustomerName.Trim();

        var customerPhone = string.IsNullOrWhiteSpace(request.CustomerPhone)
            ? null
            : request.CustomerPhone.Trim();

        if (customerName?.Length > 200)
        {
            return BadRequest(new
            {
                message = "Customer name cannot exceed 200 characters."
            });
        }

        if (customerPhone?.Length > 30)
        {
            return BadRequest(new
            {
                message = "Customer phone cannot exceed 30 characters."
            });
        }

        // A sale must contain at least one item.
        if (request.Items is null || request.Items.Count == 0)
        {
            return BadRequest(new
            {
                message = "At least one sale item is required."
            });
        }

        // Validate individual sale items.
        if (request.Items.Any(item =>
                item.ProductId == Guid.Empty ||
                item.Quantity <= 0 ||
                item.UnitPrice < 0))
        {
            return BadRequest(new
            {
                message = "Each item requires a product, positive quantity, and non-negative unit price."
            });
        }

        // Avoid duplicate product lines.
        if (request.Items
            .GroupBy(item => item.ProductId)
            .Any(group => group.Count() > 1))
        {
            return BadRequest(new
            {
                message = "Each product may appear only once in a sale."
            });
        }

        // Calculate the sale total on the server.
        decimal totalAmount;

        try
        {
            totalAmount = request.Items.Sum(item =>
                checked(item.Quantity * item.UnitPrice));

            if (totalAmount > 9999999999999999.99m)
            {
                return BadRequest(new
                {
                    message = "The sale total exceeds the supported amount."
                });
            }
        }
        catch (OverflowException)
        {
            return BadRequest(new
            {
                message = "The sale total exceeds the supported amount."
            });
        }

        // Use a serializable transaction to protect reference validation
        // and sale creation from concurrent requests.
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        // Check that the warehouse is active and belongs to this tenant.
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

        // Check sale reference uniqueness within the tenant.
        var referenceExists = await _db.Sales.AnyAsync(
            sale =>
                sale.TenantId == tenantId &&
                sale.Reference == reference,
            cancellationToken);

        if (referenceExists)
        {
            return Conflict(new
            {
                message = "A sale with this reference already exists."
            });
        }

        // Validate that every product is active and belongs to this tenant.
        var productIds = request.Items
            .Select(item => item.ProductId)
            .ToList();

        var validProductIds = await _db.Products
            .Where(product =>
                product.TenantId == tenantId &&
                product.IsActive &&
                productIds.Contains(product.Id))
            .Select(product => product.Id)
            .ToListAsync(cancellationToken);

        if (validProductIds.Count != productIds.Count)
        {
            return BadRequest(new
            {
                message = "One or more products do not exist, are inactive, or do not belong to your business."
            });
        }

        // Get the authenticated user ID.
        Guid? createdByUserId = null;

        if (Guid.TryParse(
                User.FindFirst(
                    System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                out var userId))
        {
            createdByUserId = userId;
        }

        var now = DateTimeOffset.UtcNow;

        // Create the draft sale and its items.
        var sale = new Sale
        {
            TenantId = tenantId,
            WarehouseId = warehouse.Id,
            Reference = reference,
            CustomerName = customerName,
            CustomerPhone = customerPhone,
            Status = "Draft",
            TotalAmount = totalAmount,
            CreatedAtUtc = now,
            CreatedByUserId = createdByUserId,

            Items = request.Items.Select(item => new SaleItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            }).ToList()
        };

        _db.Sales.Add(sale);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Created(
            $"/api/sales/{sale.Id}",
            new
            {
                sale.Id,
                sale.Reference,
                sale.CustomerName,
                sale.CustomerPhone,
                sale.Status,
                sale.TotalAmount,
                sale.WarehouseId,
                WarehouseName = warehouse.Name,
                sale.CreatedAtUtc,

                PaymentStatus = "Unpaid",

                Items = sale.Items.Select(item => new
                {
                    item.Id,
                    item.ProductId,
                    item.Quantity,
                    item.UnitPrice,
                    LineTotal = item.Quantity * item.UnitPrice
                })
            });
    }





    
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var tenantIdClaim = User.FindFirst("tenant_id")?.Value;

        if (!Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            return Unauthorized(new
            {
                message = "Invalid tenant information."
            });
        }

        var sale = await _db.Sales
            .AsNoTracking()
            .Where(s => s.Id == id && s.TenantId == tenantId)
            .Select(s => new
            {
                s.Id,
                s.Reference,
                s.CustomerName,
                s.CustomerPhone,
                s.Status,
                s.TotalAmount,
                s.CreatedAtUtc,

                WarehouseId = s.WarehouseId,
                WarehouseName = s.Warehouse.Name,

                Items = s.Items.Select(item => new
                {
                    item.Id,
                    item.ProductId,
                    ProductName = item.Product.Name,
                    item.Quantity,
                    item.UnitPrice,
                    LineTotal = item.Quantity * item.UnitPrice
                }),

                Payments = s.Payments
                    .OrderByDescending(p => p.ReceivedAtUtc)
                    .Select(p => new
                    {
                        p.Id,
                        p.Amount,
                        p.PaymentMethod,
                        p.Reference,
                        p.ReceivedAtUtc,
                        p.ReceivedByUserId
                    })
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (sale == null)
        {
            return NotFound(new
            {
                message = "Sale not found."
            });
        }

        var paidAmount = sale.Payments.Sum(p => p.Amount);

        var paymentStatus =
            paidAmount <= 0
                ? "Unpaid"
                : paidAmount < sale.TotalAmount
                    ? "PartiallyPaid"
                    : "Paid";

        return Ok(new
        {
            sale.Id,
            sale.Reference,
            sale.CustomerName,
            sale.CustomerPhone,
            sale.Status,
            sale.TotalAmount,

            sale.WarehouseId,
            sale.WarehouseName,

            sale.CreatedAtUtc,

            PaidAmount = paidAmount,
            OutstandingAmount = sale.TotalAmount - paidAmount,
            PaymentStatus = paymentStatus,

            sale.Items,
            sale.Payments
        });
    }




    
    [HttpPost("{id:guid}/complete")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CompleteSale(
        Guid id,
        CancellationToken cancellationToken)
    {
        // 1. Validate tenant
        if (!Guid.TryParse(
                User.FindFirst("tenant_id")?.Value,
                out var tenantId))
        {
            return Unauthorized(new
            {
                message = "The authenticated user has no valid tenant claim."
            });
        }

        // 2. Validate user
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

        // 3. Begin serializable transaction
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        // 4. Find sale belonging to this tenant
        var sale = await _db.Sales
            .Include(s => s.Items)
            .FirstOrDefaultAsync(
                s => s.Id == id && s.TenantId == tenantId,
                cancellationToken);

        if (sale is null)
        {
            return NotFound(new
            {
                message = "Sale not found."
            });
        }

        // 5. Only draft sales can be completed
        if (sale.Status != "Draft")
        {
            return Conflict(new
            {
                message = "Only draft sales can be completed."
            });
        }

        if (sale.Items.Count == 0)
        {
            return BadRequest(new
            {
                message = "Cannot complete a sale without items."
            });
        }

        // 6. Validate warehouse
        var warehouse = await _db.Warehouses
            .FirstOrDefaultAsync(
                w =>
                    w.Id == sale.WarehouseId &&
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

        // 7. Validate products
        var productIds = sale.Items
            .Select(item => item.ProductId)
            .Distinct()
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
                message = "One or more sale products are no longer active or valid for this business."
            });
        }

        // 8. Check stock and prepare stock movements
        var now = DateTimeOffset.UtcNow;

        foreach (var item in sale.Items)
        {
            var currentStock = await _db.StockMovements
                .Where(m =>
                    m.TenantId == tenantId &&
                    m.ProductId == item.ProductId &&
                    m.WarehouseId == warehouse.Id)
                .SumAsync(
                    m => (decimal?)m.QuantityChange,
                    cancellationToken) ?? 0m;

            if (currentStock < item.Quantity)
            {
                return Conflict(new
                {
                    message = "Insufficient stock to complete this sale.",
                    ProductId = item.ProductId,
                    AvailableQuantity = currentStock,
                    RequestedQuantity = item.Quantity
                });
            }

            // Create negative stock movement
            _db.StockMovements.Add(new StockMovement
            {
                TenantId = tenantId,
                ProductId = item.ProductId,
                WarehouseId = warehouse.Id,
                QuantityChange = -item.Quantity,
                Type = "Sale",
                Reference = sale.Reference,
                Note = $"Stock deducted for sale {sale.Reference}",
                CreatedByUserId = userId,
                CreatedAtUtc = now
            });
        }

        // 9. Update sale status
        sale.Status = "Completed";

        // 10. Create audit entry
        _db.AuditEntries.Add(new AuditEntry
        {
            TenantId = tenantId,
            ActorUserId = userId,
            ActorEmail = actorEmail,
            Action = "SaleCompleted",
            EntityName = "Sale",
            EntityId = sale.Id.ToString(),
            Details =
                $"Sale {sale.Reference} completed. " +
                $"Stock deducted from warehouse {warehouse.Name} ({warehouse.Code}).",
            CreatedAtUtc = now
        });

        // 11. Save and commit
        await _db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Ok(new
        {
            message = "Sale completed successfully.",
            SaleId = sale.Id,
            sale.Reference,
            sale.Status,
            WarehouseId = warehouse.Id,
            WarehouseName = warehouse.Name,
            StockMovementsCreated = sale.Items.Count,
            sale.TotalAmount
        });
    }






    [HttpPost("{id:guid}/payments")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RecordPayment(
        Guid id,
        [FromBody] RecordSalePaymentRequest request,
        CancellationToken cancellationToken)
    {
        // 1. Validate tenant
        if (!Guid.TryParse(
                User.FindFirst("tenant_id")?.Value,
                out var tenantId))
        {
            return Unauthorized(new
            {
                message = "The authenticated user has no valid tenant claim."
            });
        }

        // 2. Validate user
        var userIdClaim =
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new
            {
                message = "The authenticated user has no valid user ID."
            });
        }

        // 3. Validate payment method
        var allowedMethods = new[]
        {
            "Cash",
            "BankTransfer",
            "MobileMoney",
            "Card"
        };

        var paymentMethod = allowedMethods.FirstOrDefault(
            method => string.Equals(
                method,
                request.PaymentMethod?.Trim(),
                StringComparison.OrdinalIgnoreCase));

        if (paymentMethod is null)
        {
            return BadRequest(new
            {
                message = "Invalid payment method. Allowed methods: Cash, BankTransfer, MobileMoney, Card."
            });
        }

        // 4. Validate amount
        if (request.Amount <= 0)
        {
            return BadRequest(new
            {
                message = "Payment amount must be greater than zero."
            });
        }

        if (request.Amount > 9999999999999999.99m)
        {
            return BadRequest(new
            {
                message = "Payment amount exceeds the allowed limit."
            });
        }

        // 5. Begin transaction
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        // 6. Find sale belonging to tenant
        var sale = await _db.Sales
            .FirstOrDefaultAsync(
                s => s.Id == id && s.TenantId == tenantId,
                cancellationToken);

        if (sale is null)
        {
            return NotFound(new
            {
                message = "Sale not found."
            });
        }

        // 7. Only completed sales can receive payments
        if (sale.Status != "Completed")
        {
            return Conflict(new
            {
                message = "Payments can only be recorded for completed sales."
            });
        }

        // 8. Calculate existing payments
        var paidAmount = await _db.SalePayments
            .Where(p => p.SaleId == sale.Id)
            .SumAsync(
                p => (decimal?)p.Amount,
                cancellationToken) ?? 0m;

        var outstandingAmount = sale.TotalAmount - paidAmount;

        // 9. Prevent overpayment
        if (outstandingAmount <= 0)
        {
            return Conflict(new
            {
                message = "This sale has already been fully paid.",
                TotalAmount = sale.TotalAmount,
                PaidAmount = paidAmount,
                OutstandingAmount = 0m
            });
        }

        if (request.Amount > outstandingAmount)
        {
            return Conflict(new
            {
                message = "Payment amount exceeds the outstanding balance.",
                TotalAmount = sale.TotalAmount,
                PaidAmount = paidAmount,
                OutstandingAmount = outstandingAmount,
                RequestedAmount = request.Amount
            });
        }

        // 10. Create payment
        var now = DateTimeOffset.UtcNow;

        var payment = new SalePayment
        {
            SaleId = sale.Id,
            Amount = request.Amount,
            PaymentMethod = paymentMethod,
            Reference = string.IsNullOrWhiteSpace(request.Reference)
                ? null
                : request.Reference.Trim(),
            ReceivedAtUtc = now,
            ReceivedByUserId = userId
        };

        _db.SalePayments.Add(payment);

        // 11. Create audit entry
        var actorEmail =
            User.FindFirst(ClaimTypes.Email)?.Value
            ?? User.Identity?.Name
            ?? "Unknown";

        _db.AuditEntries.Add(new AuditEntry
        {
            TenantId = tenantId,
            ActorUserId = userId,
            ActorEmail = actorEmail,
            Action = "SalePaymentRecorded",
            EntityName = "Sale",
            EntityId = sale.Id.ToString(),
            Details =
                $"Payment of {request.Amount:F2} recorded for sale " +
                $"{sale.Reference} using {paymentMethod}.",
            CreatedAtUtc = now
        });

        // 12. Save and commit
        await _db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        // 13. Calculate updated balance
        var newPaidAmount = paidAmount + request.Amount;
        var newOutstandingAmount =
            sale.TotalAmount - newPaidAmount;

        var paymentStatus =
            newPaidAmount >= sale.TotalAmount
                ? "Paid"
                : "PartiallyPaid";

        return Ok(new
        {
            message = "Payment recorded successfully.",

            PaymentId = payment.Id,
            SaleId = sale.Id,
            SaleReference = sale.Reference,

            payment.Amount,
            payment.PaymentMethod,
            payment.Reference,
            payment.ReceivedAtUtc,
            payment.ReceivedByUserId,

            TotalAmount = sale.TotalAmount,
            PaidAmount = newPaidAmount,
            OutstandingAmount = newOutstandingAmount,
            PaymentStatus = paymentStatus
        });
    }


    [HttpGet("{id:guid}/payments")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> GetPaymentHistory(
        Guid id,
        CancellationToken cancellationToken)
    {
        // 1. Validate tenant
        if (!Guid.TryParse(
                User.FindFirst("tenant_id")?.Value,
                out var tenantId))
        {
            return Unauthorized(new
            {
                message = "The authenticated user has no valid tenant claim."
            });
        }

        // 2. Find sale belonging to this tenant
        var sale = await _db.Sales
            .AsNoTracking()
            .Where(s =>
                s.Id == id &&
                s.TenantId == tenantId)
            .Select(s => new
            {
                s.Id,
                s.Reference,
                s.Status,
                s.TotalAmount
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (sale is null)
        {
            return NotFound(new
            {
                message = "Sale not found."
            });
        }

        // 3. Retrieve payment history
        var payments = await _db.SalePayments
            .AsNoTracking()
            .Where(p => p.SaleId == sale.Id)
            .OrderByDescending(p => p.ReceivedAtUtc)
            .Select(p => new
            {
                p.Id,
                p.Amount,
                p.PaymentMethod,
                p.Reference,
                p.ReceivedAtUtc,
                p.ReceivedByUserId
            })
            .ToListAsync(cancellationToken);

        // 4. Calculate payment summary
        var paidAmount = payments.Sum(p => p.Amount);

        var outstandingAmount =
            sale.TotalAmount - paidAmount;

        var paymentStatus =
            paidAmount <= 0
                ? "Unpaid"
                : paidAmount < sale.TotalAmount
                    ? "PartiallyPaid"
                    : "Paid";

        // 5. Return payment history
        return Ok(new
        {
            SaleId = sale.Id,
            sale.Reference,
            sale.Status,
            sale.TotalAmount,

            PaidAmount = paidAmount,
            OutstandingAmount = outstandingAmount,
            PaymentStatus = paymentStatus,

            PaymentCount = payments.Count,
            Payments = payments
        });
    }





    [HttpGet]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> GetSales(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? paymentStatus = null,
        [FromQuery] DateTimeOffset? fromUtc = null,
        [FromQuery] DateTimeOffset? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate pagination values.
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

        // 2. Get tenant ID from the authenticated user's JWT.
        if (!Guid.TryParse(
                User.FindFirst("tenant_id")?.Value,
                out var tenantId))
        {
            return Unauthorized(new
            {
                message = "Invalid tenant information."
            });
        }

        // 3. Build a tenant-scoped query.
        var salesQuery = _db.Sales
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId);


       // 4. Apply search filter when provided.
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();

            salesQuery = salesQuery.Where(s =>
                s.Reference.Contains(searchTerm) ||
                (s.CustomerName != null &&
                s.CustomerName.Contains(searchTerm)));
        }


        // 5. Apply sale status filter when provided.
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim();

            if (normalizedStatus != "Draft" &&
                normalizedStatus != "Completed")
            {
                return BadRequest(new
                {
                    message = "Invalid sale status. Allowed values are Draft and Completed."
                });
            }

            salesQuery = salesQuery.Where(s =>
                s.Status == normalizedStatus);
        }


    // 6. Apply payment status filter when provided.
    if (!string.IsNullOrWhiteSpace(paymentStatus))
    {
        var normalizedPaymentStatus = paymentStatus.Trim();

        if (!normalizedPaymentStatus.Equals(
                "Paid", StringComparison.OrdinalIgnoreCase) &&
            !normalizedPaymentStatus.Equals(
                "Unpaid", StringComparison.OrdinalIgnoreCase) &&
            !normalizedPaymentStatus.Equals(
                "PartiallyPaid", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Invalid payment status. Allowed values are Paid, Unpaid, and PartiallyPaid."
            });
        }

        if (normalizedPaymentStatus.Equals(
                "Unpaid", StringComparison.OrdinalIgnoreCase))
        {
            salesQuery = salesQuery.Where(s =>
                (s.Payments.Sum(p => (decimal?)p.Amount) ?? 0m) <= 0m);
        }
       else if (normalizedPaymentStatus.Equals(
                    "PartiallyPaid", StringComparison.OrdinalIgnoreCase))
        {
            salesQuery = salesQuery.Where(s =>
                (s.Payments.Sum(p => (decimal?)p.Amount) ?? 0m) > 0m &&
                (s.Payments.Sum(p => (decimal?)p.Amount) ?? 0m) < s.TotalAmount);
        }


        else
        {
            salesQuery = salesQuery.Where(s =>
                (s.Payments.Sum(p => (decimal?)p.Amount) ?? 0m) > 0m &&
                (s.Payments.Sum(p => (decimal?)p.Amount) ?? 0m) >= s.TotalAmount);
        }


    }





        // 7. Validate date range.
        if (fromUtc.HasValue &&
            toUtc.HasValue &&
            fromUtc.Value > toUtc.Value)
        {
            return BadRequest(new
            {
                message = "fromUtc must be earlier than or equal to toUtc."
            });
        }

        // 8. Apply start date filter.
        if (fromUtc.HasValue)
        {
            salesQuery = salesQuery.Where(s =>
                s.CreatedAtUtc >= fromUtc.Value);
        }

        // 9. Apply end date filter.
        if (toUtc.HasValue)
        {
            salesQuery = salesQuery.Where(s =>
                s.CreatedAtUtc <= toUtc.Value);
        }






        // 6. Count records before pagination.
        var totalCount = await salesQuery.CountAsync(
            cancellationToken);

        // 7. Retrieve the requested page.
        var sales = await salesQuery
            .OrderByDescending(s => s.CreatedAtUtc)
            .ThenByDescending(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id,
                s.Reference,
                s.CustomerName,
                s.CustomerPhone,
                s.Status,
                s.TotalAmount,
                s.CreatedAtUtc,

                WarehouseId = s.WarehouseId,
                WarehouseName = s.Warehouse.Name,

                ItemCount = s.Items.Count(),

                PaidAmount = s.Payments
                    .Sum(p => (decimal?)p.Amount) ?? 0m
            })
            .ToListAsync(cancellationToken);

        // 8. Return paginated results.
        return Ok(new
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(
                totalCount / (double)pageSize),

            Items = sales.Select(s => new
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
                s.PaidAmount,

                OutstandingAmount =
                    s.TotalAmount - s.PaidAmount,

                PaymentStatus =
                    s.PaidAmount <= 0
                        ? "Unpaid"
                        : s.PaidAmount < s.TotalAmount
                            ? "PartiallyPaid"
                            : "Paid"
            })
        });
    }


}

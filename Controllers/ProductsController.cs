
using InventorySystem.Backend.Data;
using InventorySystem.Backend.DTOs;
using InventorySystem.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Backend.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public sealed class ProductsController : ControllerBase
{
    private readonly InventoryDbContext _db;

    public ProductsController(InventoryDbContext db)
    {
        _db = db;
    }

    private bool TryGetTenantId(out Guid tenantId)
    {
        return Guid.TryParse(
            User.FindFirst("tenant_id")?.Value,
            out tenantId);
    }

    // GET: /api/products
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (!TryGetTenantId(out var tenantId))
            return Unauthorized();

        var products = await _db.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Sku,
                p.Barcode,
                p.UnitOfMeasure,
                p.UnitCost,
                p.SellingPrice,
                p.IsActive,
                p.CreatedAtUtc
            })
            .ToListAsync();

        return Ok(products);
    }

    // GET: /api/products/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        if (!TryGetTenantId(out var tenantId))
            return Unauthorized();

        var product = await _db.Products
            .AsNoTracking()
            .Where(p => p.Id == id && p.TenantId == tenantId)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Sku,
                p.Barcode,
                p.UnitOfMeasure,
                p.UnitCost,
                p.SellingPrice,
                p.IsActive,
                p.CreatedAtUtc
            })
            .FirstOrDefaultAsync();

        if (product is null)
            return NotFound();

        return Ok(product);
    }


    // POST: /api/products
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateProductRequest request)
    {
        if (!TryGetTenantId(out var tenantId))
            return Unauthorized();

        var tenantExists = await _db.BusinessTenants
            .AnyAsync(t => t.Id == tenantId);

        if (!tenantExists)
            return Unauthorized();

        // Normalize the SKU before checking for duplicates.
        var normalizedSku = request.Sku.Trim();

        var skuExists = await _db.Products.AnyAsync(
            p => p.TenantId == tenantId &&
                p.Sku == normalizedSku);

        if (skuExists)
        {
            return Conflict(new
            {
                message = "A product with this SKU already exists in your business."
            });
        }




        var normalizedBarcode = string.IsNullOrWhiteSpace(request.Barcode)
            ? null
            : request.Barcode.Trim();

        if (normalizedBarcode is not null)
        {
            var barcodeExists = await _db.Products.AnyAsync(
                p => p.TenantId == tenantId &&
                    p.Barcode == normalizedBarcode);

            if (barcodeExists)
            {
                return Conflict(new
                {
                    message = "A product with this barcode already exists in your business."
                });
            }
        }






        var product = new Product
        {
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Sku = normalizedSku,
            Barcode = normalizedBarcode,
            UnitOfMeasure = request.UnitOfMeasure.Trim(),
            UnitCost = request.UnitCost,
            SellingPrice = request.SellingPrice,
            IsActive = true
        };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetById),
            new { id = product.Id },
            new
            {
                product.Id,
                product.Name,
                product.Sku,
                product.Barcode,
                product.UnitOfMeasure,
                product.UnitCost,
                product.SellingPrice,
                product.IsActive,
                product.CreatedAtUtc
            });
    }



    
    // PUT: /api/products/{id}
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateProductRequest request)
    {
        if (!TryGetTenantId(out var tenantId))
            return Unauthorized();

        var product = await _db.Products.FirstOrDefaultAsync(
            p => p.Id == id && p.TenantId == tenantId);

        if (product is null)
            return NotFound();

        var normalizedSku = request.Sku.Trim();

        var skuExists = await _db.Products.AnyAsync(
            p => p.TenantId == tenantId &&
                 p.Sku == normalizedSku &&
                 p.Id != id);

        if (skuExists)
        {
            return Conflict(new
            {
                message = "Another product with this SKU already exists in your business."
            });
        }

        var normalizedBarcode = string.IsNullOrWhiteSpace(request.Barcode)
            ? null
            : request.Barcode.Trim();

        if (normalizedBarcode is not null)
        {
            var barcodeExists = await _db.Products.AnyAsync(
                p => p.TenantId == tenantId &&
                     p.Barcode == normalizedBarcode &&
                     p.Id != id);

            if (barcodeExists)
            {
                return Conflict(new
                {
                    message = "Another product with this barcode already exists in your business."
                });
            }
        }

        product.Name = request.Name.Trim();
        product.Sku = normalizedSku;
        product.Barcode = normalizedBarcode;
        product.UnitOfMeasure = request.UnitOfMeasure.Trim();
        product.UnitCost = request.UnitCost;
        product.SellingPrice = request.SellingPrice;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            product.Id,
            product.Name,
            product.Sku,
            product.Barcode,
            product.UnitOfMeasure,
            product.UnitCost,
            product.SellingPrice,
            product.IsActive,
            product.CreatedAtUtc
        });
    }




        // PATCH: /api/products/{id}/activate
    [Authorize(Roles = "Admin")]
    [HttpPatch("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        if (!TryGetTenantId(out var tenantId))
            return Unauthorized();

        var product = await _db.Products.FirstOrDefaultAsync(
            p => p.Id == id && p.TenantId == tenantId);

        if (product is null)
            return NotFound();

        if (product.IsActive)
        {
            return Conflict(new
            {
                message = "Product is already active."
            });
        }

        product.IsActive = true;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Product activated successfully.",
            product.Id,
            product.Name,
            product.IsActive
        });
    }



        // PATCH: /api/products/{id}/deactivate
    [Authorize(Roles = "Admin")]
    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        if (!TryGetTenantId(out var tenantId))
            return Unauthorized();

        var product = await _db.Products.FirstOrDefaultAsync(
            p => p.Id == id && p.TenantId == tenantId);

        if (product is null)
            return NotFound();

        if (!product.IsActive)
        {
            return Conflict(new
            {
                message = "Product is already inactive."
            });
        }

        product.IsActive = false;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Product deactivated successfully.",
            product.Id,
            product.Name,
            product.IsActive
        });
    }
}

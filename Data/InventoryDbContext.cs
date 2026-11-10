using InventorySystem.Backend.Models;
using Microsoft.EntityFrameworkCore;
namespace InventorySystem.Backend.Data;

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<BusinessTenant> BusinessTenants => Set<BusinessTenant>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<SalePayment> SalePayments => Set<SalePayment>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<BusinessTenant>(e => { e.ToTable("BusinessTenants"); e.HasKey(x => x.Id); e.Property(x => x.BusinessName).HasMaxLength(200).IsRequired(); });
        b.Entity<AppUser>(e => {
            e.ToTable("AppUsers"); e.HasKey(x => x.Id); e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired(); e.Property(x => x.Role).HasMaxLength(50).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
            e.HasOne(x => x.Tenant).WithMany(x => x.Users).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Product>(e => {
            e.ToTable("Products"); e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Sku).HasMaxLength(80).IsRequired(); e.Property(x => x.Barcode).HasMaxLength(100);
            e.Property(x => x.UnitOfMeasure).HasMaxLength(40).IsRequired(); e.Property(x => x.UnitCost).HasPrecision(18, 2); e.Property(x => x.SellingPrice).HasPrecision(18, 2);
            e.HasIndex(x => new { x.TenantId, x.Sku }).IsUnique(); e.HasIndex(x => new { x.TenantId, x.Barcode })
                .IsUnique()
                .HasFilter("[Barcode] IS NOT NULL");
            e.HasOne(x => x.Tenant).WithMany(x => x.Products).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Warehouse>(e => {
            e.ToTable("Warehouses"); e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(150).IsRequired(); e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique(); e.HasOne(x => x.Tenant).WithMany(x => x.Warehouses).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
        
b.Entity<StockMovement>(e =>
{
    e.ToTable("StockMovements");
    e.HasKey(x => x.Id);

    e.Property(x => x.Type)
        .HasMaxLength(40)
        .IsRequired();

    e.Property(x => x.Reference)
        .HasMaxLength(100);

    e.Property(x => x.Note)
        .HasMaxLength(500);

    e.Property(x => x.QuantityChange)
        .HasPrecision(18, 3);

    e.HasIndex(x => new
    {
        x.TenantId,
        x.ProductId,
        x.WarehouseId,
        x.CreatedAtUtc
    });

    e.HasOne(x => x.Tenant)
        .WithMany()
        .HasForeignKey(x => x.TenantId)
        .OnDelete(DeleteBehavior.Restrict);

    e.HasOne(x => x.Product)
        .WithMany(x => x.StockMovements)
        .HasForeignKey(x => x.ProductId)
        .OnDelete(DeleteBehavior.Restrict);

    e.HasOne(x => x.Warehouse)
        .WithMany(x => x.StockMovements)
        .HasForeignKey(x => x.WarehouseId)
        .OnDelete(DeleteBehavior.Restrict);
});

        b.Entity<Purchase>(e => {
            e.ToTable("Purchases"); e.HasKey(x => x.Id); e.Property(x => x.Reference).HasMaxLength(100).IsRequired(); e.Property(x => x.SupplierName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Status).HasMaxLength(40).IsRequired(); e.Property(x => x.TotalAmount).HasPrecision(18, 2); e.HasIndex(x => new { x.TenantId, x.Reference }).IsUnique();
            e.HasMany(x => x.Items).WithOne(x => x.Purchase).HasForeignKey(x => x.PurchaseId).OnDelete(DeleteBehavior.Restrict);
        });
       b.Entity<PurchaseItem>(e =>
        {
            e.ToTable("PurchaseItems");
            e.HasKey(x => x.Id);

            e.Property(x => x.Quantity)
                .HasPrecision(18, 3);

            e.Property(x => x.UnitCost)
                .HasPrecision(18, 2);

            e.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        
        b.Entity<Sale>(e =>
        {
            e.ToTable("Sales");
            e.HasKey(x => x.Id);

            e.Property(x => x.Reference)
                .HasMaxLength(100)
                .IsRequired();

            e.Property(x => x.CustomerName)
                .HasMaxLength(200);

            e.Property(x => x.CustomerPhone)
                .HasMaxLength(30);

            e.Property(x => x.Status)
                .HasMaxLength(40)
                .IsRequired();

            e.Property(x => x.TotalAmount)
                .HasPrecision(18, 2);

            e.HasIndex(x => new { x.TenantId, x.Reference })
                .IsUnique();

            e.HasIndex(x => new { x.TenantId, x.CreatedAtUtc });

            e.HasOne(x => x.Warehouse)
                .WithMany()
                .HasForeignKey(x => x.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(x => x.Items)
                .WithOne(x => x.Sale)
                .HasForeignKey(x => x.SaleId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(x => x.Payments)
                .WithOne(x => x.Sale)
                .HasForeignKey(x => x.SaleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<SaleItem>(e =>
        {
            e.ToTable("SaleItems");
            e.HasKey(x => x.Id);

            e.Property(x => x.Quantity)
                .HasPrecision(18, 3);

            e.Property(x => x.UnitPrice)
                .HasPrecision(18, 2);

            e.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });


        b.Entity<SalePayment>(e =>
        {
            e.ToTable("SalePayments");
            e.HasKey(x => x.Id);

            e.Property(x => x.Amount)
                .HasPrecision(18, 2);

            e.Property(x => x.PaymentMethod)
                .HasMaxLength(40)
                .IsRequired();

            e.Property(x => x.Reference)
                .HasMaxLength(100);

            e.HasIndex(x => x.SaleId);

            e.HasOne(x => x.Sale)
                .WithMany(x => x.Payments)
                .HasForeignKey(x => x.SaleId)
                .OnDelete(DeleteBehavior.Restrict);
        });






        b.Entity<AuditEntry>(e => {
            e.ToTable("AuditEntries"); e.HasKey(x => x.Id); e.Property(x => x.ActorEmail).HasMaxLength(256).IsRequired(); e.Property(x => x.Action).HasMaxLength(100).IsRequired();
            e.Property(x => x.EntityName).HasMaxLength(100).IsRequired(); e.Property(x => x.EntityId).HasMaxLength(100); e.Property(x => x.Details).HasMaxLength(2000);
            e.HasIndex(x => new { x.TenantId, x.CreatedAtUtc });
        });
    }
}

using System.Reflection;
using ClothingStore.Core.Common;
using ClothingStore.Core.Entities;
using ClothingStore.Core.Tenancy;
using ClothingStore.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ClothingStore.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenantProvider)
    : IdentityDbContext<ApplicationUser, ApplicationRole, int>(options)
{
    // Platform tables (no tenant filter)
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();

    // Business tables (filtered by TenantId)
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentTrackingLog> ShipmentTrackingLogs => Set<ShipmentTrackingLog>();
    public DbSet<ReturnRequest> Returns => Set<ReturnRequest>();
    public DbSet<Coupon> Coupons => Set<Coupon>();

    /// <summary>Read by the global query filters on every query (EF parameterizes it).</summary>
    public int? CurrentTenantId => tenantProvider.TenantId;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();

        // Enums as readable strings (OrderStatus = "Shipped" instead of 3)
        var enumTypes = typeof(ITenantEntity).Assembly.GetTypes().Where(t => t.IsEnum);
        foreach (var enumType in enumTypes)
            configurationBuilder.Properties(enumType).HaveConversion<string>().HaveMaxLength(30);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Business FKs default to Restrict (SQL Server rejects multiple cascade paths).
        // Configurations opt in to Cascade explicitly for owned children (OrderItems, Images ...).
        foreach (var fk in builder.Model.GetEntityTypes()
                     .Where(t => t.ClrType.Namespace == typeof(Product).Namespace)
                     .SelectMany(t => t.GetForeignKeys()))
        {
            if (((IConventionForeignKey)fk).GetDeleteBehaviorConfigurationSource() != ConfigurationSource.Explicit)
                fk.DeleteBehavior = DeleteBehavior.Restrict;
        }

        // Every ITenantEntity: FK to Tenants + global query filter
        var applyFilter = typeof(AppDbContext).GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var entityType in builder.Model.GetEntityTypes()
                     .Where(t => typeof(ITenantEntity).IsAssignableFrom(t.ClrType))
                     .ToList())
        {
            builder.Entity(entityType.ClrType)
                .HasOne(typeof(Tenant))
                .WithMany()
                .HasForeignKey(nameof(ITenantEntity.TenantId))
                .OnDelete(DeleteBehavior.Restrict);

            applyFilter.MakeGenericMethod(entityType.ClrType).Invoke(this, [builder]);
        }

        // Users: same tenant filter (TenantId null == SuperAdmin, visible only when no tenant is set)
        builder.Entity<ApplicationUser>().HasQueryFilter(u => u.TenantId == CurrentTenantId);
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder builder) where TEntity : class, ITenantEntity =>
        builder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTenantAndAuditRules();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyTenantAndAuditRules();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyTenantAndAuditRules()
    {
        var now = DateTime.UtcNow;
        var currentTenantId = CurrentTenantId;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is BaseEntity audited)
            {
                if (entry.State == EntityState.Added && audited.CreatedAt == default)
                    audited.CreatedAt = now;
                else if (entry.State == EntityState.Modified)
                    audited.UpdatedAt = now;
            }

            if (entry.Entity is not ITenantEntity tenantEntity ||
                entry.State is EntityState.Detached or EntityState.Unchanged)
                continue;

            if (currentTenantId is null)
                throw new TenantMismatchException(
                    $"Cannot save {entry.Entity.GetType().Name}: no current tenant is set.");

            switch (entry.State)
            {
                case EntityState.Added:
                    if (tenantEntity.TenantId == 0)
                        tenantEntity.TenantId = currentTenantId.Value;
                    else if (tenantEntity.TenantId != currentTenantId)
                        throw new TenantMismatchException(
                            $"Cannot add {entry.Entity.GetType().Name} for another tenant.");
                    break;

                case EntityState.Modified:
                case EntityState.Deleted:
                    var originalTenantId = (int)entry.OriginalValues[nameof(ITenantEntity.TenantId)]!;
                    if (originalTenantId != currentTenantId || tenantEntity.TenantId != currentTenantId)
                        throw new TenantMismatchException(
                            $"Cannot change {entry.Entity.GetType().Name} of another tenant.");
                    break;
            }
        }
    }
}

public sealed class TenantMismatchException(string message) : InvalidOperationException(message);

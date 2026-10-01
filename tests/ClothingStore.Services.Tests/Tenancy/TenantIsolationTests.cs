using ClothingStore.Core.Entities;
using ClothingStore.Core.Tenancy;
using ClothingStore.Infrastructure;
using ClothingStore.Infrastructure.Data;
using ClothingStore.Infrastructure.Identity;
using ClothingStore.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Services.Tests.Tenancy;

/// <summary>
/// Integration tests against a throw-away LocalDB database (created from the real migrations).
/// Tenant A must never see or change Tenant B data.
/// </summary>
public sealed class TenantIsolationTests : IAsyncLifetime
{
    private readonly string _connectionString =
        $@"Server=(localdb)\MSSQLLocalDB;Database=ClothingStore_Tests_{Guid.NewGuid():N};Trusted_Connection=True;TrustServerCertificate=True";

    private Guid _tenantA;
    private Guid _tenantB;

    public async Task InitializeAsync()
    {
        await using var db = CreateContext(null);
        await db.Database.MigrateAsync();

        var a = new Tenant { Name = "Store A", Slug = "store-a", Settings = new TenantSettings() };
        var b = new Tenant { Name = "Store B", Slug = "store-b", Settings = new TenantSettings() };
        db.Tenants.AddRange(a, b);
        await db.SaveChangesAsync();
        _tenantA = a.Id;
        _tenantB = b.Id;

        await using (var dbA = CreateContext(_tenantA))
        {
            dbA.Categories.Add(new Category { Name = "Girls Wear", Slug = "girls-wear" });
            dbA.Users.Add(NewUser(_tenantA, "same@example.com"));
            await dbA.SaveChangesAsync();
        }

        await using (var dbB = CreateContext(_tenantB))
        {
            dbB.Categories.Add(new Category { Name = "Boys Wear", Slug = "girls-wear" }); // same slug is fine in another tenant
            dbB.Users.Add(NewUser(_tenantB, "same@example.com"));                      // same email is fine in another tenant
            await dbB.SaveChangesAsync();
        }
    }

    public async Task DisposeAsync()
    {
        await using var db = CreateContext(null);
        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task Queries_return_only_current_tenant_rows()
    {
        await using var dbA = CreateContext(_tenantA);
        var categories = await dbA.Categories.ToListAsync();

        var category = Assert.Single(categories);
        Assert.Equal("Girls Wear", category.Name);
        Assert.Equal(_tenantA, category.TenantId);
    }

    [Fact]
    public async Task Find_by_id_of_other_tenant_row_returns_null()
    {
        Guid tenantBCategoryId;
        await using (var dbB = CreateContext(_tenantB))
            tenantBCategoryId = (await dbB.Categories.SingleAsync()).Id;

        await using var dbA = CreateContext(_tenantA);
        Assert.Null(await dbA.Categories.FirstOrDefaultAsync(c => c.Id == tenantBCategoryId));
    }

    [Fact]
    public async Task No_tenant_sees_no_business_rows()
    {
        await using var db = CreateContext(null);
        Assert.Empty(await db.Categories.ToListAsync());
    }

    [Fact]
    public async Task Users_are_filtered_by_tenant()
    {
        await using var dbA = CreateContext(_tenantA);
        var user = Assert.Single(await dbA.Users.Where(u => u.NormalizedEmail == "SAME@EXAMPLE.COM").ToListAsync());
        Assert.Equal(_tenantA, user.TenantId);
    }

    [Fact]
    public async Task New_entity_gets_current_tenant_automatically()
    {
        await using var dbA = CreateContext(_tenantA);
        var supplier = new Supplier { Name = "Surat Textiles" };
        dbA.Suppliers.Add(supplier);
        await dbA.SaveChangesAsync();

        Assert.Equal(_tenantA, supplier.TenantId);
    }

    [Fact]
    public async Task Adding_entity_for_another_tenant_throws()
    {
        await using var dbA = CreateContext(_tenantA);
        dbA.Suppliers.Add(new Supplier { Name = "Sneaky", TenantId = _tenantB });

        await Assert.ThrowsAsync<TenantMismatchException>(() => dbA.SaveChangesAsync());
    }

    [Fact]
    public async Task Moving_entity_to_another_tenant_throws()
    {
        await using var dbA = CreateContext(_tenantA);
        var category = await dbA.Categories.SingleAsync();
        category.TenantId = _tenantB;

        await Assert.ThrowsAsync<TenantMismatchException>(() => dbA.SaveChangesAsync());
    }

    [Fact]
    public async Task Saving_business_data_without_tenant_throws()
    {
        await using var db = CreateContext(null);
        db.Suppliers.Add(new Supplier { Name = "No tenant" });

        await Assert.ThrowsAsync<TenantMismatchException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Same_slug_twice_in_one_tenant_is_rejected_by_unique_index()
    {
        await using var dbA = CreateContext(_tenantA);
        dbA.Categories.Add(new Category { Name = "Duplicate", Slug = "girls-wear" });

        await Assert.ThrowsAsync<DbUpdateException>(() => dbA.SaveChangesAsync());
    }

    private AppDbContext CreateContext(Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        Infrastructure.DependencyInjection.ConfigureDbContext(options, _connectionString);

        var tenantProvider = new TenantProvider();
        tenantProvider.SetTenant(tenantId);
        return new AppDbContext(options.Options, tenantProvider);
    }

    private static ApplicationUser NewUser(Guid tenantId, string email) => new()
    {
        TenantId = tenantId,
        UserName = email,
        NormalizedUserName = email.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        FullName = "Test User",
        SecurityStamp = Guid.NewGuid().ToString(),
        CreatedAt = DateTime.UtcNow,
    };
}

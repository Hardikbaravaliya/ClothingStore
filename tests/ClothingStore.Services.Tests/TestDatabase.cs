using ClothingStore.Core.Tenancy;
using ClothingStore.Infrastructure.Data;
using ClothingStore.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Services.Tests;

/// <summary>Throw-away LocalDB database built from the real migrations. One per test class instance.</summary>
public sealed class TestDatabase : IAsyncDisposable
{
    private readonly string _connectionString =
        $@"Server=(localdb)\MSSQLLocalDB;Database=ClothingStore_Tests_{Guid.NewGuid():N};Trusted_Connection=True;TrustServerCertificate=True";

    public async Task MigrateAsync()
    {
        await using var db = CreateContext(null);
        await db.Database.MigrateAsync();
    }

    /// <summary>Creates a tenant (platform table) with default settings and returns its id.</summary>
    public async Task<int> AddTenantAsync(string slug)
    {
        await using var db = CreateContext(null);
        var tenant = new Tenant { Name = slug, Slug = slug, Settings = new TenantSettings() };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant.Id;
    }

    public AppDbContext CreateContext(int? tenantId) => CreateContext(tenantId, out _);

    public AppDbContext CreateContext(int? tenantId, out ITenantProvider tenantProvider)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        Infrastructure.DependencyInjection.ConfigureDbContext(options, _connectionString);

        var provider = new TenantProvider();
        provider.SetTenant(tenantId);
        tenantProvider = provider;
        return new AppDbContext(options.Options, provider);
    }

    public async ValueTask DisposeAsync()
    {
        await using var db = CreateContext(null);
        await db.Database.EnsureDeletedAsync();
    }
}

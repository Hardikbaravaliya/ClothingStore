using ClothingStore.Core.Tenancy;
using ClothingStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ClothingStore.Infrastructure.Tenancy;

public sealed class TenancyOptions
{
    public const string SectionName = "Tenancy";

    /// <summary>Domains whose first label is the tenant slug, e.g. "localhost" => shop1.localhost.</summary>
    public string[] RootDomains { get; set; } = ["localhost"];
}

public interface ITenantResolver
{
    /// <summary>
    /// key = a slug ("shop1") or a host ("shop1.localhost", "shop1.domain.com", "littlestars.in").
    /// </summary>
    Task<TenantInfo?> ResolveAsync(string? key, CancellationToken ct = default);

    Task<TenantInfo?> GetByIdAsync(int tenantId, CancellationToken ct = default);
}

public sealed class TenantResolver(AppDbContext db, IMemoryCache cache, IOptions<TenancyOptions> options) : ITenantResolver
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<TenantInfo?> ResolveAsync(string? key, CancellationToken ct = default)
    {
        key = key?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(key))
            return null;

        var colon = key.IndexOf(':');
        if (colon >= 0)
            key = key[..colon]; // strip port

        if (!key.Contains('.'))
            return await FindAsync("slug:" + key, t => t.Slug == key, ct);

        foreach (var root in options.Value.RootDomains)
        {
            var rootDomain = root.ToLowerInvariant();
            if (key == rootDomain || key == "www." + rootDomain)
                return null;

            if (key.EndsWith("." + rootDomain, StringComparison.Ordinal))
            {
                var sub = key[..^(rootDomain.Length + 1)];
                var slug = sub.Split('.')[^1]; // "www.shop1.domain.com" => "shop1"
                return await FindAsync("slug:" + slug, t => t.Slug == slug, ct);
            }
        }

        var domain = key.StartsWith("www.", StringComparison.Ordinal) ? key[4..] : key;
        return await FindAsync("domain:" + domain, t => t.CustomDomain == domain, ct);
    }

    public Task<TenantInfo?> GetByIdAsync(int tenantId, CancellationToken ct = default) =>
        FindAsync("id:" + tenantId, t => t.Id == tenantId, ct);

    private async Task<TenantInfo?> FindAsync(string cacheKey, System.Linq.Expressions.Expression<Func<Tenant, bool>> predicate, CancellationToken ct)
    {
        cacheKey = "tenant:resolve:" + cacheKey;
        if (cache.TryGetValue(cacheKey, out TenantInfo? cached))
            return cached;

        var tenant = await db.Tenants.AsNoTracking()
            .Where(predicate)
            .Select(t => new TenantInfo(t.Id, t.Slug, t.Name, t.Status))
            .FirstOrDefaultAsync(ct);

        // Cache misses too (shorter), so random subdomains don't hit the DB every request
        cache.Set(cacheKey, tenant, tenant is null ? TimeSpan.FromSeconds(30) : CacheDuration);
        return tenant;
    }
}

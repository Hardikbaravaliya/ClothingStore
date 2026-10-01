using ClothingStore.Core.Tenancy;
using ClothingStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Services.Store;

/// <summary>Public store info for the Catalog (GET /api/store/settings).</summary>
public sealed record StoreSettingsDto(
    string Name,
    string Slug,
    string? LogoPath,
    string? ThemeColor,
    string? ContactEmail,
    string? ContactPhone,
    string CurrencyCode,
    string CurrencySymbol,
    string CultureName,
    string TimeZoneId);

public interface IStoreSettingsService
{
    Task<StoreSettingsDto?> GetCurrentAsync(CancellationToken ct = default);
}

public sealed class StoreSettingsService(AppDbContext db, ITenantProvider tenantProvider) : IStoreSettingsService
{
    public async Task<StoreSettingsDto?> GetCurrentAsync(CancellationToken ct = default)
    {
        if (tenantProvider.TenantId is not { } tenantId)
            return null;

        return await db.TenantSettings.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .Select(s => new StoreSettingsDto(
                s.Tenant.Name, s.Tenant.Slug, s.LogoPath, s.ThemeColor, s.ContactEmail, s.ContactPhone,
                s.CurrencyCode, s.CurrencySymbol, s.CultureName, s.TimeZoneId))
            .FirstOrDefaultAsync(ct);
    }
}

using System.Globalization;
using ClothingStore.Core.Tenancy;
using ClothingStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ClothingStore.Services.Store;

public sealed record TenantLocale(string TimeZoneId, string CurrencyCode, string CurrencySymbol, string CultureName)
{
    public static readonly TenantLocale Default = new("Asia/Kolkata", "INR", "₹", "en-IN");
}

public interface ITenantLocaleAccessor
{
    /// <summary>Current tenant's timezone/currency/culture (Default when no tenant).</summary>
    TenantLocale Current { get; }
}

public sealed class TenantLocaleAccessor(AppDbContext db, ITenantProvider tenantProvider, IMemoryCache cache) : ITenantLocaleAccessor
{
    private TenantLocale? _current;

    public TenantLocale Current => _current ??= Load();

    private TenantLocale Load()
    {
        if (tenantProvider.TenantId is not { } tenantId)
            return TenantLocale.Default;

        // Cached: used on almost every page (prices and dates)
        return cache.GetOrCreate($"tenant:{tenantId}:locale", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            return db.TenantSettings.AsNoTracking()
                .Where(s => s.TenantId == tenantId)
                .Select(s => new TenantLocale(s.TimeZoneId, s.CurrencyCode, s.CurrencySymbol, s.CultureName))
                .FirstOrDefault() ?? TenantLocale.Default;
        })!;
    }
}

/// <summary>DB stores UTC; screens show the tenant's local time. Never use DateTime.Now in views.</summary>
public interface ITenantClock
{
    DateTime UtcNow { get; }
    DateTime LocalNow { get; }
    DateTime ToLocal(DateTime utc);
    DateTime ToUtc(DateTime local);
}

public sealed class TenantClock(ITenantLocaleAccessor locale, TimeProvider timeProvider) : ITenantClock
{
    private TimeZoneInfo TimeZone => TimeZoneInfo.FindSystemTimeZoneById(locale.Current.TimeZoneId);

    public DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;
    public DateTime LocalNow => ToLocal(UtcNow);

    public DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZone);

    public DateTime ToUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZone);
}

public interface IMoneyFormatter
{
    /// <summary>e.g. 123456 => "₹1,23,456.00" for en-IN.</summary>
    string Format(decimal amount);
}

public sealed class MoneyFormatter(ITenantLocaleAccessor locale) : IMoneyFormatter
{
    private NumberFormatInfo? _format;

    public string Format(decimal amount) => amount.ToString("C", _format ??= CreateFormat());

    private NumberFormatInfo CreateFormat()
    {
        var current = locale.Current;
        var format = (NumberFormatInfo)new CultureInfo(current.CultureName).NumberFormat.Clone();
        format.CurrencySymbol = current.CurrencySymbol;
        return format;
    }
}

using System.Globalization;
using ClothingStore.Catalog.ApiClients;
using ClothingStore.Contracts.Store;
using Microsoft.Extensions.Caching.Memory;

namespace ClothingStore.Catalog.Infrastructure;

/// <summary>The store of the current request (from its host) + money/date formatting in the store's locale.</summary>
public sealed class StoreContext
{
    private NumberFormatInfo? _numberFormat;
    private TimeZoneInfo? _timeZone;

    public StoreSettingsDto Store { get; private set; } = default!;

    public void Set(StoreSettingsDto store)
    {
        Store = store;
        _numberFormat = null;
        _timeZone = null;
    }

    public string Money(decimal amount)
    {
        if (_numberFormat is null)
        {
            var format = (NumberFormatInfo)CultureInfo.GetCultureInfo(Store.CultureName).NumberFormat.Clone();
            format.CurrencySymbol = Store.CurrencySymbol;
            _numberFormat = format;
        }
        return amount.ToString("C", _numberFormat);
    }

    /// <summary>Api dates are UTC; show them in the store's time zone.</summary>
    public DateTime ToLocal(DateTime utc)
    {
        _timeZone ??= TimeZoneInfo.TryFindSystemTimeZoneById(Store.TimeZoneId, out var tz) ? tz : TimeZoneInfo.Utc;
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _timeZone);
    }
}

/// <summary>
/// Loads the store for the request host (cached 5 min). Unknown host => "Store not found" page.
/// Must run before UseRouting (it may rewrite the path).
/// </summary>
public sealed class StoreContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, StoreContext storeContext, StorefrontApi api, IMemoryCache cache)
    {
        // Static files and the health of the site do not need a store
        if (Path.HasExtension(context.Request.Path.Value))
        {
            await next(context);
            return;
        }

        var host = context.Request.Host.Host.ToLowerInvariant();
        var store = await cache.GetOrCreateAsync($"store:{host}", async entry =>
        {
            var found = await api.GetStoreAsync(context.RequestAborted);
            entry.AbsoluteExpirationRelativeToNow = found is null ? TimeSpan.FromSeconds(30) : TimeSpan.FromMinutes(5);
            return found;
        });

        if (store is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Request.Path = "/Home/StoreNotFound";
        }
        else
        {
            storeContext.Set(store);
        }

        await next(context);
    }
}

using ClothingStore.Catalog.ApiClients;
using ClothingStore.Contracts.Catalog;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace ClothingStore.Catalog.ViewComponents;

public sealed record StoreNavModel(IReadOnlyList<CategoryDto> Categories, int CartCount);

/// <summary>Category menu (cached per store) + cart item count, for the layout.</summary>
public class StoreNavViewComponent(StorefrontApi api, IMemoryCache cache) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var host = HttpContext.Request.Host.Host.ToLowerInvariant();
        var categories = await cache.GetOrCreateAsync($"categories:{host}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return api.GetCategoriesAsync(HttpContext.RequestAborted);
        }) ?? [];

        int cartCount;
        try
        {
            cartCount = (await api.GetCartAsync(HttpContext.RequestAborted)).ItemCount;
        }
        catch (ApiUnauthorizedException)
        {
            cartCount = 0; // expired token: the next real action will send the customer to login
        }

        return View(new StoreNavModel(categories, cartCount));
    }
}

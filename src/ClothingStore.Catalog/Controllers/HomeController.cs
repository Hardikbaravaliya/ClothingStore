using System.Diagnostics;
using ClothingStore.Catalog.ApiClients;
using ClothingStore.Catalog.Models;
using ClothingStore.Contracts.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace ClothingStore.Catalog.Controllers;

public class HomeController(StorefrontApi api) : Controller
{
    [Route("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var categories = api.GetCategoriesAsync(ct);
        var featured = api.SearchProductsAsync(new ProductSearchRequest { FeaturedOnly = true, PageSize = 8 }, ct);
        var latest = api.SearchProductsAsync(new ProductSearchRequest { PageSize = 8 }, ct);
        await Task.WhenAll(categories, featured, latest);

        return View(new HomeViewModel
        {
            Categories = categories.Result,
            Featured = featured.Result.Items,
            Latest = latest.Result.Items,
        });
    }

    public IActionResult StoreNotFound() => View();

    public IActionResult PageNotFound() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

using ClothingStore.Catalog.ApiClients;
using ClothingStore.Catalog.Models;
using ClothingStore.Contracts.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace ClothingStore.Catalog.Controllers;

public class ShopController(StorefrontApi api) : Controller
{
    /// <summary>/c/girls-wear?sizes=2-3Y&amp;colors=Red&amp;sort=price_asc</summary>
    [Route("c/{slug}")]
    public async Task<IActionResult> Category(string slug, [FromQuery] ProductSearchRequest request, CancellationToken ct)
    {
        var categories = await api.GetCategoriesAsync(ct);
        var category = categories.SelectMany(c => c.Children.Prepend(c)).FirstOrDefault(c => c.Slug == slug);
        if (category is null)
            return NotFound();

        request.Category = slug;
        return View("List", await BuildListAsync(category.Name, category, request, ct));
    }

    [Route("search")]
    public async Task<IActionResult> Search(string? q, [FromQuery] ProductSearchRequest request, CancellationToken ct)
    {
        request.Search = q ?? request.Search;
        var title = string.IsNullOrWhiteSpace(request.Search) ? "All products" : $"Results for \"{request.Search}\"";
        return View("List", await BuildListAsync(title, null, request, ct));
    }

    [Route("p/{slug}")]
    public async Task<IActionResult> Product(string slug, CancellationToken ct)
    {
        var product = await api.GetProductAsync(slug, ct);
        return product is null ? NotFound() : View(product);
    }

    private async Task<ProductListViewModel> BuildListAsync(string title, CategoryDto? category, ProductSearchRequest request, CancellationToken ct)
    {
        request.PageSize = 24;
        var results = api.SearchProductsAsync(request, ct);
        var filters = api.GetFiltersAsync(request.Category, ct);
        await Task.WhenAll(results, filters);

        return new ProductListViewModel
        {
            Title = title,
            Category = category,
            Request = request,
            Results = results.Result,
            Filters = filters.Result,
        };
    }
}

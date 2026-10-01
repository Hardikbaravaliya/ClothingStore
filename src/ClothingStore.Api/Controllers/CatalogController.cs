using ClothingStore.Contracts;
using ClothingStore.Contracts.Catalog;
using ClothingStore.Services.Storefront;
using Microsoft.AspNetCore.Mvc;

namespace ClothingStore.Api.Controllers;

[ApiController]
[Route("api")]
public class CatalogController(IStorefrontCatalogService catalog) : ControllerBase
{
    [HttpGet("categories")]
    public async Task<IReadOnlyList<CategoryDto>> GetCategories(CancellationToken ct) =>
        await catalog.GetCategoriesAsync(ct);

    /// <summary>?category=frocks&amp;sizes=2-3Y&amp;colors=Red&amp;minPrice=..&amp;maxPrice=..&amp;search=..&amp;sort=price_asc&amp;page=1</summary>
    [HttpGet("products")]
    public async Task<PagedResult<ProductSummaryDto>> Search([FromQuery] ProductSearchRequest request, CancellationToken ct) =>
        await catalog.SearchAsync(request, ct);

    [HttpGet("products/filters")]
    public async Task<ProductFiltersDto> GetFilters([FromQuery] string? category, CancellationToken ct) =>
        await catalog.GetFiltersAsync(category, ct);

    [HttpGet("products/{slug}")]
    [ProducesResponseType<ProductDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProduct(string slug, CancellationToken ct)
    {
        var product = await catalog.GetProductAsync(slug, ct);
        return product is null ? NotFound() : Ok(product);
    }
}

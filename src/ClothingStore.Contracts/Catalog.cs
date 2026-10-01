namespace ClothingStore.Contracts.Catalog;

public sealed record CategoryDto(int Id, string Name, string Slug, string? Description, IReadOnlyList<CategoryDto> Children);

public sealed record ProductSummaryDto(
    int Id,
    string Name,
    string Slug,
    string CategoryName,
    decimal Price,
    decimal Mrp,
    string? ImageUrl,
    bool InStock);

/// <summary>Query string of GET /api/products.</summary>
public sealed class ProductSearchRequest
{
    /// <summary>Category slug; includes its sub-categories.</summary>
    public string? Category { get; set; }
    public string? Search { get; set; }
    public List<string> Sizes { get; set; } = [];
    public List<string> Colors { get; set; } = [];
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }

    /// <summary>newest (default), price_asc, price_desc, name.</summary>
    public string? Sort { get; set; }

    public bool FeaturedOnly { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 24;
}

/// <summary>What can be filtered in a category (or the whole store).</summary>
public sealed record ProductFiltersDto(IReadOnlyList<string> Sizes, IReadOnlyList<string> Colors, decimal MinPrice, decimal MaxPrice);

public sealed record ProductVariantDto(
    int Id,
    string Size,
    string Color,
    decimal Price,
    decimal Mrp,
    int AvailableQuantity) // 0..10; the real stock is not exposed
{
    public bool InStock => AvailableQuantity > 0;
}

public sealed record ProductDetailDto(
    int Id,
    string Name,
    string Slug,
    string? Description,
    string? Brand,
    string? Fabric,
    string? AgeGroup,
    string CategoryName,
    string CategorySlug,
    decimal Price,
    decimal Mrp,
    string? MetaTitle,
    string? MetaDescription,
    IReadOnlyList<string> ImageUrls,
    IReadOnlyList<ProductVariantDto> Variants);

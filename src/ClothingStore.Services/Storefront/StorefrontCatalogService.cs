using ClothingStore.Contracts;
using ClothingStore.Contracts.Catalog;
using ClothingStore.Core.Common;
using ClothingStore.Core.Entities;
using ClothingStore.Core.Interfaces;
using ClothingStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Services.Storefront;

/// <summary>Read-only catalog for customers: only active categories/products/variants.</summary>
public interface IStorefrontCatalogService
{
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default);
    Task<PagedResult<ProductSummaryDto>> SearchAsync(ProductSearchRequest request, CancellationToken ct = default);
    Task<ProductFiltersDto> GetFiltersAsync(string? categorySlug, CancellationToken ct = default);
    Task<ProductDetailDto?> GetProductAsync(string slug, CancellationToken ct = default);
}

public sealed class StorefrontCatalogService(AppDbContext db, IImageStorage imageStorage) : IStorefrontCatalogService
{
    /// <summary>Customers see "only N left" at most this number; real stock stays private.</summary>
    public const int MaxVisibleQuantity = 10;

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default)
    {
        var all = await db.Categories.AsNoTracking()
            .Where(c => c.IsActive && (c.Parent == null || c.Parent.IsActive))
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.Slug, c.Description, c.ParentId })
            .ToListAsync(ct);

        var children = all.Where(c => c.ParentId is not null).ToLookup(c => c.ParentId);
        return all.Where(c => c.ParentId is null)
            .Select(p => new CategoryDto(p.Id, p.Name, p.Slug, p.Description,
                children[p.Id].Select(c => new CategoryDto(c.Id, c.Name, c.Slug, c.Description, [])).ToList()))
            .ToList();
    }

    public async Task<PagedResult<ProductSummaryDto>> SearchAsync(ProductSearchRequest request, CancellationToken ct = default)
    {
        var (page, pageSize) = Paging.Normalize(request.Page, request.PageSize);
        var sizes = request.Sizes.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct().ToList();
        var colors = request.Colors.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct().ToList();

        var products = VisibleProducts();

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var slug = request.Category.Trim();
            products = products.Where(p => p.Category.Slug == slug || (p.Category.Parent != null && p.Category.Parent.Slug == slug));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            products = products.Where(p => p.Name.Contains(term) || (p.Brand != null && p.Brand.Contains(term)) || p.Category.Name.Contains(term));
        }

        if (request.FeaturedOnly)
            products = products.Where(p => p.IsFeatured);

        // Size, color and price must match on the same variant
        if (sizes.Count > 0 || colors.Count > 0 || request.MinPrice is not null || request.MaxPrice is not null)
        {
            products = products.Where(p => p.Variants.Any(v => v.IsActive
                && (sizes.Count == 0 || sizes.Contains(v.Size))
                && (colors.Count == 0 || colors.Contains(v.Color))
                && (request.MinPrice == null || v.SellingPrice >= request.MinPrice)
                && (request.MaxPrice == null || v.SellingPrice <= request.MaxPrice)));
        }

        var rows = products.Select(p => new
        {
            p.Id,
            p.Name,
            p.Slug,
            CategoryName = p.Category.Name,
            Price = p.Variants.Where(v => v.IsActive).Min(v => v.SellingPrice),
            p.Mrp,
            p.CreatedAt,
            ImagePath = p.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).Select(i => i.Path).FirstOrDefault(),
            InStock = p.Variants.Any(v => v.IsActive && v.StockQty > 0),
        });

        rows = request.Sort switch
        {
            "price_asc" => rows.OrderBy(r => r.Price).ThenBy(r => r.Name),
            "price_desc" => rows.OrderByDescending(r => r.Price).ThenBy(r => r.Name),
            "name" => rows.OrderBy(r => r.Name),
            _ => rows.OrderByDescending(r => r.InStock).ThenByDescending(r => r.CreatedAt),
        };

        var total = await rows.CountAsync(ct);
        var items = await rows.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<ProductSummaryDto>(
            items.Select(r => new ProductSummaryDto(r.Id, r.Name, r.Slug, r.CategoryName, r.Price, r.Mrp,
                r.ImagePath is null ? null : imageStorage.GetUrl(r.ImagePath), r.InStock)).ToList(),
            page, pageSize, total);
    }

    public async Task<ProductFiltersDto> GetFiltersAsync(string? categorySlug, CancellationToken ct = default)
    {
        var products = VisibleProducts();
        if (!string.IsNullOrWhiteSpace(categorySlug))
        {
            var slug = categorySlug.Trim();
            products = products.Where(p => p.Category.Slug == slug || (p.Category.Parent != null && p.Category.Parent.Slug == slug));
        }

        var variants = await products.SelectMany(p => p.Variants.Where(v => v.IsActive))
            .Select(v => new { v.Size, v.Color, v.SellingPrice })
            .Distinct()
            .ToListAsync(ct);

        if (variants.Count == 0)
            return new ProductFiltersDto([], [], 0, 0);

        return new ProductFiltersDto(
            variants.Select(v => v.Size).Distinct(StringComparer.OrdinalIgnoreCase).Order(SizeComparer.Instance).ToList(),
            variants.Select(v => v.Color).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList(),
            variants.Min(v => v.SellingPrice),
            variants.Max(v => v.SellingPrice));
    }

    public async Task<ProductDetailDto?> GetProductAsync(string slug, CancellationToken ct = default)
    {
        var product = await VisibleProducts()
            .AsSplitQuery()
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.Variants.Where(v => v.IsActive))
            .FirstOrDefaultAsync(p => p.Slug == slug, ct);
        if (product is null)
            return null;

        var variants = product.Variants
            .OrderBy(v => v.Size, SizeComparer.Instance).ThenBy(v => v.Color)
            .Select(v => new ProductVariantDto(v.Id, v.Size, v.Color, v.SellingPrice, v.Mrp ?? product.Mrp,
                Math.Clamp(v.StockQty, 0, MaxVisibleQuantity)))
            .ToList();

        var images = product.Images
            .OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder)
            .Select(i => imageStorage.GetUrl(i.Path))
            .ToList();

        return new ProductDetailDto(product.Id, product.Name, product.Slug, product.Description, product.Brand, product.Fabric,
            product.AgeGroup, product.Category.Name, product.Category.Slug, variants.Min(v => v.Price), product.Mrp,
            product.MetaTitle, product.MetaDescription, images, variants);
    }

    /// <summary>Active product, active category (and parent), at least one active variant.</summary>
    private IQueryable<Product> VisibleProducts() =>
        db.Products.AsNoTracking().Where(p => p.IsActive
            && p.Category.IsActive
            && (p.Category.Parent == null || p.Category.Parent.IsActive)
            && p.Variants.Any(v => v.IsActive));
}

/// <summary>Orders kids sizes naturally: "2-3Y" before "10-11Y", "S" before "M" before "L".</summary>
public sealed class SizeComparer : IComparer<string>
{
    public static readonly SizeComparer Instance = new();
    private static readonly string[] Letters = ["XXS", "XS", "S", "M", "L", "XL", "XXL", "XXXL"];

    public int Compare(string? x, string? y)
    {
        if (x is null || y is null)
            return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);

        var (xNum, xHasNum) = LeadingNumber(x);
        var (yNum, yHasNum) = LeadingNumber(y);
        if (xHasNum && yHasNum && xNum != yNum)
            return xNum.CompareTo(yNum);

        var xi = Array.IndexOf(Letters, x.ToUpperInvariant());
        var yi = Array.IndexOf(Letters, y.ToUpperInvariant());
        if (xi >= 0 && yi >= 0)
            return xi.CompareTo(yi);

        return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
    }

    private static (int Value, bool Found) LeadingNumber(string s)
    {
        var digits = new string(s.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var n) ? (n, true) : (0, false);
    }
}

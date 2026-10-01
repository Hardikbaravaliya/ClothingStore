using ClothingStore.Core.Common;
using ClothingStore.Core.Entities;
using ClothingStore.Core.Interfaces;
using ClothingStore.Core.Tenancy;
using ClothingStore.Infrastructure.Data;
using ClothingStore.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Services.Products;

public interface IProductService
{
    Task<PagedList<ProductListItem>> GetPagedAsync(ProductListQuery query, CancellationToken ct = default);
    Task<ProductDetail?> GetDetailAsync(int id, CancellationToken ct = default);
    Task<Result<int>> CreateAsync(ProductForm form, CancellationToken ct = default);
    Task<Result> UpdateAsync(ProductForm form, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);

    /// <returns>Number of variants created (existing Size/Color pairs are skipped).</returns>
    Task<Result<int>> AddVariantsAsync(VariantBulkForm form, CancellationToken ct = default);
    Task<VariantForm?> GetVariantFormAsync(int variantId, CancellationToken ct = default);
    Task<Result> UpdateVariantAsync(VariantForm form, CancellationToken ct = default);
    Task<Result> DeleteVariantAsync(int variantId, CancellationToken ct = default);

    Task<Result<int>> AddImagesAsync(int productId, IReadOnlyList<ImageUpload> files, CancellationToken ct = default);
    Task<Result> SetPrimaryImageAsync(int imageId, CancellationToken ct = default);
    Task<Result> DeleteImageAsync(int imageId, CancellationToken ct = default);

    Task<IReadOnlyList<VariantOption>> GetVariantOptionsAsync(CancellationToken ct = default);
}

public sealed class ProductService(AppDbContext db, IImageStorage imageStorage, ITenantProvider tenantProvider) : IProductService
{
    public const long MaxImageBytes = 5 * 1024 * 1024;
    public const int MaxImagesPerProduct = 10;

    public async Task<PagedList<ProductListItem>> GetPagedAsync(ProductListQuery query, CancellationToken ct = default)
    {
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
        var products = db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            products = products.Where(p => p.Name.Contains(term) || p.Variants.Any(v => v.Sku.Contains(term)));
        }

        if (query.CategoryId is { } categoryId)
            products = products.Where(p => p.CategoryId == categoryId || p.Category.ParentId == categoryId);

        var total = await products.CountAsync(ct);
        var rows = await products
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.Name,
                CategoryName = p.Category.Name,
                p.Mrp,
                p.SellingPrice,
                VariantCount = p.Variants.Count,
                TotalStock = p.Variants.Sum(v => (int?)v.StockQty) ?? 0,
                p.IsActive,
                ImagePath = p.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).Select(i => i.Path).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new ProductListItem(
                r.Id, r.Name, r.CategoryName, r.Mrp, r.SellingPrice, r.VariantCount, r.TotalStock, r.IsActive,
                r.ImagePath is null ? null : imageStorage.GetUrl(r.ImagePath)))
            .ToList();
        return new PagedList<ProductListItem>(items, page, pageSize, total);
    }

    public async Task<ProductDetail?> GetDetailAsync(int id, CancellationToken ct = default)
    {
        var product = await db.Products.AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
            return null;

        var form = new ProductForm
        {
            Id = product.Id,
            Name = product.Name,
            Slug = product.Slug,
            Description = product.Description,
            CategoryId = product.CategoryId,
            Brand = product.Brand,
            Fabric = product.Fabric,
            AgeGroup = product.AgeGroup,
            Mrp = product.Mrp,
            SellingPrice = product.SellingPrice,
            IsActive = product.IsActive,
            IsFeatured = product.IsFeatured,
            MetaTitle = product.MetaTitle,
            MetaDescription = product.MetaDescription,
        };

        var variants = product.Variants
            .OrderBy(v => v.Size).ThenBy(v => v.Color)
            .Select(v => new VariantItem(v.Id, v.Size, v.Color, v.Sku, v.Mrp, v.SellingPrice, v.StockQty,
                v.AvgCostPrice, v.LowStockThreshold, v.IsActive))
            .ToList();

        var images = product.Images
            .OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder)
            .Select(i => new ProductImageItem(i.Id, imageStorage.GetUrl(i.Path), i.IsPrimary, i.SortOrder))
            .ToList();

        return new ProductDetail(form, product.Category.Name, variants, images);
    }

    public async Task<Result<int>> CreateAsync(ProductForm form, CancellationToken ct = default)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == form.CategoryId, ct))
            return Result<int>.Fail("Category not found.");

        var product = new Product();
        await ApplyAsync(product, form, ct);
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(product.Id);
    }

    public async Task<Result> UpdateAsync(ProductForm form, CancellationToken ct = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == form.Id, ct);
        if (product is null)
            return Result.Fail("Product not found.");
        if (!await db.Categories.AnyAsync(c => c.Id == form.CategoryId, ct))
            return Result.Fail("Category not found.");

        await ApplyAsync(product, form, ct);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var product = await db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
            return Result.Fail("Product not found.");

        var variantIds = await db.ProductVariants.Where(v => v.ProductId == id).Select(v => v.Id).ToListAsync(ct);
        if (await AnyVariantInUseAsync(variantIds, ct))
            return Result.Fail("This product has stock history or orders, so it cannot be deleted. Mark it inactive instead.");

        var imagePaths = product.Images.Select(i => i.Path).ToList();
        db.Products.Remove(product); // variants and images cascade
        await db.SaveChangesAsync(ct);

        foreach (var path in imagePaths)
            await imageStorage.DeleteAsync(path, ct);
        return Result.Ok();
    }

    public async Task<Result<int>> AddVariantsAsync(VariantBulkForm form, CancellationToken ct = default)
    {
        var product = await db.Products.Include(p => p.Variants).FirstOrDefaultAsync(p => p.Id == form.ProductId, ct);
        if (product is null)
            return Result<int>.Fail("Product not found.");

        var sizes = SplitList(form.Sizes);
        var colors = SplitList(form.Colors);
        if (sizes.Any(s => s.Length > 30) || colors.Any(c => c.Length > 50))
            return Result<int>.Fail("A size can be at most 30 characters and a color at most 50.");
        if (sizes.Count == 0 || colors.Count == 0)
            return Result<int>.Fail("Enter at least one size and one color.");

        var created = new List<ProductVariant>();
        foreach (var size in sizes)
        foreach (var color in colors)
        {
            if (product.Variants.Any(v => v.Size.Equals(size, StringComparison.OrdinalIgnoreCase)
                                          && v.Color.Equals(color, StringComparison.OrdinalIgnoreCase)))
                continue;

            var variant = new ProductVariant
            {
                Size = size,
                Color = color,
                Sku = await UniqueSkuAsync($"{product.Id}-{size}-{color}", created, ct),
                Mrp = form.Mrp,
                SellingPrice = form.SellingPrice ?? product.SellingPrice,
                LowStockThreshold = form.LowStockThreshold,
            };
            product.Variants.Add(variant);
            created.Add(variant);
        }

        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(created.Count);
    }

    public Task<VariantForm?> GetVariantFormAsync(int variantId, CancellationToken ct = default) =>
        db.ProductVariants.AsNoTracking()
            .Where(v => v.Id == variantId)
            .Select(v => new VariantForm
            {
                Id = v.Id,
                ProductId = v.ProductId,
                Size = v.Size,
                Color = v.Color,
                Sku = v.Sku,
                Mrp = v.Mrp,
                SellingPrice = v.SellingPrice,
                LowStockThreshold = v.LowStockThreshold,
                IsActive = v.IsActive,
            })
            .FirstOrDefaultAsync(ct);

    public async Task<Result> UpdateVariantAsync(VariantForm form, CancellationToken ct = default)
    {
        var variant = await db.ProductVariants.FirstOrDefaultAsync(v => v.Id == form.Id, ct);
        if (variant is null)
            return Result.Fail("Variant not found.");

        var size = form.Size.Trim();
        var color = form.Color.Trim();
        var sku = NormalizeSku(form.Sku);

        if (await db.ProductVariants.AnyAsync(v => v.Sku == sku && v.Id != variant.Id, ct))
            return Result.Fail($"SKU {sku} is already used by another variant.");
        if (await db.ProductVariants.AnyAsync(v => v.ProductId == variant.ProductId && v.Size == size && v.Color == color && v.Id != variant.Id, ct))
            return Result.Fail($"This product already has a {size} / {color} variant.");

        // StockQty and AvgCostPrice are never edited here: only purchases and stock adjustments change them
        variant.Size = size;
        variant.Color = color;
        variant.Sku = sku;
        variant.Mrp = form.Mrp;
        variant.SellingPrice = form.SellingPrice;
        variant.LowStockThreshold = form.LowStockThreshold;
        variant.IsActive = form.IsActive;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteVariantAsync(int variantId, CancellationToken ct = default)
    {
        var variant = await db.ProductVariants.FirstOrDefaultAsync(v => v.Id == variantId, ct);
        if (variant is null)
            return Result.Fail("Variant not found.");
        if (await AnyVariantInUseAsync([variantId], ct))
            return Result.Fail("This variant has stock history or orders, so it cannot be deleted. Mark it inactive instead.");

        db.ProductVariants.Remove(variant);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result<int>> AddImagesAsync(int productId, IReadOnlyList<ImageUpload> files, CancellationToken ct = default)
    {
        var product = await db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null)
            return Result<int>.Fail("Product not found.");
        if (files.Count == 0)
            return Result<int>.Fail("Choose at least one image.");
        if (product.Images.Count + files.Count > MaxImagesPerProduct)
            return Result<int>.Fail($"A product can have at most {MaxImagesPerProduct} images.");

        // Validate everything before saving anything
        var checkedFiles = new List<(ImageUpload File, string Extension)>();
        foreach (var file in files)
        {
            if (file.Length is 0 or > MaxImageBytes)
                return Result<int>.Fail($"{file.FileName}: images must be between 1 byte and 5 MB.");
            var extension = await DetectImageExtensionAsync(file.Content, ct);
            if (extension is null)
                return Result<int>.Fail($"{file.FileName}: only JPG, PNG and WEBP images are allowed.");
            checkedFiles.Add((file, extension));
        }

        var folder = $"{tenantProvider.TenantId}/{product.Id}";
        var nextSort = product.Images.Count == 0 ? 0 : product.Images.Max(i => i.SortOrder) + 1;
        var savedPaths = new List<string>();
        try
        {
            foreach (var (file, extension) in checkedFiles)
            {
                var path = await imageStorage.SaveAsync(file.Content, folder, extension, ct);
                savedPaths.Add(path);
                product.Images.Add(new ProductImage
                {
                    Path = path,
                    AltText = product.Name,
                    SortOrder = nextSort++,
                    IsPrimary = !product.Images.Any(i => i.IsPrimary),
                });
            }

            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Don't leave orphan files when the DB save fails
            foreach (var path in savedPaths)
                await imageStorage.DeleteAsync(path, CancellationToken.None);
            throw;
        }

        return Result<int>.Ok(checkedFiles.Count);
    }

    public async Task<Result> SetPrimaryImageAsync(int imageId, CancellationToken ct = default)
    {
        var image = await db.ProductImages.FirstOrDefaultAsync(i => i.Id == imageId, ct);
        if (image is null)
            return Result.Fail("Image not found.");

        var siblings = await db.ProductImages.Where(i => i.ProductId == image.ProductId).ToListAsync(ct);
        foreach (var sibling in siblings)
            sibling.IsPrimary = sibling.Id == imageId;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteImageAsync(int imageId, CancellationToken ct = default)
    {
        var image = await db.ProductImages.FirstOrDefaultAsync(i => i.Id == imageId, ct);
        if (image is null)
            return Result.Fail("Image not found.");

        db.ProductImages.Remove(image);
        if (image.IsPrimary)
        {
            var next = await db.ProductImages
                .Where(i => i.ProductId == image.ProductId && i.Id != imageId)
                .OrderBy(i => i.SortOrder)
                .FirstOrDefaultAsync(ct);
            if (next is not null)
                next.IsPrimary = true;
        }

        await db.SaveChangesAsync(ct);
        await imageStorage.DeleteAsync(image.Path, ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<VariantOption>> GetVariantOptionsAsync(CancellationToken ct = default) =>
        await db.ProductVariants.AsNoTracking()
            .Where(v => v.IsActive && v.Product.IsActive)
            .OrderBy(v => v.Product.Name).ThenBy(v => v.Size).ThenBy(v => v.Color)
            .Select(v => new VariantOption(v.Id, v.Product.Name + " – " + v.Size + " / " + v.Color + " (" + v.Sku + ")", v.AvgCostPrice))
            .ToListAsync(ct);

    private async Task ApplyAsync(Product product, ProductForm form, CancellationToken ct)
    {
        product.Name = form.Name.Trim();
        product.Description = form.Description?.Trim();
        product.CategoryId = form.CategoryId!.Value;
        product.Brand = form.Brand?.Trim();
        product.Fabric = form.Fabric?.Trim();
        product.AgeGroup = form.AgeGroup?.Trim();
        product.Mrp = form.Mrp;
        product.SellingPrice = form.SellingPrice;
        product.IsActive = form.IsActive;
        product.IsFeatured = form.IsFeatured;
        product.MetaTitle = form.MetaTitle?.Trim();
        product.MetaDescription = form.MetaDescription?.Trim();

        var baseSlug = SlugHelper.Generate(string.IsNullOrWhiteSpace(form.Slug) ? form.Name : form.Slug, 200);
        if (baseSlug != product.Slug)
            product.Slug = await SlugHelper.MakeUniqueAsync(baseSlug,
                s => db.Products.AnyAsync(p => p.Slug == s && p.Id != product.Id, ct));
    }

    private async Task<bool> AnyVariantInUseAsync(IReadOnlyCollection<int> variantIds, CancellationToken ct) =>
        variantIds.Count > 0 && (
            await db.StockTransactions.AnyAsync(t => variantIds.Contains(t.ProductVariantId), ct)
            || await db.PurchaseItems.AnyAsync(i => variantIds.Contains(i.ProductVariantId), ct)
            || await db.OrderItems.AnyAsync(i => variantIds.Contains(i.ProductVariantId), ct)
            || await db.CartItems.AnyAsync(i => variantIds.Contains(i.ProductVariantId), ct)
            || await db.Returns.AnyAsync(r => r.ExchangeVariantId != null && variantIds.Contains(r.ExchangeVariantId.Value), ct));

    private async Task<string> UniqueSkuAsync(string raw, IReadOnlyCollection<ProductVariant> pending, CancellationToken ct)
    {
        var baseSku = NormalizeSku(raw);
        return await SlugHelper.MakeUniqueAsync(baseSku,
            async s => pending.Any(v => v.Sku == s) || await db.ProductVariants.AnyAsync(v => v.Sku == s, ct));
    }

    /// <summary>"12-2-3y-sky blue" => "12-2-3Y-SKY-BLUE".</summary>
    private static string NormalizeSku(string raw)
    {
        var sku = SlugHelper.Generate(raw, 64).ToUpperInvariant();
        return sku == "ITEM" ? raw.Trim().ToUpperInvariant() : sku;
    }

    private static List<string> SplitList(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Checks the file signature (not just the name). Returns ".jpg"/".png"/".webp" or null.</summary>
    private static async Task<string?> DetectImageExtensionAsync(Stream content, CancellationToken ct)
    {
        var header = new byte[12];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        content.Seek(0, SeekOrigin.Begin);
        if (read < header.Length)
            return null;

        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return ".jpg";
        if (header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return ".png";
        if (header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            return ".webp";
        return null;
    }
}

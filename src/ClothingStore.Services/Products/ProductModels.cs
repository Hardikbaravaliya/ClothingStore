using System.ComponentModel.DataAnnotations;

namespace ClothingStore.Services.Products;

public sealed record ProductListItem(
    int Id,
    string Name,
    string CategoryName,
    decimal Mrp,
    decimal SellingPrice,
    int VariantCount,
    int TotalStock,
    bool IsActive,
    string? PrimaryImageUrl);

public sealed class ProductListQuery
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class ProductForm : IValidatableObject
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = default!;

    /// <summary>Optional; generated from Name when empty.</summary>
    [StringLength(220)]
    public string? Slug { get; set; }

    [StringLength(4000)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Select a category.")]
    [Display(Name = "Category")]
    public int? CategoryId { get; set; }

    [StringLength(100)]
    public string? Brand { get; set; }

    [StringLength(100)]
    public string? Fabric { get; set; }

    [StringLength(50), Display(Name = "Age group")]
    public string? AgeGroup { get; set; }

    [Range(0, 10_000_000), Display(Name = "MRP")]
    public decimal Mrp { get; set; }

    [Range(0.01, 10_000_000), Display(Name = "Selling price")]
    public decimal SellingPrice { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Featured")]
    public bool IsFeatured { get; set; }

    [StringLength(200), Display(Name = "Meta title")]
    public string? MetaTitle { get; set; }

    [StringLength(500), Display(Name = "Meta description")]
    public string? MetaDescription { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Mrp > 0 && SellingPrice > Mrp)
            yield return new ValidationResult("Selling price cannot be more than MRP.", [nameof(SellingPrice)]);
    }
}

public sealed record VariantItem(
    int Id,
    string Size,
    string Color,
    string Sku,
    decimal? Mrp,
    decimal SellingPrice,
    int StockQty,
    decimal AvgCostPrice,
    int LowStockThreshold,
    bool IsActive)
{
    public bool IsLowStock => StockQty <= LowStockThreshold;
}

public sealed record ProductImageItem(int Id, string Url, bool IsPrimary, int SortOrder);

public sealed record ProductDetail(
    ProductForm Form,
    string CategoryName,
    IReadOnlyList<VariantItem> Variants,
    IReadOnlyList<ProductImageItem> Images);

/// <summary>Creates every Size × Color combination at once, e.g. "2-3Y, 3-4Y" × "Red, Blue" = 4 variants.</summary>
public class VariantBulkForm
{
    public int ProductId { get; set; }

    [Required, StringLength(500), Display(Name = "Sizes (comma separated)")]
    public string Sizes { get; set; } = default!;

    [Required, StringLength(500), Display(Name = "Colors (comma separated)")]
    public string Colors { get; set; } = default!;

    /// <summary>Empty = product selling price.</summary>
    [Range(0.01, 10_000_000), Display(Name = "Selling price")]
    public decimal? SellingPrice { get; set; }

    [Range(0, 10_000_000), Display(Name = "MRP")]
    public decimal? Mrp { get; set; }

    [Range(0, 100_000), Display(Name = "Low stock alert at")]
    public int LowStockThreshold { get; set; } = 5;
}

public class VariantForm
{
    public int Id { get; set; }
    public int ProductId { get; set; }

    [Required, StringLength(30)]
    public string Size { get; set; } = default!;

    [Required, StringLength(50)]
    public string Color { get; set; } = default!;

    [Required, StringLength(64), Display(Name = "SKU")]
    public string Sku { get; set; } = default!;

    [Range(0, 10_000_000), Display(Name = "MRP")]
    public decimal? Mrp { get; set; }

    [Range(0.01, 10_000_000), Display(Name = "Selling price")]
    public decimal SellingPrice { get; set; }

    [Range(0, 100_000), Display(Name = "Low stock alert at")]
    public int LowStockThreshold { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}

/// <summary>One uploaded file. Content must be readable (and seekable for the type check).</summary>
public sealed record ImageUpload(Stream Content, string FileName, long Length);

/// <summary>For the purchase form: "Party Frock – 2-3Y / Red (12-2-3Y-RED)".</summary>
public sealed record VariantOption(int Id, string DisplayName, decimal LastCost);

using ClothingStore.Core.Common;

namespace ClothingStore.Core.Entities;

public class Product : TenantEntity
{
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? Description { get; set; }
    public string? Brand { get; set; }
    public string? Fabric { get; set; }

    /// <summary>e.g. "2-3 Years".</summary>
    public string? AgeGroup { get; set; }

    public decimal Mrp { get; set; }

    /// <summary>Default selling price; a variant can have its own.</summary>
    public decimal SellingPrice { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }

    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }

    public int CategoryId { get; set; }
    public Category Category { get; set; } = default!;

    public ICollection<ProductImage> Images { get; set; } = [];
    public ICollection<ProductVariant> Variants { get; set; } = [];
}

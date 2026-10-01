using ClothingStore.Core.Common;

namespace ClothingStore.Core.Entities;

/// <summary>Girls Wear / Boys Wear and their sub-categories (Frock, T-Shirt, Jeans ...).</summary>
public class Category : TenantEntity
{
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? Description { get; set; }
    public string? ImagePath { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public int? ParentId { get; set; }
    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = [];

    public ICollection<Product> Products { get; set; } = [];
}

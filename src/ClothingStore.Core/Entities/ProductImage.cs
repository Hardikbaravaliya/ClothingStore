using ClothingStore.Core.Common;

namespace ClothingStore.Core.Entities;

public class ProductImage : TenantEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    /// <summary>Relative path, e.g. "{TenantId}/{ProductId}/abc.jpg". URL = Storage BaseUrl + Path.</summary>
    public string Path { get; set; } = default!;

    public string? AltText { get; set; }
    public int SortOrder { get; set; }
    public bool IsPrimary { get; set; }
}

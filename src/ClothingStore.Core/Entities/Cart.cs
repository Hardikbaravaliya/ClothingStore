using ClothingStore.Core.Common;

namespace ClothingStore.Core.Entities;

public class Cart : TenantEntity
{
    /// <summary>null for a guest cart (identified by SessionKey).</summary>
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public string? SessionKey { get; set; }

    public ICollection<CartItem> Items { get; set; } = [];
}

public class CartItem : TenantEntity
{
    public Guid CartId { get; set; }
    public Cart Cart { get; set; } = default!;

    public Guid ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = default!;

    public int Quantity { get; set; }
}

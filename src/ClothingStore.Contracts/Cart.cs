using System.ComponentModel.DataAnnotations;

namespace ClothingStore.Contracts.Cart;

public sealed record CartItemDto(
    int VariantId,
    string ProductName,
    string ProductSlug,
    string Size,
    string Color,
    string? ImageUrl,
    decimal UnitPrice,
    int Quantity,
    int AvailableQuantity) // 0..10; less than Quantity => fix the cart before checkout
{
    public decimal LineTotal => UnitPrice * Quantity;
    public bool IsAvailable => AvailableQuantity >= Quantity;
}

public sealed record CartDto(IReadOnlyList<CartItemDto> Items)
{
    public decimal SubTotal => Items.Sum(i => i.LineTotal);
    public int ItemCount => Items.Sum(i => i.Quantity);
    public bool CanCheckout => Items.Count > 0 && Items.All(i => i.IsAvailable);

    public static readonly CartDto Empty = new([]);
}

public sealed class AddCartItemRequest
{
    [Range(1, int.MaxValue)]
    public int VariantId { get; set; }

    [Range(1, CartLimits.MaxQuantityPerItem)]
    public int Quantity { get; set; } = 1;
}

public sealed class UpdateCartItemRequest
{
    /// <summary>0 removes the item.</summary>
    [Range(0, CartLimits.MaxQuantityPerItem)]
    public int Quantity { get; set; }
}

public static class CartLimits
{
    public const int MaxQuantityPerItem = 10;
    public const int MaxDistinctItems = 30;
}

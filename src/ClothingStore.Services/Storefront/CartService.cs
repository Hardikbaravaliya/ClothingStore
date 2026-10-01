using ClothingStore.Contracts.Cart;
using ClothingStore.Core.Common;
using ClothingStore.Core.Entities;
using ClothingStore.Core.Interfaces;
using ClothingStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Services.Storefront;

/// <summary>Whose cart: a logged-in customer, or a guest identified by a random session key.</summary>
public readonly record struct CartOwner(int? CustomerId, string? SessionKey)
{
    public bool IsValid => CustomerId is not null || !string.IsNullOrWhiteSpace(SessionKey);
}

public interface ICartService
{
    Task<CartDto> GetAsync(CartOwner owner, CancellationToken ct = default);
    Task<Result<CartDto>> AddItemAsync(CartOwner owner, AddCartItemRequest request, CancellationToken ct = default);

    /// <summary>Quantity 0 removes the item.</summary>
    Task<Result<CartDto>> UpdateItemAsync(CartOwner owner, int variantId, int quantity, CancellationToken ct = default);

    /// <summary>After login: moves the guest cart items into the customer's cart.</summary>
    Task<CartDto> MergeAsync(int customerId, string sessionKey, CancellationToken ct = default);

    Task ClearAsync(int customerId, CancellationToken ct = default);
}

public sealed class CartService(AppDbContext db, IImageStorage imageStorage) : ICartService
{
    public async Task<CartDto> GetAsync(CartOwner owner, CancellationToken ct = default)
    {
        var cart = await FindCartAsync(owner, ct);
        return cart is null ? CartDto.Empty : await ToDtoAsync(cart.Id, ct);
    }

    public async Task<Result<CartDto>> AddItemAsync(CartOwner owner, AddCartItemRequest request, CancellationToken ct = default)
    {
        if (!owner.IsValid)
            return Result<CartDto>.Fail("Cart not found.");

        var variant = await FindBuyableVariantAsync(request.VariantId, ct);
        if (variant is null)
            return Result<CartDto>.Fail("This item is not available.");

        var cart = await FindCartAsync(owner, ct) ?? CreateCart(owner);
        var item = cart.Items.FirstOrDefault(i => i.ProductVariantId == request.VariantId);
        if (item is null && cart.Items.Count >= CartLimits.MaxDistinctItems)
            return Result<CartDto>.Fail("Your cart is full.");

        var wanted = Math.Min((item?.Quantity ?? 0) + request.Quantity, CartLimits.MaxQuantityPerItem);
        if (wanted > variant.StockQty)
            return Result<CartDto>.Fail(variant.StockQty <= 0
                ? "Sorry, this size/color is out of stock."
                : $"Only {Math.Min(variant.StockQty, StorefrontCatalogService.MaxVisibleQuantity)} left in stock.");

        if (item is null)
            cart.Items.Add(new CartItem { ProductVariantId = variant.Id, Quantity = wanted });
        else
            item.Quantity = wanted;

        cart.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result<CartDto>.Ok(await ToDtoAsync(cart.Id, ct));
    }

    public async Task<Result<CartDto>> UpdateItemAsync(CartOwner owner, int variantId, int quantity, CancellationToken ct = default)
    {
        var cart = await FindCartAsync(owner, ct);
        var item = cart?.Items.FirstOrDefault(i => i.ProductVariantId == variantId);
        if (cart is null || item is null)
            return Result<CartDto>.Fail("Item is not in your cart.");

        if (quantity <= 0)
        {
            cart.Items.Remove(item);
        }
        else
        {
            var stock = await db.ProductVariants.Where(v => v.Id == variantId).Select(v => v.StockQty).FirstOrDefaultAsync(ct);
            if (quantity > stock)
                return Result<CartDto>.Fail(stock <= 0 ? "Sorry, this item is now out of stock." : $"Only {Math.Min(stock, StorefrontCatalogService.MaxVisibleQuantity)} left in stock.");
            item.Quantity = Math.Min(quantity, CartLimits.MaxQuantityPerItem);
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result<CartDto>.Ok(await ToDtoAsync(cart.Id, ct));
    }

    public async Task<CartDto> MergeAsync(int customerId, string sessionKey, CancellationToken ct = default)
    {
        var guest = await FindCartAsync(new CartOwner(null, sessionKey), ct);
        var customerCart = await FindCartAsync(new CartOwner(customerId, null), ct);

        if (guest is not null && guest.Items.Count > 0)
        {
            customerCart ??= CreateCart(new CartOwner(customerId, null));
            foreach (var guestItem in guest.Items)
            {
                var existing = customerCart.Items.FirstOrDefault(i => i.ProductVariantId == guestItem.ProductVariantId);
                if (existing is null)
                    customerCart.Items.Add(new CartItem { ProductVariantId = guestItem.ProductVariantId, Quantity = guestItem.Quantity });
                else
                    existing.Quantity = Math.Min(existing.Quantity + guestItem.Quantity, CartLimits.MaxQuantityPerItem);
            }
        }

        if (guest is not null)
            db.Carts.Remove(guest);
        await db.SaveChangesAsync(ct);

        return customerCart is null ? CartDto.Empty : await ToDtoAsync(customerCart.Id, ct);
    }

    public async Task ClearAsync(int customerId, CancellationToken ct = default)
    {
        var cart = await FindCartAsync(new CartOwner(customerId, null), ct);
        if (cart is null)
            return;
        db.Carts.Remove(cart);
        await db.SaveChangesAsync(ct);
    }

    private Task<Cart?> FindCartAsync(CartOwner owner, CancellationToken ct)
    {
        if (owner.CustomerId is { } customerId)
            return db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);
        if (!string.IsNullOrWhiteSpace(owner.SessionKey))
            return db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.CustomerId == null && c.SessionKey == owner.SessionKey, ct);
        return Task.FromResult<Cart?>(null);
    }

    private Cart CreateCart(CartOwner owner)
    {
        var cart = new Cart
        {
            CustomerId = owner.CustomerId,
            SessionKey = owner.CustomerId is null ? owner.SessionKey : null,
        };
        db.Carts.Add(cart);
        return cart;
    }

    private Task<ProductVariant?> FindBuyableVariantAsync(int variantId, CancellationToken ct) =>
        db.ProductVariants.AsNoTracking().FirstOrDefaultAsync(v => v.Id == variantId
            && v.IsActive && v.Product.IsActive && v.Product.Category.IsActive, ct);

    /// <summary>Prices and stock are read live: the cart never stores a price.</summary>
    private async Task<CartDto> ToDtoAsync(int cartId, CancellationToken ct)
    {
        var rows = await db.CartItems.AsNoTracking()
            .Where(i => i.CartId == cartId)
            .OrderBy(i => i.Id)
            .Select(i => new
            {
                i.ProductVariantId,
                ProductName = i.ProductVariant.Product.Name,
                ProductSlug = i.ProductVariant.Product.Slug,
                i.ProductVariant.Size,
                i.ProductVariant.Color,
                ImagePath = i.ProductVariant.Product.Images.OrderByDescending(img => img.IsPrimary).ThenBy(img => img.SortOrder).Select(img => img.Path).FirstOrDefault(),
                i.ProductVariant.SellingPrice,
                i.Quantity,
                Buyable = i.ProductVariant.IsActive && i.ProductVariant.Product.IsActive && i.ProductVariant.Product.Category.IsActive,
                i.ProductVariant.StockQty,
            })
            .ToListAsync(ct);

        return new CartDto(rows.Select(r => new CartItemDto(
                r.ProductVariantId, r.ProductName, r.ProductSlug, r.Size, r.Color,
                r.ImagePath is null ? null : imageStorage.GetUrl(r.ImagePath),
                r.SellingPrice, r.Quantity,
                r.Buyable ? Math.Clamp(r.StockQty, 0, StorefrontCatalogService.MaxVisibleQuantity) : 0))
            .ToList());
    }
}

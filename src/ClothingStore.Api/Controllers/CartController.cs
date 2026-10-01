using ClothingStore.Api.Auth;
using ClothingStore.Contracts;
using ClothingStore.Contracts.Cart;
using ClothingStore.Core.Common;
using ClothingStore.Services.Storefront;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClothingStore.Api.Controllers;

/// <summary>
/// Cart of the logged-in customer (Bearer token) or of a guest (X-Cart-Session header).
/// </summary>
[ApiController]
[Route("api/cart")]
public class CartController(ICartService carts) : ControllerBase
{
    [HttpGet]
    public async Task<CartDto> Get(CancellationToken ct) => await carts.GetAsync(this.CartOwner(), ct);

    [HttpPost("items")]
    public async Task<IActionResult> AddItem(AddCartItemRequest request, CancellationToken ct)
    {
        var owner = this.CartOwner();
        if (!owner.IsValid)
            return this.Problem(Result.Fail($"Log in or send a {ApiHeaders.CartSession} header."));

        var result = await carts.AddItemAsync(owner, request, ct);
        return result.Succeeded ? Ok(result.Value) : this.Problem(result);
    }

    [HttpPut("items/{variantId:int}")]
    public async Task<IActionResult> UpdateItem(int variantId, UpdateCartItemRequest request, CancellationToken ct)
    {
        var result = await carts.UpdateItemAsync(this.CartOwner(), variantId, request.Quantity, ct);
        return result.Succeeded ? Ok(result.Value) : this.Problem(result);
    }

    [HttpDelete("items/{variantId:int}")]
    public async Task<IActionResult> RemoveItem(int variantId, CancellationToken ct)
    {
        var result = await carts.UpdateItemAsync(this.CartOwner(), variantId, 0, ct);
        return result.Succeeded ? Ok(result.Value) : this.Problem(result);
    }

    /// <summary>After login: moves the guest cart (X-Cart-Session) into the customer's cart.</summary>
    [HttpPost("merge")]
    [Authorize(Roles = AppRoles.Customer)]
    public async Task<CartDto> Merge(CancellationToken ct)
    {
        var customerId = this.CustomerId()!.Value;
        return this.GuestCartSession() is { } session
            ? await carts.MergeAsync(customerId, session, ct)
            : await carts.GetAsync(new CartOwner(customerId, null), ct);
    }
}

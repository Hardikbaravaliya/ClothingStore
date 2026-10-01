using ClothingStore.Catalog.ApiClients;
using ClothingStore.Catalog.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace ClothingStore.Catalog.Controllers;

[Route("cart")]
public class CartController(StorefrontApi api) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await api.GetCartAsync(ct));

    [HttpPost("add"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int variantId, int quantity, string? returnUrl, CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true)
            CartSession.GetOrCreate(HttpContext);

        var result = await api.AddToCartAsync(variantId, Math.Max(quantity, 1), ct);
        if (result.Succeeded)
        {
            TempData["Success"] = "Added to your cart.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Error"] = result.Error;
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Index));
    }

    [HttpPost("update"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int variantId, int quantity, CancellationToken ct)
    {
        var result = await api.UpdateCartItemAsync(variantId, Math.Max(quantity, 0), ct);
        if (!result.Succeeded)
            TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("remove"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int variantId, CancellationToken ct)
    {
        var result = await api.UpdateCartItemAsync(variantId, 0, ct);
        if (!result.Succeeded)
            TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Index));
    }
}

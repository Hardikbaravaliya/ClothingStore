using ClothingStore.Catalog.ApiClients;
using ClothingStore.Catalog.Infrastructure;
using ClothingStore.Catalog.Models;
using ClothingStore.Contracts.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClothingStore.Catalog.Controllers;

[Authorize]
[Route("checkout")]
public class CheckoutController(StorefrontApi api, StoreContext storeContext) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = await BuildAsync(null, ct);
        if (model.Cart.Items.Count == 0)
            return RedirectToAction("Index", "Cart");
        return View(model);
    }

    [HttpPost(""), ValidateAntiForgeryToken]
    public async Task<IActionResult> Index([Bind(Prefix = "Form")] CheckoutForm form, CancellationToken ct)
    {
        // The new-address fields only matter when that option is chosen
        if (!form.UseNewAddress)
        {
            foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Form.NewAddress.", StringComparison.Ordinal)).ToList())
                ModelState.Remove(key);
            if (form.AddressId is null)
                ModelState.AddModelError("Form.AddressId", "Choose a delivery address.");
        }

        if (!ModelState.IsValid)
            return View(await BuildAsync(form, ct));

        var result = await api.PlaceOrderAsync(new PlaceOrderRequest
        {
            AddressId = form.UseNewAddress ? null : form.AddressId,
            NewAddress = form.UseNewAddress ? form.NewAddress : null,
            SaveNewAddress = true,
            PaymentMethod = form.PaymentMethod,
            Notes = form.Notes,
        }, ct);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(await BuildAsync(form, ct));
        }

        var order = result.Value!;
        return order.Razorpay is null
            ? RedirectToAction(nameof(Done), new { orderNo = order.OrderNo })
            : RedirectToAction(nameof(Pay), new { orderNo = order.OrderNo });
    }

    /// <summary>Opens Razorpay Checkout for a pending online order (also used for "retry payment").</summary>
    [HttpGet("pay/{orderNo}")]
    public async Task<IActionResult> Pay(string orderNo, CancellationToken ct)
    {
        var result = await api.GetPaymentAsync(orderNo, ct);
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction("Details", "Orders", new { orderNo });
        }

        ViewBag.OrderNo = orderNo;
        ViewBag.ThemeColor = storeContext.Store.ThemeColor;
        return View(result.Value);
    }

    /// <summary>Razorpay success handler posts here; the Api checks the signature before confirming.</summary>
    [HttpPost("verify"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Verify(string orderNo, string razorpay_order_id, string razorpay_payment_id, string razorpay_signature, CancellationToken ct)
    {
        var result = await api.VerifyPaymentAsync(new VerifyPaymentRequest
        {
            OrderNo = orderNo,
            RazorpayOrderId = razorpay_order_id,
            RazorpayPaymentId = razorpay_payment_id,
            RazorpaySignature = razorpay_signature,
        }, ct);

        if (result.Succeeded)
            return RedirectToAction(nameof(Done), new { orderNo });

        TempData["Error"] = result.Error;
        return RedirectToAction("Details", "Orders", new { orderNo });
    }

    [HttpGet("done/{orderNo}")]
    public async Task<IActionResult> Done(string orderNo, CancellationToken ct)
    {
        var order = await api.GetOrderAsync(orderNo, ct);
        return order is null ? NotFound() : View(order);
    }

    private async Task<CheckoutViewModel> BuildAsync(CheckoutForm? form, CancellationToken ct)
    {
        var cart = api.GetCartAsync(ct);
        var addresses = api.GetAddressesAsync(ct);
        await Task.WhenAll(cart, addresses);

        form ??= new CheckoutForm
        {
            AddressId = addresses.Result.FirstOrDefault(a => a.IsDefault)?.Id ?? addresses.Result.FirstOrDefault()?.Id,
            UseNewAddress = addresses.Result.Count == 0,
            NewAddress = new() { FullName = User.Identity?.Name ?? "" },
        };

        return new CheckoutViewModel
        {
            Cart = cart.Result,
            Addresses = addresses.Result,
            OnlinePaymentEnabled = storeContext.Store.OnlinePaymentEnabled,
            Form = form,
        };
    }
}

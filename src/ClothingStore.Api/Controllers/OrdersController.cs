using ClothingStore.Api.Auth;
using ClothingStore.Contracts;
using ClothingStore.Contracts.Orders;
using ClothingStore.Core.Common;
using ClothingStore.Services.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClothingStore.Api.Controllers;

/// <summary>Checkout, payment verification and "My orders". Customer token required.</summary>
[ApiController]
[Route("api")]
[Authorize(Roles = AppRoles.Customer)]
public class OrdersController(
    ICheckoutService checkout,
    IPaymentService payments,
    ICustomerOrderService orders) : ControllerBase
{
    /// <summary>Places an order from the customer's cart (COD = confirmed now, Online = waits for Razorpay).</summary>
    [HttpPost("orders")]
    [ProducesResponseType<PlaceOrderResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PlaceOrder(PlaceOrderRequest request, CancellationToken ct)
    {
        var result = await checkout.PlaceOrderAsync(this.CustomerId()!.Value, request, ct);
        return result.Succeeded ? Ok(result.Value) : this.Problem(result);
    }

    [HttpGet("orders")]
    public async Task<PagedResult<OrderSummaryDto>> GetOrders([FromQuery] int page = 1, CancellationToken ct = default) =>
        await orders.GetOrdersAsync(this.CustomerId()!.Value, page, ct);

    [HttpGet("orders/{orderNo}")]
    [ProducesResponseType<OrderDetailDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrder(string orderNo, CancellationToken ct)
    {
        var order = await orders.GetOrderAsync(this.CustomerId()!.Value, orderNo, ct);
        return order is null ? NotFound() : Ok(order);
    }

    /// <summary>Razorpay data to (re)open the payment window for a pending online order.</summary>
    [HttpGet("orders/{orderNo}/payment")]
    [ProducesResponseType<RazorpayCheckoutDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPayment(string orderNo, CancellationToken ct)
    {
        var result = await payments.GetCheckoutAsync(this.CustomerId()!.Value, orderNo, ct);
        return result.Succeeded ? Ok(result.Value) : this.Problem(result);
    }

    /// <summary>Called after Razorpay Checkout succeeds in the browser.</summary>
    [HttpPost("payments/verify")]
    public async Task<IActionResult> VerifyPayment(VerifyPaymentRequest request, CancellationToken ct)
    {
        var result = await payments.VerifyAsync(this.CustomerId()!.Value, request, ct);
        return result.Succeeded ? NoContent() : this.Problem(result);
    }
}

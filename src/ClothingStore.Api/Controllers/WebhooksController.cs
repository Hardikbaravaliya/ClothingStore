using ClothingStore.Services.Orders;
using Microsoft.AspNetCore.Mvc;

namespace ClothingStore.Api.Controllers;

/// <summary>
/// Called by Razorpay, not by the Catalog. The store comes from the URL (ApiTenantMiddleware),
/// e.g. POST /api/webhooks/razorpay/shop1. The signature (X-Razorpay-Signature) proves the sender.
/// </summary>
[ApiController]
[Route("api/webhooks")]
[ApiExplorerSettings(IgnoreApi = true)]
public class WebhooksController(IPaymentService payments, ILogger<WebhooksController> logger) : ControllerBase
{
    [HttpPost("razorpay/{tenant}")]
    public async Task<IActionResult> Razorpay(string tenant, CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct); // raw body: the signature is over these exact bytes
        var signature = Request.Headers["X-Razorpay-Signature"].FirstOrDefault();

        var result = await payments.HandleWebhookAsync(body, signature, ct);
        if (result.Succeeded)
            return Ok();

        logger.LogWarning("Razorpay webhook for {Tenant} rejected: {Error}", tenant, result.Error);
        return BadRequest();
    }
}

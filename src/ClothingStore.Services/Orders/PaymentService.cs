using System.Text.Json;
using ClothingStore.Contracts.Orders;
using ClothingStore.Core.Common;
using ClothingStore.Core.Enums;
using ClothingStore.Core.Interfaces;
using ClothingStore.Infrastructure.Data;
using ClothingStore.Services.Inventory;
using ClothingStore.Services.Store;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClothingStore.Services.Orders;

public interface IPaymentService
{
    /// <summary>Razorpay checkout data to (re)open the payment window for a pending online order.</summary>
    Task<Result<RazorpayCheckoutDto>> GetCheckoutAsync(int customerId, string orderNo, CancellationToken ct = default);

    /// <summary>Browser callback after a successful payment: verifies the signature, then confirms the order.</summary>
    Task<Result> VerifyAsync(int customerId, VerifyPaymentRequest request, CancellationToken ct = default);

    /// <summary>Razorpay webhook (backup when the browser was closed). Safe to receive many times.</summary>
    Task<Result> HandleWebhookAsync(string rawBody, string? signature, CancellationToken ct = default);
}

public sealed class PaymentService(
    AppDbContext db,
    IStockService stockService,
    IStoreSettingsService storeSettings,
    IPaymentGateway paymentGateway,
    ILogger<PaymentService> logger) : IPaymentService
{
    private const int MaxConcurrencyRetries = 3;

    public async Task<Result<RazorpayCheckoutDto>> GetCheckoutAsync(int customerId, string orderNo, CancellationToken ct = default)
    {
        var data = await db.Payments.AsNoTracking()
            .Where(p => p.Order.OrderNo == orderNo && p.Order.CustomerId == customerId
                        && p.Method == PaymentMethod.Online && p.Status == PaymentStatus.Pending
                        && p.Order.Status == OrderStatus.Pending && p.RazorpayOrderId != null)
            .Select(p => new { p.RazorpayOrderId, p.Amount, p.CurrencyCode, p.Order.Customer.FullName, p.Order.Customer.Email, p.Order.Customer.Phone })
            .FirstOrDefaultAsync(ct);
        if (data is null)
            return Result<RazorpayCheckoutDto>.Fail("This order is not waiting for payment.");

        var credentials = await storeSettings.GetPaymentCredentialsAsync(ct);
        if (credentials is null)
            return Result<RazorpayCheckoutDto>.Fail("Online payment is not available in this store.");

        var store = await storeSettings.GetCurrentAsync(ct);
        return Result<RazorpayCheckoutDto>.Ok(new RazorpayCheckoutDto(credentials.KeyId, data.RazorpayOrderId!,
            CheckoutService.ToPaise(data.Amount), data.CurrencyCode, store?.Name ?? "Store", data.FullName, data.Email, data.Phone));
    }

    public async Task<Result> VerifyAsync(int customerId, VerifyPaymentRequest request, CancellationToken ct = default)
    {
        var ownsOrder = await db.Payments.AnyAsync(p => p.RazorpayOrderId == request.RazorpayOrderId
            && p.Order.OrderNo == request.OrderNo && p.Order.CustomerId == customerId, ct);
        if (!ownsOrder)
            return Result.Fail("Payment not found.");

        var credentials = await storeSettings.GetPaymentCredentialsAsync(ct);
        if (credentials is null || !paymentGateway.VerifyPaymentSignature(credentials.KeySecret, request.RazorpayOrderId,
                request.RazorpayPaymentId, request.RazorpaySignature))
        {
            logger.LogWarning("Invalid Razorpay signature for order {OrderNo}", request.OrderNo);
            return Result.Fail("Payment could not be verified. If money was deducted, it will be confirmed automatically or refunded.");
        }

        return await ConfirmPaymentAsync(request.RazorpayOrderId, request.RazorpayPaymentId, request.RazorpaySignature, null, ct);
    }

    public async Task<Result> HandleWebhookAsync(string rawBody, string? signature, CancellationToken ct = default)
    {
        var credentials = await storeSettings.GetPaymentCredentialsAsync(ct);
        if (credentials?.WebhookSecret is null)
            return Result.Fail("Webhook secret is not configured for this store.");
        if (signature is null || !paymentGateway.VerifyWebhookSignature(credentials.WebhookSecret, rawBody, signature))
            return Result.Fail("Invalid webhook signature.");

        string? eventName, gatewayOrderId, paymentId, error;
        long amount;
        try
        {
            using var json = JsonDocument.Parse(rawBody);
            eventName = json.RootElement.GetProperty("event").GetString();
            if (!json.RootElement.TryGetProperty("payload", out var payload) || !payload.TryGetProperty("payment", out var paymentElement))
                return Result.Ok(); // not a payment event – nothing to do

            var entity = paymentElement.GetProperty("entity");
            gatewayOrderId = entity.GetProperty("order_id").GetString();
            paymentId = entity.GetProperty("id").GetString();
            amount = entity.GetProperty("amount").GetInt64();
            error = entity.TryGetProperty("error_description", out var e) ? e.GetString() : null;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Unreadable Razorpay webhook body");
            return Result.Fail("Unreadable webhook body.");
        }

        if (gatewayOrderId is null || paymentId is null)
            return Result.Ok();

        return eventName switch
        {
            "payment.captured" or "order.paid" => await ConfirmPaymentAsync(gatewayOrderId, paymentId, null, amount, ct),
            "payment.failed" => await MarkFailedAsync(gatewayOrderId, paymentId, error, ct),
            _ => Result.Ok(),
        };
    }

    private async Task<Result> ConfirmPaymentAsync(string gatewayOrderId, string paymentId, string? signature, long? amountInPaise, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await ConfirmOnceAsync(gatewayOrderId, paymentId, signature, amountInPaise, ct);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries)
            {
                db.ChangeTracker.Clear();
            }
            catch (DbUpdateException ex) when (attempt < MaxConcurrencyRetries)
            {
                // Browser callback and webhook arrived together: the unique RazorpayPaymentId index stopped
                // the second one. Retry; it will now see the payment as Paid and return OK.
                logger.LogInformation(ex, "Concurrent confirmation of {GatewayOrderId}, retrying", gatewayOrderId);
                db.ChangeTracker.Clear();
            }
        }
    }

    private async Task<Result> ConfirmOnceAsync(string gatewayOrderId, string paymentId, string? signature, long? amountInPaise, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var payment = await db.Payments
            .Include(p => p.Order).ThenInclude(o => o.Items).ThenInclude(i => i.ProductVariant)
            .FirstOrDefaultAsync(p => p.RazorpayOrderId == gatewayOrderId, ct);
        if (payment is null)
            return Result.Fail("Payment not found.");
        if (payment.Status == PaymentStatus.Paid)
            return Result.Ok(); // already done (idempotent)

        if (amountInPaise is { } paid && paid != CheckoutService.ToPaise(payment.Amount))
        {
            logger.LogError("Amount mismatch for {GatewayOrderId}: paid {Paid} paise, expected {Expected}", gatewayOrderId, paid, payment.Amount);
            return Result.Fail("Paid amount does not match the order.");
        }

        var order = payment.Order;
        payment.Status = PaymentStatus.Paid;
        payment.RazorpayPaymentId = paymentId;
        payment.RazorpaySignature = signature ?? payment.RazorpaySignature;
        payment.FailureReason = null;
        order.PaymentStatus = PaymentStatus.Paid;

        if (order.Status == OrderStatus.Pending)
        {
            var allInStock = order.Items.All(i => i.ProductVariant.StockQty >= i.Quantity);
            if (allInStock)
            {
                foreach (var item in order.Items)
                    stockService.TryRecordSale(item.ProductVariant, item.Quantity, order.Id, order.OrderNo);
                order.Status = OrderStatus.Confirmed;
                order.ConfirmedAt = DateTime.UtcNow;

                var cart = await db.Carts.FirstOrDefaultAsync(c => c.CustomerId == order.CustomerId, ct);
                if (cart is not null)
                    db.Carts.Remove(cart);
            }
            else
            {
                // Someone bought the last pieces while this customer was paying
                order.Status = OrderStatus.Cancelled;
                order.CancelledAt = DateTime.UtcNow;
                order.CancelReason = "Out of stock after payment. Your refund will be processed.";
                logger.LogWarning("Order {OrderNo} paid but out of stock – refund needed", order.OrderNo);
            }
        }
        else
        {
            logger.LogWarning("Payment received for order {OrderNo} in status {Status} – check for refund", order.OrderNo, order.Status);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result.Ok();
    }

    private async Task<Result> MarkFailedAsync(string gatewayOrderId, string paymentId, string? error, CancellationToken ct)
    {
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.RazorpayOrderId == gatewayOrderId, ct);
        if (payment is null || payment.Status != PaymentStatus.Pending)
            return Result.Ok();

        // The order stays Pending: the customer can retry with another method/card
        payment.FailureReason = $"{paymentId}: {error}".Trim(' ', ':');
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}

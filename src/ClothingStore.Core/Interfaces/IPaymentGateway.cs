namespace ClothingStore.Core.Interfaces;

/// <summary>Razorpay credentials of one store (already decrypted).</summary>
public sealed record PaymentGatewayCredentials(string KeyId, string KeySecret, string? WebhookSecret);

public interface IPaymentGateway
{
    /// <summary>Creates a gateway order (Razorpay "order_xxx") for the amount in the smallest unit (paise).</summary>
    Task<string> CreateOrderAsync(PaymentGatewayCredentials credentials, long amountInPaise, string currency, string receipt, CancellationToken ct = default);

    /// <summary>HMAC_SHA256(orderId + "|" + paymentId, keySecret) == signature.</summary>
    bool VerifyPaymentSignature(string keySecret, string gatewayOrderId, string paymentId, string signature);

    /// <summary>HMAC_SHA256(rawBody, webhookSecret) == signature.</summary>
    bool VerifyWebhookSignature(string webhookSecret, string rawBody, string signature);
}

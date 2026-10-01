using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using ClothingStore.Core.Interfaces;

namespace ClothingStore.Infrastructure.Payments;

/// <summary>
/// Razorpay Orders API over plain HTTP (test keys locally, live keys in production – per store).
/// Docs: https://razorpay.com/docs/api/orders/
/// </summary>
public sealed class RazorpayGateway(HttpClient http) : IPaymentGateway
{
    public const string BaseUrl = "https://api.razorpay.com/v1/";

    public async Task<string> CreateOrderAsync(PaymentGatewayCredentials credentials, long amountInPaise, string currency, string receipt, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "orders")
        {
            Content = JsonContent.Create(new CreateOrderBody(amountInPaise, currency, receipt)),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.KeyId}:{credentials.KeySecret}")));

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new PaymentGatewayException($"Razorpay order create failed ({(int)response.StatusCode}): {body}");
        }

        var order = await response.Content.ReadFromJsonAsync<CreateOrderResult>(ct);
        return order?.Id ?? throw new PaymentGatewayException("Razorpay returned no order id.");
    }

    public bool VerifyPaymentSignature(string keySecret, string gatewayOrderId, string paymentId, string signature) =>
        SignatureMatches(keySecret, $"{gatewayOrderId}|{paymentId}", signature);

    public bool VerifyWebhookSignature(string webhookSecret, string rawBody, string signature) =>
        SignatureMatches(webhookSecret, rawBody, signature);

    public static string ComputeSignature(string secret, string payload) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload)));

    private static bool SignatureMatches(string secret, string payload, string signature)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(signature))
            return false;

        var expected = Encoding.ASCII.GetBytes(ComputeSignature(secret, payload));
        var actual = Encoding.ASCII.GetBytes(signature.Trim().ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private sealed record CreateOrderBody(
        [property: JsonPropertyName("amount")] long Amount,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("receipt")] string Receipt);

    private sealed record CreateOrderResult([property: JsonPropertyName("id")] string Id);
}

public sealed class PaymentGatewayException(string message) : Exception(message);

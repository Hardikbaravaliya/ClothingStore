using System.ComponentModel.DataAnnotations;
using ClothingStore.Contracts.Account;

namespace ClothingStore.Contracts.Orders;

public static class PaymentMethods
{
    public const string Cod = "Cod";
    public const string Online = "Online";
}

public sealed class PlaceOrderRequest : IValidatableObject
{
    /// <summary>A saved address of the customer, or null with <see cref="NewAddress"/>.</summary>
    public int? AddressId { get; set; }

    public AddressRequest? NewAddress { get; set; }

    /// <summary>Save <see cref="NewAddress"/> to the customer's address book.</summary>
    public bool SaveNewAddress { get; set; } = true;

    [Required]
    public string PaymentMethod { get; set; } = PaymentMethods.Cod;

    [StringLength(500)]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AddressId is null && NewAddress is null)
            yield return new ValidationResult("Choose a delivery address.", [nameof(AddressId)]);
        if (PaymentMethod is not (PaymentMethods.Cod or PaymentMethods.Online))
            yield return new ValidationResult("Choose a payment method.", [nameof(PaymentMethod)]);
    }
}

/// <summary>Everything the Razorpay Checkout JS needs.</summary>
public sealed record RazorpayCheckoutDto(
    string KeyId,
    string RazorpayOrderId,
    long AmountInPaise,
    string Currency,
    string StoreName,
    string CustomerName,
    string CustomerEmail,
    string? CustomerPhone);

public sealed record PlaceOrderResponse(string OrderNo, string Status, string PaymentMethod, RazorpayCheckoutDto? Razorpay);

public sealed class VerifyPaymentRequest
{
    [Required] public string OrderNo { get; set; } = default!;
    [Required] public string RazorpayOrderId { get; set; } = default!;
    [Required] public string RazorpayPaymentId { get; set; } = default!;
    [Required] public string RazorpaySignature { get; set; } = default!;
}

public sealed record OrderSummaryDto(
    string OrderNo, DateTime CreatedAtUtc, string Status, string PaymentMethod, string PaymentStatus,
    decimal TotalAmount, string CurrencyCode, int ItemCount);

public sealed record OrderItemDto(
    string ProductName, string? ProductSlug, string Size, string Color, string? ImageUrl,
    int Quantity, decimal UnitPrice, decimal LineTotal);

public sealed record OrderAddressDto(
    string Name, string Phone, string Line1, string? Line2, string City, string State, string Pincode, string Country);

/// <summary>One step of the order timeline (Placed, Confirmed, Delivered, Cancelled ...).</summary>
public sealed record OrderEventDto(string Title, DateTime AtUtc);

public sealed record OrderDetailDto(
    string OrderNo,
    DateTime CreatedAtUtc,
    string Status,
    string PaymentMethod,
    string PaymentStatus,
    decimal SubTotal,
    decimal DiscountAmount,
    decimal ShippingCharge,
    decimal TaxAmount,
    decimal TotalAmount,
    string CurrencyCode,
    string? Notes,
    string? CancelReason,
    OrderAddressDto ShippingAddress,
    IReadOnlyList<OrderItemDto> Items,
    IReadOnlyList<OrderEventDto> Timeline,
    bool CanRetryPayment); // online order still waiting for payment

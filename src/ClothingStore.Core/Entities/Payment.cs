using ClothingStore.Core.Common;
using ClothingStore.Core.Enums;

namespace ClothingStore.Core.Entities;

public class Payment : TenantEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = default!;

    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = default!;

    public string? RazorpayOrderId { get; set; }
    public string? RazorpayPaymentId { get; set; }
    public string? RazorpaySignature { get; set; }

    public string? RefundId { get; set; }
    public decimal? RefundAmount { get; set; }

    public string? FailureReason { get; set; }
}

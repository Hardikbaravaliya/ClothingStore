using ClothingStore.Core.Common;
using ClothingStore.Core.Enums;

namespace ClothingStore.Core.Entities;

/// <summary>Return / Exchange request (table "Returns").</summary>
public class ReturnRequest : TenantEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = default!;

    public int OrderItemId { get; set; }
    public OrderItem OrderItem { get; set; } = default!;

    public ReturnType Type { get; set; }
    public ReturnStatus Status { get; set; } = ReturnStatus.Requested;

    public int Quantity { get; set; }
    public string Reason { get; set; } = default!;

    /// <summary>For an exchange: the variant the customer wants instead.</summary>
    public int? ExchangeVariantId { get; set; }
    public ProductVariant? ExchangeVariant { get; set; }

    public decimal? RefundAmount { get; set; }
    public string? AdminNotes { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

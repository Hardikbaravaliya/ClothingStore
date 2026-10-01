using ClothingStore.Core.Common;
using ClothingStore.Core.Enums;

namespace ClothingStore.Core.Entities;

public class Order : TenantEntity
{
    public string OrderNo { get; set; } = default!;

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;

    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ShippingCharge { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }

    /// <summary>Snapshot of tenant currency at order time.</summary>
    public string CurrencyCode { get; set; } = default!;

    public string? CouponCode { get; set; }

    // Shipping address snapshot
    public string ShippingName { get; set; } = default!;
    public string ShippingPhone { get; set; } = default!;
    public string ShippingLine1 { get; set; } = default!;
    public string? ShippingLine2 { get; set; }
    public string ShippingCity { get; set; } = default!;
    public string ShippingState { get; set; } = default!;
    public string ShippingPincode { get; set; } = default!;
    public string ShippingCountry { get; set; } = default!;

    public string? Notes { get; set; }

    public DateTime? ConfirmedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    public ICollection<OrderItem> Items { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
    public ICollection<Shipment> Shipments { get; set; } = [];
}

public class OrderItem : TenantEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = default!;

    public Guid ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = default!;

    // Snapshot, so later product edits don't change old orders
    public string ProductName { get; set; } = default!;
    public string Size { get; set; } = default!;
    public string Color { get; set; } = default!;
    public string Sku { get; set; } = default!;

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    /// <summary>AvgCostPrice snapshot at order time, used for profit.</summary>
    public decimal CostPrice { get; set; }

    public decimal LineTotal { get; set; }
}

namespace ClothingStore.Core.Enums;

public enum TenantStatus { Trial, Active, Suspended }

public enum SubscriptionStatus { Active, Expired, Cancelled }

public enum OrderStatus
{
    Pending,
    Confirmed,
    Packed,
    Shipped,
    OutForDelivery,
    Delivered,
    Cancelled,
    ReturnRequested,
    Returned
}

public enum PaymentMethod { Online, Cod }

public enum PaymentStatus { Pending, Paid, Failed, Refunded }

public enum StockTxnType { In, Out, Return, Adjust }

public enum ShipmentStatus
{
    Created,
    PickupScheduled,
    PickedUp,
    InTransit,
    OutForDelivery,
    Delivered,
    Cancelled,
    ReturnToOrigin
}

public enum ReturnType { Return, Exchange }

public enum ReturnStatus { Requested, Approved, Rejected, Received, Refunded, Exchanged }

public enum DiscountType { Percentage, FixedAmount }

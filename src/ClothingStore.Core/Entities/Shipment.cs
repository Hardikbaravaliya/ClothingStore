using ClothingStore.Core.Common;
using ClothingStore.Core.Enums;

namespace ClothingStore.Core.Entities;

public class Shipment : TenantEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = default!;

    /// <summary>"Shiprocket" or "Fake".</summary>
    public string Provider { get; set; } = default!;
    public string? ProviderOrderId { get; set; }
    public string? ProviderShipmentId { get; set; }

    public string? CourierName { get; set; }
    public string? Awb { get; set; }
    public string? LabelUrl { get; set; }

    public ShipmentStatus Status { get; set; } = ShipmentStatus.Created;

    /// <summary>What we pay the courier, used for profit.</summary>
    public decimal ShippingCost { get; set; }

    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }

    public ICollection<ShipmentTrackingLog> TrackingLogs { get; set; } = [];
}

public class ShipmentTrackingLog : TenantEntity
{
    public int ShipmentId { get; set; }
    public Shipment Shipment { get; set; } = default!;

    /// <summary>Raw status text from the courier.</summary>
    public string Status { get; set; } = default!;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public DateTime EventTime { get; set; }
}

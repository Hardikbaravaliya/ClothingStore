using ClothingStore.Core.Common;

namespace ClothingStore.Core.Tenancy;

/// <summary>Platform table – not filtered by tenant.</summary>
public class SubscriptionPlan : BaseEntity
{
    public string Name { get; set; } = default!;
    public decimal Price { get; set; }
    public int DurationDays { get; set; }

    /// <summary>null = unlimited.</summary>
    public int? MaxProducts { get; set; }
    public int? MaxStaffUsers { get; set; }
    public int? MaxOrdersPerMonth { get; set; }

    public bool IsActive { get; set; } = true;
}

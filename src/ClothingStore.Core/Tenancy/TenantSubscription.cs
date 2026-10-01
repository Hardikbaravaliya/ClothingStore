using ClothingStore.Core.Common;
using ClothingStore.Core.Enums;

namespace ClothingStore.Core.Tenancy;

/// <summary>Platform table – managed by Super Admin, so not filtered by tenant.</summary>
public class TenantSubscription : BaseEntity
{
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = default!;

    public int PlanId { get; set; }
    public SubscriptionPlan Plan { get; set; } = default!;

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public decimal Amount { get; set; }
    public string? PaymentReference { get; set; }
}

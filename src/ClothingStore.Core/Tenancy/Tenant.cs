using ClothingStore.Core.Common;
using ClothingStore.Core.Enums;

namespace ClothingStore.Core.Tenancy;

/// <summary>Platform table – not filtered by tenant.</summary>
public class Tenant : BaseEntity
{
    public string Name { get; set; } = default!;

    /// <summary>Subdomain key, e.g. "shop1" for shop1.domain.com. Lowercase, unique.</summary>
    public string Slug { get; set; } = default!;

    /// <summary>Optional custom domain, e.g. "www.littlestars.in" stored as "littlestars.in".</summary>
    public string? CustomDomain { get; set; }

    public TenantStatus Status { get; set; } = TenantStatus.Trial;

    public Guid? PlanId { get; set; }
    public SubscriptionPlan? Plan { get; set; }

    public TenantSettings? Settings { get; set; }
    public ICollection<TenantSubscription> Subscriptions { get; set; } = [];
}

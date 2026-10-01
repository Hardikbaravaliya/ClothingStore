using ClothingStore.Core.Tenancy;

namespace ClothingStore.Infrastructure.Tenancy;

/// <summary>Scoped: one per request.</summary>
public sealed class TenantProvider : ITenantProvider
{
    public Guid? TenantId { get; private set; }

    public void SetTenant(Guid? tenantId) => TenantId = tenantId;
}

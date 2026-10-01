namespace ClothingStore.Core.Tenancy;

/// <summary>
/// Current tenant for this request/scope. Set by tenant middleware (or by login / seeding).
/// null = no tenant (Super Admin or unresolved): tenant-filtered queries then return no rows.
/// </summary>
public interface ITenantProvider
{
    int? TenantId { get; }

    void SetTenant(int? tenantId);
}

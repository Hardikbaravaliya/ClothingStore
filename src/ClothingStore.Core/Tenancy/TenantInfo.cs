using ClothingStore.Core.Enums;

namespace ClothingStore.Core.Tenancy;

public sealed record TenantInfo(int Id, string Slug, string Name, TenantStatus Status)
{
    public bool IsActive => Status != TenantStatus.Suspended;
}

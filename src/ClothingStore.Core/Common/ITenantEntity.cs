namespace ClothingStore.Core.Common;

/// <summary>
/// Business data that belongs to one tenant. AppDbContext applies a global query filter
/// on TenantId and sets/validates it on SaveChanges.
/// </summary>
public interface ITenantEntity
{
    int TenantId { get; set; }
}

public abstract class TenantEntity : BaseEntity, ITenantEntity
{
    public int TenantId { get; set; }
}

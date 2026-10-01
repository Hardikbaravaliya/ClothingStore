namespace ClothingStore.Core.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; }

    /// <summary>Always UTC.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Always UTC.</summary>
    public DateTime? UpdatedAt { get; set; }
}

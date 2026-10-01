namespace ClothingStore.Core.Common;

public abstract class BaseEntity
{
    public int Id { get; set; }

    /// <summary>Always UTC.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Always UTC.</summary>
    public DateTime? UpdatedAt { get; set; }
}

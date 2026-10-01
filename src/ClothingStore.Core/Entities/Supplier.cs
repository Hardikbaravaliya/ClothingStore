using ClothingStore.Core.Common;

namespace ClothingStore.Core.Entities;

public class Supplier : TenantEntity
{
    public string Name { get; set; } = default!;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? GstNo { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Pincode { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Purchase> Purchases { get; set; } = [];
}

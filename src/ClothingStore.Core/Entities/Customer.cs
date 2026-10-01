using ClothingStore.Core.Common;

namespace ClothingStore.Core.Entities;

/// <summary>Customer profile of one store. Login lives in AspNetUsers (UserId).</summary>
public class Customer : TenantEntity
{
    public Guid UserId { get; set; }

    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Address> Addresses { get; set; } = [];
    public ICollection<Order> Orders { get; set; } = [];
}

public class Address : TenantEntity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;

    public string FullName { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string Line1 { get; set; } = default!;
    public string? Line2 { get; set; }
    public string? Landmark { get; set; }
    public string City { get; set; } = default!;
    public string State { get; set; } = default!;
    public string Pincode { get; set; } = default!;
    public string Country { get; set; } = "India";
    public bool IsDefault { get; set; }
}

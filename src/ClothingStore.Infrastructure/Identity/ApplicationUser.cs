using Microsoft.AspNetCore.Identity;

namespace ClothingStore.Infrastructure.Identity;

/// <summary>
/// Staff and customer login. TenantId is null only for SuperAdmin.
/// Email/UserName are unique per tenant, not globally.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public Guid? TenantId { get; set; }

    public string FullName { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() { }

    public ApplicationRole(string roleName) : base(roleName) { }
}

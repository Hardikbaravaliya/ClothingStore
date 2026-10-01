using System.Security.Claims;
using ClothingStore.Core.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ClothingStore.Infrastructure.Identity;

/// <summary>Adds tenant_id and full_name claims to the login cookie / token.</summary>
public class AppUserClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, ApplicationRole>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(AppClaimTypes.FullName, user.FullName));
        if (user.TenantId is { } tenantId)
            identity.AddClaim(new Claim(AppClaimTypes.TenantId, tenantId.ToString()));
        return identity;
    }
}

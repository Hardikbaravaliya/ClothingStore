using ClothingStore.Core.Tenancy;
using ClothingStore.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Infrastructure.Identity;

/// <summary>
/// Users are tenant-filtered (FindByEmail/FindByName only see the current tenant's users).
/// FindById is the exception when no tenant is set yet: the cookie security-stamp check runs
/// before the tenant middleware, and an identity id is unique across all tenants.
/// </summary>
public class AppUserStore(AppDbContext context, ITenantProvider tenantProvider, IdentityErrorDescriber? describer = null)
    : UserStore<ApplicationUser, ApplicationRole, AppDbContext, int>(context, describer)
{
    public override Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (tenantProvider.TenantId is not null)
            return base.FindByIdAsync(userId, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        var id = ConvertIdFromString(userId);
        return Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }
}

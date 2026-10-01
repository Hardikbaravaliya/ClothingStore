using ClothingStore.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ClothingStore.Infrastructure.Identity;

public static class IdentitySetup
{
    public static void ConfigureOptions(IdentityOptions o)
    {
        o.User.RequireUniqueEmail = true; // checked through the tenant-filtered store => unique per tenant
        o.Password.RequiredLength = 8;
        o.Password.RequireNonAlphanumeric = false;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        o.SignIn.RequireConfirmedEmail = false; // customer email verification comes with Phase 3
    }

    /// <summary>EF stores + tenant-aware user store + claims factory + token providers.</summary>
    public static IdentityBuilder AddAppStores(this IdentityBuilder builder) =>
        builder
            .AddEntityFrameworkStores<AppDbContext>()
            .AddUserStore<AppUserStore>()
            .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>()
            .AddDefaultTokenProviders();
}

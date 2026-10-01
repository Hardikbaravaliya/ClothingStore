using ClothingStore.Core.Common;
using ClothingStore.Core.Entities;
using ClothingStore.Core.Enums;
using ClothingStore.Core.Tenancy;
using ClothingStore.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClothingStore.Infrastructure.Data.Seed;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public bool Enabled { get; set; }
    public SeedUser? SuperAdmin { get; set; }

    /// <summary>Creates shop1 / shop2 demo stores with an admin user and categories (local testing).</summary>
    public bool DemoTenants { get; set; }
    public string? DemoAdminPassword { get; set; }

    public sealed class SeedUser
    {
        public string Email { get; set; } = default!;
        public string Password { get; set; } = default!;
    }
}

/// <summary>Applies migrations and seeds roles, plans, super admin and demo stores. Idempotent.</summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var options = sp.GetRequiredService<IOptions<SeedOptions>>().Value;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DbSeeder));

        await db.Database.MigrateAsync(ct);
        if (!options.Enabled)
            return;

        var tenantProvider = sp.GetRequiredService<ITenantProvider>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = sp.GetRequiredService<RoleManager<ApplicationRole>>();

        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new ApplicationRole(role));
        }

        var trialPlan = await db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Name == "Trial", ct);
        if (trialPlan is null)
        {
            trialPlan = new SubscriptionPlan { Name = "Trial", Price = 0, DurationDays = 14, MaxProducts = 50, MaxStaffUsers = 2, MaxOrdersPerMonth = 100 };
            db.SubscriptionPlans.Add(trialPlan);
            await db.SaveChangesAsync(ct);
        }

        if (options.SuperAdmin is { Email: { Length: > 0 }, Password: { Length: > 0 } } superAdmin)
        {
            tenantProvider.SetTenant(null);
            await EnsureUserAsync(userManager, logger, null, superAdmin.Email, superAdmin.Password, "Super Admin", AppRoles.SuperAdmin);
        }

        if (options.DemoTenants && !string.IsNullOrEmpty(options.DemoAdminPassword))
        {
            await EnsureDemoTenantAsync(db, tenantProvider, userManager, logger, trialPlan, "shop1", "Little Stars Kids Wear", options.DemoAdminPassword, ct);
            await EnsureDemoTenantAsync(db, tenantProvider, userManager, logger, trialPlan, "shop2", "Tiny Trends", options.DemoAdminPassword, ct);
        }

        tenantProvider.SetTenant(null);
    }

    private static async Task EnsureDemoTenantAsync(
        AppDbContext db, ITenantProvider tenantProvider, UserManager<ApplicationUser> userManager, ILogger logger,
        SubscriptionPlan plan, string slug, string name, string adminPassword, CancellationToken ct)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug, ct);
        if (tenant is null)
        {
            var now = DateTime.UtcNow;
            tenant = new Tenant
            {
                Name = name,
                Slug = slug,
                Status = TenantStatus.Trial,
                PlanId = plan.Id,
                Settings = new TenantSettings(),
            };
            tenant.Subscriptions.Add(new TenantSubscription
            {
                PlanId = plan.Id,
                StartDate = now,
                EndDate = now.AddDays(plan.DurationDays),
                Amount = plan.Price,
            });
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded demo tenant {Slug}", slug);
        }

        tenantProvider.SetTenant(tenant.Id);

        await EnsureUserAsync(userManager, logger, tenant.Id, $"admin@{slug}.local", adminPassword, $"{name} Admin", AppRoles.TenantAdmin);

        if (!await db.Categories.AnyAsync(ct))
        {
            var girls = new Category { Name = "Girls Wear", Slug = "girls-wear", SortOrder = 1 };
            var boys = new Category { Name = "Boys Wear", Slug = "boys-wear", SortOrder = 2 };
            db.Categories.AddRange(
                girls, boys,
                new Category { Name = "Frocks", Slug = "frocks", Parent = girls, SortOrder = 1 },
                new Category { Name = "Tops", Slug = "girls-tops", Parent = girls, SortOrder = 2 },
                new Category { Name = "Leggings", Slug = "leggings", Parent = girls, SortOrder = 3 },
                new Category { Name = "T-Shirts", Slug = "boys-t-shirts", Parent = boys, SortOrder = 1 },
                new Category { Name = "Shirts", Slug = "boys-shirts", Parent = boys, SortOrder = 2 },
                new Category { Name = "Jeans", Slug = "boys-jeans", Parent = boys, SortOrder = 3 });
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager, ILogger logger,
        Guid? tenantId, string email, string password, string fullName, string role)
    {
        // FindByEmail is tenant-filtered: the caller must have set the tenant first
        if (await userManager.FindByEmailAsync(email) is not null)
            return;

        var user = new ApplicationUser
        {
            TenantId = tenantId,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName,
            CreatedAt = DateTime.UtcNow,
        };

        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
            result = await userManager.AddToRoleAsync(user, role);

        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Seeding user {email} failed: {string.Join("; ", result.Errors.Select(e => e.Description))}");

        logger.LogInformation("Seeded user {Email} ({Role})", email, role);
    }
}

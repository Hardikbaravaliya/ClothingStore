using ClothingStore.Core.Common;
using ClothingStore.Infrastructure;
using ClothingStore.Infrastructure.Data.Seed;
using ClothingStore.Infrastructure.Identity;
using ClothingStore.Infrastructure.Storage;
using ClothingStore.Infrastructure.Tenancy;
using ClothingStore.Manager;
using ClothingStore.Services;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAppServices();
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.SectionName));

builder.Services
    .AddIdentity<ApplicationUser, ApplicationRole>(IdentitySetup.ConfigureOptions)
    .AddAppStores();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "cs.manager";
    o.LoginPath = "/Account/Login";
    o.LogoutPath = "/Account/Logout";
    o.AccessDeniedPath = "/Account/AccessDenied";
    o.ExpireTimeSpan = TimeSpan.FromHours(10);
    o.SlidingExpiration = true;
});

builder.Services.AddAuthorization(o =>
{
    // Every page needs a staff login unless marked [AllowAnonymous]
    o.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireRole(AppRoles.Staff)
        .Build();

    // Store pages (products, stock ...) need a store: SuperAdmin has none
    o.AddPolicy(Policies.StoreStaff, p => p
        .RequireRole(AppRoles.TenantAdmin, AppRoles.Manager)
        .RequireClaim(AppClaimTypes.TenantId));
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await DbSeeder.SeedAsync(app.Services);
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseLocalImageFiles(); // /uploads (product images)
app.UseRouting();

app.UseAuthentication();
app.UseMiddleware<ClaimsTenantMiddleware>();
app.UseAuthorization();

app.MapStaticAssets().AllowAnonymous();
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

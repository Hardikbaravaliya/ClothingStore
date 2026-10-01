using ClothingStore.Core.Common;
using ClothingStore.Infrastructure;
using ClothingStore.Infrastructure.Data.Seed;
using ClothingStore.Infrastructure.Identity;
using ClothingStore.Infrastructure.Tenancy;
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

// Every page needs a staff login unless marked [AllowAnonymous]
builder.Services.AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder()
    .RequireRole(AppRoles.Staff)
    .Build());

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

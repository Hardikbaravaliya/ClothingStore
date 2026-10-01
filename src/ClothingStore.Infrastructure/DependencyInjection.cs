using ClothingStore.Core.Tenancy;
using ClothingStore.Infrastructure.Data;
using ClothingStore.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClothingStore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

        services.AddDbContext<AppDbContext>(o => ConfigureDbContext(o, connectionString));

        services.AddMemoryCache();
        services.Configure<TenancyOptions>(configuration.GetSection(TenancyOptions.SectionName));
        services.AddScoped<ITenantProvider, TenantProvider>();
        services.AddScoped<ITenantResolver, TenantResolver>();

        return services;
    }

    /// <summary>Shared by DI, the design-time factory and tests.</summary>
    public static void ConfigureDbContext(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            // Expected: Identity child tables (UserRoles, Claims ...) point at the tenant-filtered Users table
            .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
}

using ClothingStore.Core.Interfaces;
using ClothingStore.Core.Tenancy;
using ClothingStore.Infrastructure.Data;
using ClothingStore.Infrastructure.Notifications;
using ClothingStore.Infrastructure.Payments;
using ClothingStore.Infrastructure.Security;
using ClothingStore.Infrastructure.Storage;
using ClothingStore.Infrastructure.Tenancy;
using Microsoft.AspNetCore.DataProtection;
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

        services.AddImageStorage(configuration);
        services.AddSharedDataProtection(configuration);
        services.AddEmail(configuration);

        services.AddHttpClient<IPaymentGateway, RazorpayGateway>(c =>
        {
            c.BaseAddress = new Uri(RazorpayGateway.BaseUrl);
            c.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }

    /// <summary>Shared by DI, the design-time factory and tests.</summary>
    public static void ConfigureDbContext(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            // Expected: Identity child tables (UserRoles, Claims ...) point at the tenant-filtered Users table
            .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));

    /// <summary>Same key ring + app name in Manager and Api, so secrets encrypted by one are readable by the other.</summary>
    private static void AddSharedDataProtection(this IServiceCollection services, IConfiguration configuration)
    {
        var keysPath = configuration[$"{DataProtectionOptionsConfig.SectionName}:{nameof(DataProtectionOptionsConfig.KeysPath)}"]
            ?? throw new InvalidOperationException("DataProtection:KeysPath is missing.");

        services.AddDataProtection()
            .SetApplicationName("ClothingStore")
            .PersistKeysToFileSystem(Directory.CreateDirectory(keysPath));
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
    }

    private static void AddEmail(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(EmailOptions.SectionName);
        services.Configure<EmailOptions>(section);

        if (string.Equals(section[nameof(EmailOptions.Provider)], "Smtp", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        else
            services.AddSingleton<IEmailSender, LogEmailSender>();
    }
}

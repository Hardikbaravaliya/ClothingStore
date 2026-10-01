using ClothingStore.Services.Store;
using Microsoft.Extensions.DependencyInjection;

namespace ClothingStore.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddAppServices(this IServiceCollection services)
    {
        services.AddScoped<IStoreSettingsService, StoreSettingsService>();
        return services;
    }
}

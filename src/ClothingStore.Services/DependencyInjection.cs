using ClothingStore.Services.Inventory;
using ClothingStore.Services.Products;
using ClothingStore.Services.Purchases;
using ClothingStore.Services.Store;
using Microsoft.Extensions.DependencyInjection;

namespace ClothingStore.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddAppServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        // Store / tenant locale
        services.AddScoped<IStoreSettingsService, StoreSettingsService>();
        services.AddScoped<ITenantLocaleAccessor, TenantLocaleAccessor>();
        services.AddScoped<ITenantClock, TenantClock>();
        services.AddScoped<IMoneyFormatter, MoneyFormatter>();

        // Catalog, purchases, stock
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IPurchaseService, PurchaseService>();
        services.AddScoped<IStockService, StockService>();

        return services;
    }
}

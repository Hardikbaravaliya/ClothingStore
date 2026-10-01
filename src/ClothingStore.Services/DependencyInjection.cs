using ClothingStore.Services.Inventory;
using ClothingStore.Services.Orders;
using ClothingStore.Services.Products;
using ClothingStore.Services.Purchases;
using ClothingStore.Services.Store;
using ClothingStore.Services.Storefront;
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

        // Manager: catalog, purchases, stock
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IPurchaseService, PurchaseService>();
        services.AddScoped<IStockService, StockService>();

        // Storefront (Api): catalog, customers, cart, orders, payments
        services.AddScoped<IStorefrontCatalogService, StorefrontCatalogService>();
        services.AddScoped<ICustomerAccountService, CustomerAccountService>();
        services.AddScoped<IAddressService, AddressService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<ICheckoutService, CheckoutService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ICustomerOrderService, CustomerOrderService>();

        return services;
    }
}

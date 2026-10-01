using ClothingStore.Core.Entities;
using ClothingStore.Core.Enums;
using ClothingStore.Core.Tenancy;
using ClothingStore.Infrastructure.Data;
using ClothingStore.Services.Inventory;
using ClothingStore.Services.Purchases;
using ClothingStore.Services.Store;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ClothingStore.Services.Tests.Inventory;

public class WeightedAverageTests
{
    [Theory]
    [InlineData(0, 0, 10, 100, 100)]       // first purchase => its cost
    [InlineData(10, 100, 10, 120, 110)]    // equal qty => midpoint
    [InlineData(5, 80, 3, 90, 83.75)]      // (400 + 270) / 8
    [InlineData(-2, 50, 4, 60, 60)]        // negative/zero old stock is ignored
    public void Calculates_weighted_average_cost(int oldQty, decimal oldAvg, int newQty, decimal newCost, decimal expected) =>
        Assert.Equal(expected, StockService.WeightedAverage(oldQty, oldAvg, newQty, newCost));
}

public sealed class StockAndPurchaseTests : IAsyncLifetime
{
    private readonly TestDatabase _database = new();
    private int _tenantA;
    private int _tenantB;
    private int _supplierId;
    private int _variantId;
    private int _otherTenantVariantId;

    public async Task InitializeAsync()
    {
        await _database.MigrateAsync();
        _tenantA = await _database.AddTenantAsync("store-a");
        _tenantB = await _database.AddTenantAsync("store-b");

        (_supplierId, _variantId) = await SeedCatalogAsync(_tenantA);
        (_, _otherTenantVariantId) = await SeedCatalogAsync(_tenantB);
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Purchase_adds_stock_updates_average_cost_and_writes_ledger()
    {
        var first = await CreatePurchaseAsync(_variantId, quantity: 10, cost: 100m);
        var second = await CreatePurchaseAsync(_variantId, quantity: 10, cost: 120m);

        Assert.True(first.Succeeded, first.Error);
        Assert.True(second.Succeeded, second.Error);

        await using var db = _database.CreateContext(_tenantA);
        var variant = await db.ProductVariants.SingleAsync(v => v.Id == _variantId);
        Assert.Equal(20, variant.StockQty);
        Assert.Equal(110m, variant.AvgCostPrice);

        var ledger = await db.StockTransactions.Where(t => t.ProductVariantId == _variantId).OrderBy(t => t.Id).ToListAsync();
        Assert.Collection(ledger,
            t => { Assert.Equal(StockTxnType.In, t.Type); Assert.Equal(10, t.Quantity); Assert.Equal(10, t.BalanceAfter); Assert.Equal(first.Value, t.ReferenceId); },
            t => { Assert.Equal(StockTxnType.In, t.Type); Assert.Equal(10, t.Quantity); Assert.Equal(20, t.BalanceAfter); Assert.Equal(second.Value, t.ReferenceId); });

        var purchase = await db.Purchases.Include(p => p.Items).SingleAsync(p => p.Id == second.Value);
        Assert.Equal(1200m, purchase.SubTotal);
        Assert.Equal(1200m, purchase.TotalAmount);
        Assert.Equal("INR", purchase.CurrencyCode);
    }

    [Fact]
    public async Task Purchase_with_another_tenants_variant_is_rejected()
    {
        var result = await CreatePurchaseAsync(_otherTenantVariantId, quantity: 5, cost: 50m);

        Assert.False(result.Succeeded);
        await using var dbB = _database.CreateContext(_tenantB);
        Assert.Equal(0, (await dbB.ProductVariants.SingleAsync(v => v.Id == _otherTenantVariantId)).StockQty);
    }

    [Fact]
    public async Task Purchase_dated_in_the_future_is_rejected()
    {
        var result = await CreatePurchaseAsync(_variantId, quantity: 1, cost: 10m, date: DateTime.UtcNow.AddDays(3));

        Assert.False(result.Succeeded);
        await using var db = _database.CreateContext(_tenantA);
        Assert.False(await db.Purchases.AnyAsync());
    }

    [Fact]
    public async Task Adjust_sets_actual_quantity_and_records_signed_change()
    {
        await CreatePurchaseAsync(_variantId, quantity: 10, cost: 100m);

        await using (var db = _database.CreateContext(_tenantA))
        {
            var result = await new StockService(db).AdjustAsync(new StockAdjustForm { VariantId = _variantId, NewQuantity = 7, Reason = "3 damaged" });
            Assert.True(result.Succeeded, result.Error);
        }

        await using var check = _database.CreateContext(_tenantA);
        Assert.Equal(7, (await check.ProductVariants.SingleAsync(v => v.Id == _variantId)).StockQty);
        var adjust = await check.StockTransactions.SingleAsync(t => t.Type == StockTxnType.Adjust);
        Assert.Equal(-3, adjust.Quantity);
        Assert.Equal(7, adjust.BalanceAfter);
        Assert.Equal("3 damaged", adjust.Notes);
    }

    [Fact]
    public async Task Adjust_below_zero_is_rejected()
    {
        await using var db = _database.CreateContext(_tenantA);
        var result = await new StockService(db).AdjustAsync(new StockAdjustForm { VariantId = _variantId, NewQuantity = -1, Reason = "x" });

        Assert.False(result.Succeeded);
    }

    private async Task<Core.Common.Result<int>> CreatePurchaseAsync(int variantId, int quantity, decimal cost, DateTime? date = null)
    {
        await using var db = _database.CreateContext(_tenantA, out var tenantProvider);
        var service = CreatePurchaseService(db, tenantProvider);
        return await service.CreateAsync(new PurchaseForm
        {
            SupplierId = _supplierId,
            PurchaseDate = date ?? DateTime.UtcNow.Date.AddDays(-1),
            InvoiceNo = "INV-1",
            Items = [new PurchaseItemForm { ProductVariantId = variantId, Quantity = quantity, CostPrice = cost }],
        });
    }

    private static PurchaseService CreatePurchaseService(AppDbContext db, ITenantProvider tenantProvider)
    {
        var locale = new TenantLocaleAccessor(db, tenantProvider, new MemoryCache(new MemoryCacheOptions()));
        var clock = new TenantClock(locale, TimeProvider.System);
        return new PurchaseService(db, new StockService(db), clock, locale);
    }

    private async Task<(int SupplierId, int VariantId)> SeedCatalogAsync(int tenantId)
    {
        await using var db = _database.CreateContext(tenantId);
        var category = new Category { Name = "Girls Wear", Slug = "girls-wear" };
        var product = new Product { Name = "Party Frock", Slug = "party-frock", Category = category, Mrp = 999, SellingPrice = 799 };
        var variant = new ProductVariant { Product = product, Size = "2-3Y", Color = "Red", Sku = "PF-2-3Y-RED", SellingPrice = 799 };
        var supplier = new Supplier { Name = "Surat Textiles" };
        db.AddRange(category, product, variant, supplier);
        await db.SaveChangesAsync();
        return (supplier.Id, variant.Id);
    }
}

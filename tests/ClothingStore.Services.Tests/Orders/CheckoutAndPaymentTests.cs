using ClothingStore.Contracts.Account;
using ClothingStore.Contracts.Cart;
using ClothingStore.Contracts.Orders;
using ClothingStore.Core.Entities;
using ClothingStore.Core.Enums;
using ClothingStore.Core.Interfaces;
using ClothingStore.Core.Tenancy;
using ClothingStore.Infrastructure.Data;
using ClothingStore.Infrastructure.Payments;
using ClothingStore.Services.Inventory;
using ClothingStore.Services.Orders;
using ClothingStore.Services.Store;
using ClothingStore.Services.Storefront;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClothingStore.Services.Tests.Orders;

public class RazorpaySignatureTests
{
    private readonly RazorpayGateway _gateway = new(new HttpClient());

    [Fact]
    public void Valid_payment_signature_is_accepted_and_tampered_one_rejected()
    {
        var signature = RazorpayGateway.ComputeSignature("secret", "order_1|pay_1");

        Assert.True(_gateway.VerifyPaymentSignature("secret", "order_1", "pay_1", signature));
        Assert.False(_gateway.VerifyPaymentSignature("secret", "order_1", "pay_2", signature));
        Assert.False(_gateway.VerifyPaymentSignature("other-secret", "order_1", "pay_1", signature));
        Assert.False(_gateway.VerifyPaymentSignature("secret", "order_1", "pay_1", ""));
    }
}

public sealed class CheckoutAndPaymentTests : IAsyncLifetime
{
    private const string KeySecret = "test_key_secret";
    private const string WebhookSecret = "test_webhook_secret";

    private readonly TestDatabase _database = new();
    private int _tenantId;
    private int _customerId;
    private int _variantId;

    public async Task InitializeAsync()
    {
        await _database.MigrateAsync();
        _tenantId = await _database.AddTenantAsync("store-a");

        await using var db = _database.CreateContext(_tenantId);
        var user = new Infrastructure.Identity.ApplicationUser
        {
            TenantId = _tenantId, UserName = "c@x.com", NormalizedUserName = "C@X.COM", Email = "c@x.com",
            NormalizedEmail = "C@X.COM", FullName = "Test Customer", SecurityStamp = "s", CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var customer = new Customer { UserId = user.Id, FullName = "Test Customer", Email = "c@x.com" };
        var product = new Product
        {
            Name = "Party Frock", Slug = "party-frock", Mrp = 999, SellingPrice = 799,
            Category = new Category { Name = "Girls Wear", Slug = "girls-wear" },
        };
        var variant = new ProductVariant { Product = product, Size = "2-3Y", Color = "Red", Sku = "PF-RED", SellingPrice = 799, StockQty = 5, AvgCostPrice = 300 };
        db.AddRange(customer, product, variant);

        var settings = await db.TenantSettings.SingleAsync(s => s.TenantId == _tenantId);
        settings.RazorpayKeyId = "rzp_test_key";
        settings.RazorpayKeySecretEncrypted = KeySecret;          // PlainProtector below: stored as-is
        settings.RazorpayWebhookSecretEncrypted = WebhookSecret;
        await db.SaveChangesAsync();

        _customerId = customer.Id;
        _variantId = variant.Id;
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Cod_order_is_confirmed_reduces_stock_writes_ledger_and_clears_cart()
    {
        await AddToCartAsync(2);

        var result = await PlaceAsync(PaymentMethods.Cod);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("Confirmed", result.Value!.Status);
        Assert.Null(result.Value.Razorpay);

        await using var db = _database.CreateContext(_tenantId);
        Assert.Equal(3, (await db.ProductVariants.SingleAsync()).StockQty);
        var order = await db.Orders.Include(o => o.Items).Include(o => o.Payments).SingleAsync();
        Assert.Equal(1598m, order.TotalAmount);
        Assert.Equal(300m, order.Items.Single().CostPrice); // cost snapshot for profit
        Assert.Equal(PaymentStatus.Pending, order.Payments.Single().Status); // COD: paid at delivery
        var ledger = await db.StockTransactions.SingleAsync();
        Assert.Equal(StockTxnType.Out, ledger.Type);
        Assert.Equal(-2, ledger.Quantity);
        Assert.False(await db.Carts.AnyAsync());
    }

    [Fact]
    public async Task Order_is_rejected_when_stock_is_lower_than_cart_quantity()
    {
        await AddToCartAsync(3);
        await using (var db = _database.CreateContext(_tenantId))
        {
            (await db.ProductVariants.SingleAsync()).StockQty = 1;
            await db.SaveChangesAsync();
        }

        var result = await PlaceAsync(PaymentMethods.Cod);

        Assert.False(result.Succeeded);
        await using var check = _database.CreateContext(_tenantId);
        Assert.False(await check.Orders.AnyAsync());
        Assert.Equal(1, (await check.ProductVariants.SingleAsync()).StockQty);
    }

    [Fact]
    public async Task Online_payment_confirms_once_even_if_verified_twice_and_webhook_arrives()
    {
        await AddToCartAsync(2);
        var placed = await PlaceAsync(PaymentMethods.Online);
        Assert.True(placed.Succeeded, placed.Error);
        Assert.Equal("Pending", placed.Value!.Status);
        var checkout = placed.Value.Razorpay!;
        Assert.Equal(159800, checkout.AmountInPaise);

        // Stock is not touched until payment
        await using (var db = _database.CreateContext(_tenantId))
            Assert.Equal(5, (await db.ProductVariants.SingleAsync()).StockQty);

        var verify = new VerifyPaymentRequest
        {
            OrderNo = placed.Value.OrderNo,
            RazorpayOrderId = checkout.RazorpayOrderId,
            RazorpayPaymentId = "pay_123",
            RazorpaySignature = RazorpayGateway.ComputeSignature(KeySecret, $"{checkout.RazorpayOrderId}|pay_123"),
        };
        Assert.True((await VerifyAsync(verify)).Succeeded);
        Assert.True((await VerifyAsync(verify)).Succeeded); // browser retried

        var webhookBody = System.Text.Json.JsonSerializer.Serialize(new
        {
            @event = "payment.captured",
            payload = new { payment = new { entity = new { id = "pay_123", order_id = checkout.RazorpayOrderId, amount = 159800 } } },
        });
        Assert.True((await WebhookAsync(webhookBody, RazorpayGateway.ComputeSignature(WebhookSecret, webhookBody))).Succeeded);

        await using var check = _database.CreateContext(_tenantId);
        var order = await check.Orders.Include(o => o.Payments).SingleAsync();
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal("pay_123", order.Payments.Single().RazorpayPaymentId);
        Assert.Equal(3, (await check.ProductVariants.SingleAsync()).StockQty); // deducted exactly once
        Assert.Single(await check.StockTransactions.ToListAsync());
        Assert.False(await check.Carts.AnyAsync());
    }

    [Fact]
    public async Task Wrong_signature_does_not_confirm_the_order()
    {
        await AddToCartAsync(1);
        var placed = await PlaceAsync(PaymentMethods.Online);

        var result = await VerifyAsync(new VerifyPaymentRequest
        {
            OrderNo = placed.Value!.OrderNo,
            RazorpayOrderId = placed.Value.Razorpay!.RazorpayOrderId,
            RazorpayPaymentId = "pay_999",
            RazorpaySignature = "forged",
        });
        var webhook = await WebhookAsync("""{"event":"payment.captured"}""", "forged");

        Assert.False(result.Succeeded);
        Assert.False(webhook.Succeeded);
        await using var check = _database.CreateContext(_tenantId);
        Assert.Equal(OrderStatus.Pending, (await check.Orders.SingleAsync()).Status);
        Assert.Equal(5, (await check.ProductVariants.SingleAsync()).StockQty);
    }

    // ---- helpers ----

    private async Task AddToCartAsync(int quantity)
    {
        await using var db = _database.CreateContext(_tenantId);
        var result = await new CartService(db, new NoImages()).AddItemAsync(
            new CartOwner(_customerId, null), new AddCartItemRequest { VariantId = _variantId, Quantity = quantity });
        Assert.True(result.Succeeded, result.Error);
    }

    private async Task<Core.Common.Result<PlaceOrderResponse>> PlaceAsync(string method)
    {
        await using var db = _database.CreateContext(_tenantId, out var tenant);
        var (settings, locale, clock) = CreateStoreServices(db, tenant);
        var service = new CheckoutService(db, new AddressService(db), new StockService(db), settings, new FakeGateway(), locale, clock,
            NullLogger<CheckoutService>.Instance);

        return await service.PlaceOrderAsync(_customerId, new PlaceOrderRequest
        {
            PaymentMethod = method,
            NewAddress = new AddressRequest { FullName = "Test", Phone = "9999999999", Line1 = "1 Main Road", City = "Surat", State = "Gujarat", Pincode = "395001" },
        });
    }

    private async Task<Core.Common.Result> VerifyAsync(VerifyPaymentRequest request)
    {
        await using var db = _database.CreateContext(_tenantId, out var tenant);
        return await CreatePaymentService(db, tenant).VerifyAsync(_customerId, request);
    }

    private async Task<Core.Common.Result> WebhookAsync(string body, string signature)
    {
        await using var db = _database.CreateContext(_tenantId, out var tenant);
        return await CreatePaymentService(db, tenant).HandleWebhookAsync(body, signature);
    }

    private static PaymentService CreatePaymentService(AppDbContext db, ITenantProvider tenant) =>
        new(db, new StockService(db), CreateStoreServices(db, tenant).Settings, new RazorpayGateway(new HttpClient()), NullLogger<PaymentService>.Instance);

    private static (StoreSettingsService Settings, TenantLocaleAccessor Locale, TenantClock Clock) CreateStoreServices(AppDbContext db, ITenantProvider tenant)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var locale = new TenantLocaleAccessor(db, tenant, cache);
        return (new StoreSettingsService(db, tenant, new PlainProtector(), new NoImages(), cache), locale, new TenantClock(locale, TimeProvider.System));
    }

    /// <summary>Creates Razorpay orders without calling Razorpay; signatures use the real algorithm.</summary>
    private sealed class FakeGateway : IPaymentGateway
    {
        public Task<string> CreateOrderAsync(PaymentGatewayCredentials credentials, long amountInPaise, string currency, string receipt, CancellationToken ct = default) =>
            Task.FromResult("order_" + receipt);

        public bool VerifyPaymentSignature(string keySecret, string gatewayOrderId, string paymentId, string signature) =>
            new RazorpayGateway(new HttpClient()).VerifyPaymentSignature(keySecret, gatewayOrderId, paymentId, signature);

        public bool VerifyWebhookSignature(string webhookSecret, string rawBody, string signature) =>
            new RazorpayGateway(new HttpClient()).VerifyWebhookSignature(webhookSecret, rawBody, signature);
    }

    private sealed class PlainProtector : ISecretProtector
    {
        public string Protect(string plainText) => plainText;
        public string? Unprotect(string protectedText) => protectedText;
    }

    private sealed class NoImages : IImageStorage
    {
        public Task<string> SaveAsync(Stream content, string folder, string extension, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(string relativePath, CancellationToken ct = default) => Task.CompletedTask;
        public string GetUrl(string relativePath) => "/uploads/" + relativePath;
    }
}

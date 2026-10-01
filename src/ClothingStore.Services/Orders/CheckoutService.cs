using System.Security.Cryptography;
using ClothingStore.Contracts.Orders;
using ClothingStore.Core.Common;
using ClothingStore.Core.Entities;
using ClothingStore.Core.Enums;
using ClothingStore.Core.Interfaces;
using ClothingStore.Infrastructure.Data;
using ClothingStore.Infrastructure.Payments;
using ClothingStore.Services.Inventory;
using ClothingStore.Services.Store;
using ClothingStore.Services.Storefront;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PaymentMethod = ClothingStore.Core.Enums.PaymentMethod;

namespace ClothingStore.Services.Orders;

public interface ICheckoutService
{
    /// <summary>
    /// Turns the customer's cart into an order. Prices and stock are re-checked here (never trusted from the browser).
    /// COD: confirmed + stock out at once. Online: Pending + Razorpay order; stock out after payment.
    /// </summary>
    Task<Result<PlaceOrderResponse>> PlaceOrderAsync(int customerId, PlaceOrderRequest request, CancellationToken ct = default);
}

public sealed class CheckoutService(
    AppDbContext db,
    IAddressService addressService,
    IStockService stockService,
    IStoreSettingsService storeSettings,
    IPaymentGateway paymentGateway,
    ITenantLocaleAccessor locale,
    ITenantClock clock,
    ILogger<CheckoutService> logger) : ICheckoutService
{
    private const int MaxConcurrencyRetries = 3;

    public async Task<Result<PlaceOrderResponse>> PlaceOrderAsync(int customerId, PlaceOrderRequest request, CancellationToken ct = default)
    {
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId && c.IsActive, ct);
        if (customer is null)
            return Result<PlaceOrderResponse>.Fail("Customer not found.");

        var isOnline = request.PaymentMethod == PaymentMethods.Online;
        var credentials = isOnline ? await storeSettings.GetPaymentCredentialsAsync(ct) : null;
        if (isOnline && credentials is null)
            return Result<PlaceOrderResponse>.Fail("Online payment is not available in this store. Please choose Cash on Delivery.");

        var address = await ResolveAddressAsync(customerId, request, ct);
        if (!address.Succeeded)
            return Result<PlaceOrderResponse>.Fail(address.Error!);

        Result<Order> placed;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                placed = await CreateOrderAsync(customer, address.Value!, request, isOnline, ct);
                break;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries)
            {
                db.ChangeTracker.Clear(); // stock changed meanwhile: re-read and try again
            }
        }

        if (!placed.Succeeded)
            return Result<PlaceOrderResponse>.Fail(placed.Error!);

        var order = placed.Value!;
        if (!isOnline)
            return Result<PlaceOrderResponse>.Ok(new PlaceOrderResponse(order.OrderNo, order.Status.ToString(), PaymentMethods.Cod, null));

        // Online: create the Razorpay order (outside the DB transaction – it is an HTTP call)
        var payment = order.Payments.Single();
        var amountInPaise = ToPaise(order.TotalAmount);
        try
        {
            payment.RazorpayOrderId = await paymentGateway.CreateOrderAsync(credentials!, amountInPaise, order.CurrencyCode, order.OrderNo, ct);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is PaymentGatewayException or HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "Could not create Razorpay order for {OrderNo}", order.OrderNo);
            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = "Payment could not be started.";
            order.Status = OrderStatus.Cancelled;
            order.CancelledAt = DateTime.UtcNow;
            order.CancelReason = "Online payment could not be started.";
            await db.SaveChangesAsync(CancellationToken.None);
            return Result<PlaceOrderResponse>.Fail("Online payment could not be started. Please try again or choose Cash on Delivery.");
        }

        var store = await storeSettings.GetCurrentAsync(ct);
        var checkout = new RazorpayCheckoutDto(credentials!.KeyId, payment.RazorpayOrderId, amountInPaise, order.CurrencyCode,
            store?.Name ?? "Store", customer.FullName, customer.Email, customer.Phone);
        return Result<PlaceOrderResponse>.Ok(new PlaceOrderResponse(order.OrderNo, order.Status.ToString(), PaymentMethods.Online, checkout));
    }

    public static long ToPaise(decimal amount) => (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

    private async Task<Result<Order>> CreateOrderAsync(Customer customer, Address address, PlaceOrderRequest request, bool isOnline, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var cart = await db.Carts
            .Include(c => c.Items).ThenInclude(i => i.ProductVariant).ThenInclude(v => v.Product).ThenInclude(p => p.Category)
            .FirstOrDefaultAsync(c => c.CustomerId == customer.Id, ct);
        if (cart is null || cart.Items.Count == 0)
            return Result<Order>.Fail("Your cart is empty.");

        foreach (var item in cart.Items)
        {
            var v = item.ProductVariant;
            if (!v.IsActive || !v.Product.IsActive || !v.Product.Category.IsActive)
                return Result<Order>.Fail($"{v.Product.Name} ({v.Size} / {v.Color}) is no longer available. Remove it from your cart.");
            if (v.StockQty < item.Quantity)
                return Result<Order>.Fail(v.StockQty <= 0
                    ? $"{v.Product.Name} ({v.Size} / {v.Color}) is out of stock. Remove it from your cart."
                    : $"Only {v.StockQty} of {v.Product.Name} ({v.Size} / {v.Color}) left. Update your cart.");
        }

        var order = new Order
        {
            OrderNo = await NewOrderNoAsync(ct),
            CustomerId = customer.Id,
            Status = OrderStatus.Pending,
            PaymentMethod = isOnline ? PaymentMethod.Online : PaymentMethod.Cod,
            PaymentStatus = PaymentStatus.Pending,
            CurrencyCode = locale.Current.CurrencyCode,
            ShippingName = address.FullName,
            ShippingPhone = address.Phone,
            ShippingLine1 = address.Line1,
            ShippingLine2 = string.IsNullOrEmpty(address.Landmark) ? address.Line2 : $"{address.Line2} {address.Landmark}".Trim(),
            ShippingCity = address.City,
            ShippingState = address.State,
            ShippingPincode = address.Pincode,
            ShippingCountry = address.Country,
            Notes = request.Notes?.Trim(),
        };

        foreach (var item in cart.Items)
        {
            var v = item.ProductVariant;
            order.Items.Add(new OrderItem
            {
                ProductVariantId = v.Id,
                ProductName = v.Product.Name,
                Size = v.Size,
                Color = v.Color,
                Sku = v.Sku,
                Quantity = item.Quantity,
                UnitPrice = v.SellingPrice,
                CostPrice = v.AvgCostPrice, // snapshot for profit
                LineTotal = v.SellingPrice * item.Quantity,
            });
        }

        // Prices include GST; shipping and coupons come in later phases
        order.SubTotal = order.Items.Sum(i => i.LineTotal);
        order.DiscountAmount = 0;
        order.ShippingCharge = 0;
        order.TaxAmount = 0;
        order.TotalAmount = order.SubTotal - order.DiscountAmount + order.ShippingCharge;

        order.Payments.Add(new Payment
        {
            Method = order.PaymentMethod,
            Status = PaymentStatus.Pending,
            Amount = order.TotalAmount,
            CurrencyCode = order.CurrencyCode,
        });

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct); // order.Id for the stock ledger

        if (!isOnline)
        {
            foreach (var item in cart.Items)
            {
                if (!stockService.TryRecordSale(item.ProductVariant, item.Quantity, order.Id, order.OrderNo))
                {
                    db.ChangeTracker.Clear(); // the transaction rolls back; drop the half-made order too
                    return Result<Order>.Fail("Stock changed while placing the order. Please check your cart.");
                }
            }

            order.Status = OrderStatus.Confirmed;
            order.ConfirmedAt = DateTime.UtcNow;
            db.Carts.Remove(cart);
        }
        // Online: the cart stays until the payment succeeds (so an abandoned payment loses nothing)

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result<Order>.Ok(order);
    }

    private async Task<Result<Address>> ResolveAddressAsync(int customerId, PlaceOrderRequest request, CancellationToken ct)
    {
        if (request.AddressId is { } addressId)
        {
            var saved = await db.Addresses.AsNoTracking().FirstOrDefaultAsync(a => a.Id == addressId && a.CustomerId == customerId, ct);
            return saved is null ? Result<Address>.Fail("Delivery address not found.") : Result<Address>.Ok(saved);
        }

        var a = request.NewAddress!;
        if (request.SaveNewAddress)
        {
            var added = await addressService.AddAsync(customerId, a, ct);
            if (!added.Succeeded)
                return Result<Address>.Fail(added.Error!);
        }

        return Result<Address>.Ok(new Address
        {
            FullName = a.FullName.Trim(),
            Phone = a.Phone.Trim(),
            Line1 = a.Line1.Trim(),
            Line2 = a.Line2?.Trim(),
            Landmark = a.Landmark?.Trim(),
            City = a.City.Trim(),
            State = a.State.Trim(),
            Pincode = a.Pincode.Trim(),
            Country = string.IsNullOrWhiteSpace(a.Country) ? "India" : a.Country.Trim(),
        });
    }

    /// <summary>"OD261001-48213": store-local date + 5 random digits, unique per store.</summary>
    private async Task<string> NewOrderNoAsync(CancellationToken ct)
    {
        var prefix = $"OD{clock.LocalNow:yyMMdd}-";
        while (true)
        {
            var orderNo = prefix + RandomNumberGenerator.GetInt32(10000, 100000);
            if (!await db.Orders.AnyAsync(o => o.OrderNo == orderNo, ct))
                return orderNo;
        }
    }
}

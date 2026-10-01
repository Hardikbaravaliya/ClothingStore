using ClothingStore.Contracts;
using ClothingStore.Contracts.Orders;
using ClothingStore.Core.Common;
using ClothingStore.Core.Enums;
using ClothingStore.Core.Interfaces;
using ClothingStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Services.Orders;

/// <summary>"My orders" for a customer. Only their own orders are ever returned.</summary>
public interface ICustomerOrderService
{
    Task<PagedResult<OrderSummaryDto>> GetOrdersAsync(int customerId, int page, CancellationToken ct = default);
    Task<OrderDetailDto?> GetOrderAsync(int customerId, string orderNo, CancellationToken ct = default);
}

public sealed class CustomerOrderService(AppDbContext db, IImageStorage imageStorage) : ICustomerOrderService
{
    public async Task<PagedResult<OrderSummaryDto>> GetOrdersAsync(int customerId, int page, CancellationToken ct = default)
    {
        const int pageSize = 10;
        page = Math.Max(page, 1);
        var orders = db.Orders.AsNoTracking().Where(o => o.CustomerId == customerId);

        var total = await orders.CountAsync(ct);
        var rows = await orders
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new { o.OrderNo, o.CreatedAt, o.Status, o.PaymentMethod, o.PaymentStatus, o.TotalAmount, o.CurrencyCode, Count = o.Items.Sum(i => i.Quantity) })
            .ToListAsync(ct);

        return new PagedResult<OrderSummaryDto>(rows.Select(o => new OrderSummaryDto(o.OrderNo, o.CreatedAt, o.Status.ToString(),
            o.PaymentMethod.ToString(), o.PaymentStatus.ToString(), o.TotalAmount, o.CurrencyCode, o.Count)).ToList(), page, pageSize, total);
    }

    public async Task<OrderDetailDto?> GetOrderAsync(int customerId, string orderNo, CancellationToken ct = default)
    {
        var o = await db.Orders.AsNoTracking()
            .Where(x => x.CustomerId == customerId && x.OrderNo == orderNo)
            .Select(x => new
            {
                Order = x,
                Items = x.Items.OrderBy(i => i.Id).Select(i => new
                {
                    i.ProductName, i.ProductVariant.Product.Slug, i.Size, i.Color, i.Quantity, i.UnitPrice, i.LineTotal,
                    ImagePath = i.ProductVariant.Product.Images.OrderByDescending(img => img.IsPrimary).ThenBy(img => img.SortOrder).Select(img => img.Path).FirstOrDefault(),
                }).ToList(),
                ShippedAt = x.Shipments.Where(s => s.ShippedAt != null).Min(s => s.ShippedAt),
                HasPendingOnlinePayment = x.Payments.Any(p => p.Method == PaymentMethod.Online && p.Status == PaymentStatus.Pending && p.RazorpayOrderId != null),
            })
            .FirstOrDefaultAsync(ct);
        if (o is null)
            return null;

        var order = o.Order;
        var timeline = new List<OrderEventDto> { new("Order placed", order.CreatedAt) };
        if (order.ConfirmedAt is { } confirmed) timeline.Add(new("Confirmed", confirmed));
        if (o.ShippedAt is { } shipped) timeline.Add(new("Shipped", shipped));
        if (order.DeliveredAt is { } delivered) timeline.Add(new("Delivered", delivered));
        if (order.CancelledAt is { } cancelled) timeline.Add(new("Cancelled", cancelled));

        return new OrderDetailDto(
            order.OrderNo, order.CreatedAt, order.Status.ToString(), order.PaymentMethod.ToString(), order.PaymentStatus.ToString(),
            order.SubTotal, order.DiscountAmount, order.ShippingCharge, order.TaxAmount, order.TotalAmount, order.CurrencyCode,
            order.Notes, order.CancelReason,
            new OrderAddressDto(order.ShippingName, order.ShippingPhone, order.ShippingLine1, order.ShippingLine2,
                order.ShippingCity, order.ShippingState, order.ShippingPincode, order.ShippingCountry),
            o.Items.Select(i => new OrderItemDto(i.ProductName, i.Slug, i.Size, i.Color,
                i.ImagePath is null ? null : imageStorage.GetUrl(i.ImagePath), i.Quantity, i.UnitPrice, i.LineTotal)).ToList(),
            timeline.OrderBy(t => t.AtUtc).ToList(),
            order.Status == OrderStatus.Pending && o.HasPendingOnlinePayment);
    }
}

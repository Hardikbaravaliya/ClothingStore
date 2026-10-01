using ClothingStore.Core.Entities;
using ClothingStore.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClothingStore.Infrastructure.Data.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(20);

        b.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();

        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Addresses).WithOne(x => x.Customer).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> b)
    {
        b.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(20).IsRequired();
        b.Property(x => x.Line1).HasMaxLength(250).IsRequired();
        b.Property(x => x.Line2).HasMaxLength(250);
        b.Property(x => x.Landmark).HasMaxLength(150);
        b.Property(x => x.City).HasMaxLength(100).IsRequired();
        b.Property(x => x.State).HasMaxLength(100).IsRequired();
        b.Property(x => x.Pincode).HasMaxLength(10).IsRequired();
        b.Property(x => x.Country).HasMaxLength(100).IsRequired();
    }
}

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> b)
    {
        b.Property(x => x.SessionKey).HasMaxLength(100);

        b.HasIndex(x => new { x.TenantId, x.CustomerId });
        b.HasIndex(x => new { x.TenantId, x.SessionKey });

        b.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
        b.HasMany(x => x.Items).WithOne(x => x.Cart).HasForeignKey(x => x.CartId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> b)
    {
        b.HasOne(x => x.ProductVariant).WithMany().HasForeignKey(x => x.ProductVariantId);
        b.HasIndex(x => new { x.CartId, x.ProductVariantId }).IsUnique();
    }
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.Property(x => x.OrderNo).HasMaxLength(30).IsRequired();
        b.Property(x => x.CurrencyCode).HasMaxLength(3).IsFixedLength().IsRequired();
        b.Property(x => x.CouponCode).HasMaxLength(50);

        b.Property(x => x.ShippingName).HasMaxLength(150).IsRequired();
        b.Property(x => x.ShippingPhone).HasMaxLength(20).IsRequired();
        b.Property(x => x.ShippingLine1).HasMaxLength(250).IsRequired();
        b.Property(x => x.ShippingLine2).HasMaxLength(250);
        b.Property(x => x.ShippingCity).HasMaxLength(100).IsRequired();
        b.Property(x => x.ShippingState).HasMaxLength(100).IsRequired();
        b.Property(x => x.ShippingPincode).HasMaxLength(10).IsRequired();
        b.Property(x => x.ShippingCountry).HasMaxLength(100).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Property(x => x.CancelReason).HasMaxLength(500);

        b.HasIndex(x => new { x.TenantId, x.OrderNo }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.CreatedAt });
        b.HasIndex(x => new { x.TenantId, x.Status });
        b.HasIndex(x => new { x.TenantId, x.CustomerId });

        b.HasOne(x => x.Customer).WithMany(x => x.Orders).HasForeignKey(x => x.CustomerId);
        b.HasMany(x => x.Items).WithOne(x => x.Order).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Payments).WithOne(x => x.Order).HasForeignKey(x => x.OrderId);
        b.HasMany(x => x.Shipments).WithOne(x => x.Order).HasForeignKey(x => x.OrderId);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Size).HasMaxLength(30).IsRequired();
        b.Property(x => x.Color).HasMaxLength(50).IsRequired();
        b.Property(x => x.Sku).HasMaxLength(64).IsRequired();

        b.HasOne(x => x.ProductVariant).WithMany().HasForeignKey(x => x.ProductVariantId);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.Property(x => x.CurrencyCode).HasMaxLength(3).IsFixedLength().IsRequired();
        b.Property(x => x.RazorpayOrderId).HasMaxLength(50);
        b.Property(x => x.RazorpayPaymentId).HasMaxLength(50);
        b.Property(x => x.RazorpaySignature).HasMaxLength(200);
        b.Property(x => x.RefundId).HasMaxLength(50);
        b.Property(x => x.FailureReason).HasMaxLength(500);

        b.HasIndex(x => new { x.TenantId, x.RazorpayOrderId });
        // Idempotency: the same Razorpay payment can never be recorded twice
        b.HasIndex(x => new { x.TenantId, x.RazorpayPaymentId }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.CreatedAt });
    }
}

public class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> b)
    {
        b.Property(x => x.Provider).HasMaxLength(30).IsRequired();
        b.Property(x => x.ProviderOrderId).HasMaxLength(50);
        b.Property(x => x.ProviderShipmentId).HasMaxLength(50);
        b.Property(x => x.CourierName).HasMaxLength(100);
        b.Property(x => x.Awb).HasMaxLength(50);
        b.Property(x => x.LabelUrl).HasMaxLength(1000);

        b.HasIndex(x => new { x.TenantId, x.Awb });
        b.HasMany(x => x.TrackingLogs).WithOne(x => x.Shipment).HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ShipmentTrackingLogConfiguration : IEntityTypeConfiguration<ShipmentTrackingLog>
{
    public void Configure(EntityTypeBuilder<ShipmentTrackingLog> b)
    {
        b.Property(x => x.Status).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.Location).HasMaxLength(200);

        b.HasIndex(x => new { x.ShipmentId, x.EventTime });
    }
}

public class ReturnRequestConfiguration : IEntityTypeConfiguration<ReturnRequest>
{
    public void Configure(EntityTypeBuilder<ReturnRequest> b)
    {
        b.ToTable("Returns");
        b.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        b.Property(x => x.AdminNotes).HasMaxLength(1000);

        b.HasIndex(x => new { x.TenantId, x.Status });

        b.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId);
        b.HasOne(x => x.OrderItem).WithMany().HasForeignKey(x => x.OrderItemId);
        b.HasOne(x => x.ExchangeVariant).WithMany().HasForeignKey(x => x.ExchangeVariantId);
    }
}

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> b)
    {
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);

        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}

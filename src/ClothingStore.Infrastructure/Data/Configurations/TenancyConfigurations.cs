using ClothingStore.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClothingStore.Infrastructure.Data.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(63).IsRequired(); // DNS label limit
        b.Property(x => x.CustomDomain).HasMaxLength(253);

        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => x.CustomDomain).IsUnique();

        b.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TenantSettingsConfiguration : IEntityTypeConfiguration<TenantSettings>
{
    public void Configure(EntityTypeBuilder<TenantSettings> b)
    {
        b.HasKey(x => x.TenantId);
        b.HasOne(x => x.Tenant).WithOne(x => x.Settings)
            .HasForeignKey<TenantSettings>(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Property(x => x.LogoPath).HasMaxLength(500);
        b.Property(x => x.ThemeColor).HasMaxLength(20);
        b.Property(x => x.GstNo).HasMaxLength(20);
        b.Property(x => x.ContactEmail).HasMaxLength(256);
        b.Property(x => x.ContactPhone).HasMaxLength(20);
        b.Property(x => x.Address).HasMaxLength(500);

        b.Property(x => x.TimeZoneId).HasMaxLength(64).IsRequired();
        b.Property(x => x.CurrencyCode).HasMaxLength(3).IsFixedLength().IsRequired();
        b.Property(x => x.CurrencySymbol).HasMaxLength(8).IsRequired();
        b.Property(x => x.CultureName).HasMaxLength(20).IsRequired();

        b.Property(x => x.RazorpayKeyId).HasMaxLength(100);
        b.Property(x => x.RazorpayKeySecretEncrypted).HasMaxLength(1000);
        b.Property(x => x.RazorpayWebhookSecretEncrypted).HasMaxLength(1000);
        b.Property(x => x.ShiprocketEmail).HasMaxLength(256);
        b.Property(x => x.ShiprocketPasswordEncrypted).HasMaxLength(1000);
        b.Property(x => x.ShiprocketPickupLocation).HasMaxLength(100);
    }
}

public class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> b)
    {
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
    }
}

public class TenantSubscriptionConfiguration : IEntityTypeConfiguration<TenantSubscription>
{
    public void Configure(EntityTypeBuilder<TenantSubscription> b)
    {
        b.Property(x => x.PaymentReference).HasMaxLength(100);
        b.HasOne(x => x.Tenant).WithMany(x => x.Subscriptions).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.StartDate });
    }
}

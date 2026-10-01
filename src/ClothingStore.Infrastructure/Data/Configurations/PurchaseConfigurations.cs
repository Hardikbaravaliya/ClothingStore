using ClothingStore.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClothingStore.Infrastructure.Data.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b)
    {
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.ContactPerson).HasMaxLength(100);
        b.Property(x => x.Phone).HasMaxLength(20);
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.GstNo).HasMaxLength(20);
        b.Property(x => x.Address).HasMaxLength(500);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.State).HasMaxLength(100);
        b.Property(x => x.Pincode).HasMaxLength(10);
        b.Property(x => x.Notes).HasMaxLength(1000);

        b.HasIndex(x => new { x.TenantId, x.Name });
    }
}

public class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> b)
    {
        b.Property(x => x.InvoiceNo).HasMaxLength(50);
        b.Property(x => x.CurrencyCode).HasMaxLength(3).IsFixedLength().IsRequired();
        b.Property(x => x.Notes).HasMaxLength(1000);

        b.HasIndex(x => new { x.TenantId, x.PurchaseDate });
        b.HasIndex(x => new { x.TenantId, x.SupplierId });

        b.HasOne(x => x.Supplier).WithMany(x => x.Purchases).HasForeignKey(x => x.SupplierId);
        b.HasMany(x => x.Items).WithOne(x => x.Purchase).HasForeignKey(x => x.PurchaseId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PurchaseItemConfiguration : IEntityTypeConfiguration<PurchaseItem>
{
    public void Configure(EntityTypeBuilder<PurchaseItem> b)
    {
        b.HasOne(x => x.ProductVariant).WithMany().HasForeignKey(x => x.ProductVariantId);
    }
}

public class StockTransactionConfiguration : IEntityTypeConfiguration<StockTransaction>
{
    public void Configure(EntityTypeBuilder<StockTransaction> b)
    {
        b.Property(x => x.ReferenceType).HasMaxLength(30);
        b.Property(x => x.Notes).HasMaxLength(500);

        b.HasIndex(x => new { x.TenantId, x.ProductVariantId, x.CreatedAt });
        b.HasIndex(x => new { x.TenantId, x.CreatedAt });

        b.HasOne(x => x.ProductVariant).WithMany().HasForeignKey(x => x.ProductVariantId);
    }
}

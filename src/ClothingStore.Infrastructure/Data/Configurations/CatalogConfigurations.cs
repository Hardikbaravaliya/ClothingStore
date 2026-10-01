using ClothingStore.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClothingStore.Infrastructure.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(120).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.ImagePath).HasMaxLength(500);

        b.HasIndex(x => new { x.TenantId, x.Slug }).IsUnique();
        b.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(220).IsRequired();
        b.Property(x => x.Brand).HasMaxLength(100);
        b.Property(x => x.Fabric).HasMaxLength(100);
        b.Property(x => x.AgeGroup).HasMaxLength(50);
        b.Property(x => x.MetaTitle).HasMaxLength(200);
        b.Property(x => x.MetaDescription).HasMaxLength(500);

        b.HasIndex(x => new { x.TenantId, x.Slug }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.CategoryId });

        b.HasOne(x => x.Category).WithMany(x => x.Products).HasForeignKey(x => x.CategoryId);
        b.HasMany(x => x.Images).WithOne(x => x.Product).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Variants).WithOne(x => x.Product).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> b)
    {
        b.Property(x => x.Path).HasMaxLength(500).IsRequired();
        b.Property(x => x.AltText).HasMaxLength(200);
    }
}

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> b)
    {
        b.Property(x => x.Size).HasMaxLength(30).IsRequired();
        b.Property(x => x.Color).HasMaxLength(50).IsRequired();
        b.Property(x => x.Sku).HasMaxLength(64).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasIndex(x => new { x.TenantId, x.Sku }).IsUnique();
        b.HasIndex(x => new { x.ProductId, x.Size, x.Color }).IsUnique();

        b.ToTable(t => t.HasCheckConstraint("CK_ProductVariants_StockQty", "[StockQty] >= 0"));
    }
}

using ClothingStore.Core.Tenancy;
using ClothingStore.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClothingStore.Infrastructure.Data.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.Property(x => x.FullName).HasMaxLength(150).IsRequired();

        b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);

        // Identity makes UserName globally unique. We need it unique per tenant instead.
        // HasFilter(null): SuperAdmins (TenantId NULL) are also unique among themselves.
        RemoveIndex(b, nameof(ApplicationUser.NormalizedUserName));
        RemoveIndex(b, nameof(ApplicationUser.NormalizedEmail));
        b.HasIndex(x => new { x.TenantId, x.NormalizedUserName }).IsUnique().HasFilter(null).HasDatabaseName("UserNameIndex");
        b.HasIndex(x => new { x.TenantId, x.NormalizedEmail }).IsUnique().HasFilter(null).HasDatabaseName("EmailIndex");
    }

    private static void RemoveIndex(EntityTypeBuilder<ApplicationUser> b, string propertyName)
    {
        var property = b.Metadata.FindProperty(propertyName)!;
        var index = b.Metadata.GetIndexes().SingleOrDefault(i => i.Properties.Count == 1 && i.Properties[0] == property);
        if (index is not null)
            b.Metadata.RemoveIndex(index);
    }
}

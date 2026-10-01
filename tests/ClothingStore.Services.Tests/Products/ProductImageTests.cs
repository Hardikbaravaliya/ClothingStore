using ClothingStore.Core.Entities;
using ClothingStore.Infrastructure.Storage;
using ClothingStore.Services.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClothingStore.Services.Tests.Products;

public sealed class ProductImageTests : IAsyncLifetime
{
    // Smallest valid PNG (1×1 transparent pixel)
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

    private readonly TestDatabase _database = new();
    private readonly string _uploadRoot = Path.Combine(Path.GetTempPath(), "cs-test-uploads-" + Guid.NewGuid().ToString("N"));
    private int _tenantId;
    private int _productId;

    public async Task InitializeAsync()
    {
        await _database.MigrateAsync();
        _tenantId = await _database.AddTenantAsync("store-a");

        await using var db = _database.CreateContext(_tenantId);
        var product = new Product
        {
            Name = "Party Frock",
            Slug = "party-frock",
            Category = new Category { Name = "Girls Wear", Slug = "girls-wear" },
            SellingPrice = 799,
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        _productId = product.Id;
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        if (Directory.Exists(_uploadRoot))
            Directory.Delete(_uploadRoot, recursive: true);
    }

    [Fact]
    public async Task Valid_png_is_saved_under_tenant_and_product_folder_and_becomes_primary()
    {
        await using var db = _database.CreateContext(_tenantId, out var tenantProvider);
        var service = new ProductService(db, CreateStorage(), tenantProvider);

        var result = await service.AddImagesAsync(_productId, [new ImageUpload(new MemoryStream(Png), "photo.png", Png.Length)]);

        Assert.True(result.Succeeded, result.Error);
        var image = await db.ProductImages.SingleAsync();
        Assert.True(image.IsPrimary);
        Assert.StartsWith($"{_tenantId}/{_productId}/", image.Path);
        Assert.EndsWith(".png", image.Path);
        Assert.True(File.Exists(Path.Combine(_uploadRoot, image.Path)));
    }

    [Fact]
    public async Task File_that_is_not_really_an_image_is_rejected()
    {
        var fake = "this is not an image"u8.ToArray();
        await using var db = _database.CreateContext(_tenantId, out var tenantProvider);
        var service = new ProductService(db, CreateStorage(), tenantProvider);

        var result = await service.AddImagesAsync(_productId, [new ImageUpload(new MemoryStream(fake), "photo.jpg", fake.Length)]);

        Assert.False(result.Succeeded);
        Assert.False(await db.ProductImages.AnyAsync());
        Assert.False(Directory.Exists(_uploadRoot) && Directory.EnumerateFiles(_uploadRoot, "*", SearchOption.AllDirectories).Any());
    }

    private LocalFileImageStorage CreateStorage() =>
        new(Options.Create(new StorageOptions { LocalPath = _uploadRoot, BaseUrl = "/uploads" }));
}

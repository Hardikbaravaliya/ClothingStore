using ClothingStore.Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace ClothingStore.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>"Local" now; "Azure" when going live.</summary>
    public string Provider { get; set; } = "Local";

    /// <summary>Folder outside the projects, shared by Manager and Api.</summary>
    public string LocalPath { get; set; } = default!;

    /// <summary>Public URL prefix of the files, e.g. "/uploads" or "https://localhost:5003/uploads".</summary>
    public string BaseUrl { get; set; } = "/uploads";
}

public sealed class LocalFileImageStorage(IOptions<StorageOptions> options) : IImageStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.LocalPath);
    private readonly string _baseUrl = options.Value.BaseUrl.TrimEnd('/');

    public async Task<string> SaveAsync(Stream content, string folder, string extension, CancellationToken ct = default)
    {
        var relativePath = $"{folder.Trim('/')}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var fullPath = ToFullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var file = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write);
        await content.CopyToAsync(file, ct);
        return relativePath;
    }

    public Task DeleteAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = ToFullPath(relativePath);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public string GetUrl(string relativePath) => $"{_baseUrl}/{relativePath}";

    private string ToFullPath(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Path is outside the storage folder.");
        return fullPath;
    }
}

public static class StorageSetup
{
    public static IServiceCollection AddImageStorage(this IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        var section = configuration.GetSection(StorageOptions.SectionName);
        services.Configure<StorageOptions>(section);

        var provider = section[nameof(StorageOptions.Provider)] ?? "Local";
        if (!provider.Equals("Local", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Storage provider '{provider}' is not implemented yet (Azure comes before going live).");

        services.AddSingleton<IImageStorage, LocalFileImageStorage>();
        return services;
    }

    /// <summary>Serves the local upload folder at /uploads (Local provider only).</summary>
    public static IApplicationBuilder UseLocalImageFiles(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetRequiredService<IOptions<StorageOptions>>().Value;
        if (!options.Provider.Equals("Local", StringComparison.OrdinalIgnoreCase))
            return app;

        Directory.CreateDirectory(options.LocalPath);
        return app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(Path.GetFullPath(options.LocalPath)),
            RequestPath = "/uploads",
        });
    }
}

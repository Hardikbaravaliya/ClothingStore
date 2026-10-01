namespace ClothingStore.Core.Interfaces;

/// <summary>
/// Image files. Local folder now, Azure Blob later (Section 13) – switched by config.
/// The DB stores only the relative path ("{TenantId}/{ProductId}/abc.jpg"); URL = BaseUrl + path.
/// </summary>
public interface IImageStorage
{
    /// <returns>Relative path of the saved file.</returns>
    Task<string> SaveAsync(Stream content, string folder, string extension, CancellationToken ct = default);

    Task DeleteAsync(string relativePath, CancellationToken ct = default);

    string GetUrl(string relativePath);
}

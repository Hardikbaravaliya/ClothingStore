using System.Net;

namespace ClothingStore.Catalog.ApiClients;

public sealed record StoreSettings(
    string Name,
    string Slug,
    string? LogoPath,
    string? ThemeColor,
    string? ContactEmail,
    string? ContactPhone,
    string CurrencyCode,
    string CurrencySymbol,
    string CultureName,
    string TimeZoneId);

public sealed class StoreApiClient(HttpClient http)
{
    /// <summary>null when the Api does not know this store (404).</summary>
    public async Task<StoreSettings?> GetSettingsAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/store/settings", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StoreSettings>(ct);
    }
}

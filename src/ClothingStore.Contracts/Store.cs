namespace ClothingStore.Contracts.Store;

/// <summary>Public store info: GET /api/store/settings.</summary>
public sealed record StoreSettingsDto(
    string Name,
    string Slug,
    string? LogoUrl,
    string? ThemeColor,
    string? ContactEmail,
    string? ContactPhone,
    string CurrencyCode,
    string CurrencySymbol,
    string CultureName,
    string TimeZoneId,
    bool OnlinePaymentEnabled);

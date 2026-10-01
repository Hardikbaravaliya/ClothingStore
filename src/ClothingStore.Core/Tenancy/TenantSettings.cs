namespace ClothingStore.Core.Tenancy;

/// <summary>Platform table, one row per tenant (PK = TenantId).</summary>
public class TenantSettings
{
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = default!;

    // Store branding / contact
    public string? LogoPath { get; set; }
    public string? ThemeColor { get; set; }
    public string? GstNo { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }

    // Locale (Section 4: TimeZone ane Currency)
    public string TimeZoneId { get; set; } = "Asia/Kolkata";
    public string CurrencyCode { get; set; } = "INR";
    public string CurrencySymbol { get; set; } = "₹";
    public string CultureName { get; set; } = "en-IN";

    // Razorpay (secrets stored encrypted)
    public string? RazorpayKeyId { get; set; }
    public string? RazorpayKeySecretEncrypted { get; set; }
    public string? RazorpayWebhookSecretEncrypted { get; set; }

    // Shiprocket (secrets stored encrypted)
    public string? ShiprocketEmail { get; set; }
    public string? ShiprocketPasswordEncrypted { get; set; }
    public string? ShiprocketPickupLocation { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

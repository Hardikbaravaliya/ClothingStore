using System.ComponentModel.DataAnnotations;
using System.Globalization;
using ClothingStore.Contracts.Store;
using ClothingStore.Core.Common;
using ClothingStore.Core.Interfaces;
using ClothingStore.Core.Tenancy;
using ClothingStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ClothingStore.Services.Store;

/// <summary>Manager "Store settings" form. Secrets are write-only: empty = keep the saved value.</summary>
public class StoreSettingsForm : IValidatableObject
{
    [Required, StringLength(150), Display(Name = "Store name")]
    public string StoreName { get; set; } = default!;

    [EmailAddress, StringLength(256), Display(Name = "Contact email")]
    public string? ContactEmail { get; set; }

    [Phone, StringLength(20), Display(Name = "Contact phone")]
    public string? ContactPhone { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(20), Display(Name = "GST no")]
    public string? GstNo { get; set; }

    [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Use a color like #d63384."), Display(Name = "Theme color")]
    public string? ThemeColor { get; set; }

    [Required, StringLength(64), Display(Name = "Time zone")]
    public string TimeZoneId { get; set; } = "Asia/Kolkata";

    [Required, RegularExpression("^[A-Z]{3}$", ErrorMessage = "3 letter code, e.g. INR."), Display(Name = "Currency code")]
    public string CurrencyCode { get; set; } = "INR";

    [Required, StringLength(8), Display(Name = "Currency symbol")]
    public string CurrencySymbol { get; set; } = "₹";

    [Required, StringLength(20), Display(Name = "Number/date format (culture)")]
    public string CultureName { get; set; } = "en-IN";

    [StringLength(100), Display(Name = "Razorpay Key Id")]
    public string? RazorpayKeyId { get; set; }

    [StringLength(200), DataType(DataType.Password), Display(Name = "Razorpay Key Secret")]
    public string? RazorpayKeySecret { get; set; }

    [StringLength(200), DataType(DataType.Password), Display(Name = "Razorpay Webhook Secret")]
    public string? RazorpayWebhookSecret { get; set; }

    // Read-only info for the page
    public bool HasRazorpayKeySecret { get; set; }
    public bool HasRazorpayWebhookSecret { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(TimeZoneId, out _))
            yield return new ValidationResult("Unknown time zone. Use an IANA id like Asia/Kolkata.", [nameof(TimeZoneId)]);

        CultureInfo? culture = null;
        try { culture = CultureInfo.GetCultureInfo(CultureName); } catch (CultureNotFoundException) { }
        if (culture is null || culture.IsNeutralCulture)
            yield return new ValidationResult("Unknown culture. Use one like en-IN or en-US.", [nameof(CultureName)]);
    }
}

public interface IStoreSettingsService
{
    /// <summary>Public info of the current store (Catalog / Manager dashboard).</summary>
    Task<StoreSettingsDto?> GetCurrentAsync(CancellationToken ct = default);

    Task<StoreSettingsForm?> GetFormAsync(CancellationToken ct = default);
    Task<Result> UpdateAsync(StoreSettingsForm form, CancellationToken ct = default);

    /// <summary>Decrypted Razorpay keys of the current store, or null when online payment is not set up.</summary>
    Task<PaymentGatewayCredentials?> GetPaymentCredentialsAsync(CancellationToken ct = default);
}

public sealed class StoreSettingsService(
    AppDbContext db,
    ITenantProvider tenantProvider,
    ISecretProtector secretProtector,
    IImageStorage imageStorage,
    IMemoryCache cache) : IStoreSettingsService
{
    public async Task<StoreSettingsDto?> GetCurrentAsync(CancellationToken ct = default)
    {
        if (tenantProvider.TenantId is not { } tenantId)
            return null;

        var s = await db.TenantSettings.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => new
            {
                x.Tenant.Name, x.Tenant.Slug, x.LogoPath, x.ThemeColor, x.ContactEmail, x.ContactPhone,
                x.CurrencyCode, x.CurrencySymbol, x.CultureName, x.TimeZoneId,
                OnlinePayment = x.RazorpayKeyId != null && x.RazorpayKeySecretEncrypted != null,
            })
            .FirstOrDefaultAsync(ct);

        return s is null
            ? null
            : new StoreSettingsDto(s.Name, s.Slug, s.LogoPath is null ? null : imageStorage.GetUrl(s.LogoPath), s.ThemeColor,
                s.ContactEmail, s.ContactPhone, s.CurrencyCode, s.CurrencySymbol, s.CultureName, s.TimeZoneId, s.OnlinePayment);
    }

    public async Task<StoreSettingsForm?> GetFormAsync(CancellationToken ct = default)
    {
        if (tenantProvider.TenantId is not { } tenantId)
            return null;

        return await db.TenantSettings.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .Select(s => new StoreSettingsForm
            {
                StoreName = s.Tenant.Name,
                ContactEmail = s.ContactEmail,
                ContactPhone = s.ContactPhone,
                Address = s.Address,
                GstNo = s.GstNo,
                ThemeColor = s.ThemeColor,
                TimeZoneId = s.TimeZoneId,
                CurrencyCode = s.CurrencyCode,
                CurrencySymbol = s.CurrencySymbol,
                CultureName = s.CultureName,
                RazorpayKeyId = s.RazorpayKeyId,
                HasRazorpayKeySecret = s.RazorpayKeySecretEncrypted != null,
                HasRazorpayWebhookSecret = s.RazorpayWebhookSecretEncrypted != null,
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Result> UpdateAsync(StoreSettingsForm form, CancellationToken ct = default)
    {
        if (tenantProvider.TenantId is not { } tenantId)
            return Result.Fail("No store selected.");

        var settings = await db.TenantSettings.Include(s => s.Tenant).FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
        if (settings is null)
            return Result.Fail("Store settings not found.");

        settings.Tenant.Name = form.StoreName.Trim();
        settings.ContactEmail = form.ContactEmail?.Trim();
        settings.ContactPhone = form.ContactPhone?.Trim();
        settings.Address = form.Address?.Trim();
        settings.GstNo = form.GstNo?.Trim().ToUpperInvariant();
        settings.ThemeColor = form.ThemeColor?.Trim();
        settings.TimeZoneId = form.TimeZoneId.Trim();
        settings.CurrencyCode = form.CurrencyCode.Trim().ToUpperInvariant();
        settings.CurrencySymbol = form.CurrencySymbol.Trim();
        settings.CultureName = form.CultureName.Trim();

        // Razorpay: an empty Key Id switches online payment off
        if (string.IsNullOrWhiteSpace(form.RazorpayKeyId))
        {
            settings.RazorpayKeyId = null;
            settings.RazorpayKeySecretEncrypted = null;
            settings.RazorpayWebhookSecretEncrypted = null;
        }
        else
        {
            settings.RazorpayKeyId = form.RazorpayKeyId.Trim();
            if (!string.IsNullOrWhiteSpace(form.RazorpayKeySecret))
                settings.RazorpayKeySecretEncrypted = secretProtector.Protect(form.RazorpayKeySecret.Trim());
            if (!string.IsNullOrWhiteSpace(form.RazorpayWebhookSecret))
                settings.RazorpayWebhookSecretEncrypted = secretProtector.Protect(form.RazorpayWebhookSecret.Trim());
            if (settings.RazorpayKeySecretEncrypted is null)
                return Result.Fail("Enter the Razorpay Key Secret together with the Key Id.");
        }

        settings.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        cache.Remove(TenantLocaleAccessor.CacheKey(tenantId));
        return Result.Ok();
    }

    public async Task<PaymentGatewayCredentials?> GetPaymentCredentialsAsync(CancellationToken ct = default)
    {
        if (tenantProvider.TenantId is not { } tenantId)
            return null;

        var s = await db.TenantSettings.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => new { x.RazorpayKeyId, x.RazorpayKeySecretEncrypted, x.RazorpayWebhookSecretEncrypted })
            .FirstOrDefaultAsync(ct);
        if (s?.RazorpayKeyId is null || s.RazorpayKeySecretEncrypted is null)
            return null;

        var secret = secretProtector.Unprotect(s.RazorpayKeySecretEncrypted);
        if (secret is null)
            return null;

        var webhookSecret = s.RazorpayWebhookSecretEncrypted is null ? null : secretProtector.Unprotect(s.RazorpayWebhookSecretEncrypted);
        return new PaymentGatewayCredentials(s.RazorpayKeyId, secret, webhookSecret);
    }
}

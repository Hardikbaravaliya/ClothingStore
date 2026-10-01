using ClothingStore.Core.Common;
using ClothingStore.Services.Store;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClothingStore.Manager.Controllers;

/// <summary>Store settings (contact, locale, Razorpay keys). Store owner (TenantAdmin) only.</summary>
[Authorize(Roles = AppRoles.TenantAdmin)]
public class SettingsController(IStoreSettingsService storeSettings) : StoreControllerBase
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var form = await storeSettings.GetFormAsync(ct);
        return form is null ? NotFound() : View(form);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(StoreSettingsForm form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await storeSettings.UpdateAsync(form, ct);
            if (result.Succeeded)
            {
                Success("Store settings saved.");
                return RedirectToAction(nameof(Index));
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }

        // Re-show which secrets are already saved (they are never sent back to the page)
        var saved = await storeSettings.GetFormAsync(ct);
        form.HasRazorpayKeySecret = saved?.HasRazorpayKeySecret ?? false;
        form.HasRazorpayWebhookSecret = saved?.HasRazorpayWebhookSecret ?? false;
        form.RazorpayKeySecret = null;
        form.RazorpayWebhookSecret = null;
        return View(form);
    }
}

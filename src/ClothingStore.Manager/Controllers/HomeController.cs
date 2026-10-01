using System.Diagnostics;
using ClothingStore.Manager.Models;
using ClothingStore.Services.Store;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClothingStore.Manager.Controllers;

public class HomeController(IStoreSettingsService storeSettings) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        // null for Super Admin (no tenant)
        var store = await storeSettings.GetCurrentAsync(ct);
        return View(store);
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

using System.Diagnostics;
using ClothingStore.Catalog.ApiClients;
using ClothingStore.Catalog.Models;
using Microsoft.AspNetCore.Mvc;

namespace ClothingStore.Catalog.Controllers;

public class HomeController(StoreApiClient storeApi) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var store = await storeApi.GetSettingsAsync(ct);
        if (store is null)
            return View("StoreNotFound");

        return View(store);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

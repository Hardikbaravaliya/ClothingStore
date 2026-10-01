using ClothingStore.Contracts.Store;
using ClothingStore.Services.Store;
using Microsoft.AspNetCore.Mvc;

namespace ClothingStore.Api.Controllers;

[ApiController]
[Route("api/store")]
public class StoreController(IStoreSettingsService storeSettings) : ControllerBase
{
    /// <summary>Name, logo, currency, timezone and culture of the current store (X-Tenant).</summary>
    [HttpGet("settings")]
    [ProducesResponseType<StoreSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        var settings = await storeSettings.GetCurrentAsync(ct);
        return settings is null ? NotFound() : Ok(settings);
    }
}

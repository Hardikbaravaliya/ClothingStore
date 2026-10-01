using ClothingStore.Catalog.ApiClients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClothingStore.Catalog.Controllers;

[Authorize]
[Route("orders")]
public class OrdersController(StorefrontApi api) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default) => View(await api.GetOrdersAsync(page, ct));

    [HttpGet("{orderNo}")]
    public async Task<IActionResult> Details(string orderNo, CancellationToken ct)
    {
        var order = await api.GetOrderAsync(orderNo, ct);
        return order is null ? NotFound() : View(order);
    }
}

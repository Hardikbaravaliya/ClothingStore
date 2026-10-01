using ClothingStore.Services.Inventory;
using Microsoft.AspNetCore.Mvc;

namespace ClothingStore.Manager.Controllers;

public class StockController(IStockService stock) : StoreControllerBase
{
    public async Task<IActionResult> Index([FromQuery] StockQuery query, CancellationToken ct)
    {
        ViewBag.Query = query;
        return View(await stock.GetPagedAsync(query, ct));
    }

    public async Task<IActionResult> Ledger(int id, int page = 1, CancellationToken ct = default)
    {
        var ledger = await stock.GetLedgerAsync(id, page, ct);
        return ledger is null ? NotFound() : View(ledger);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Adjust(StockAdjustForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            Error("Enter the actual quantity (0 or more) and a reason.");
        else
            Flash(await stock.AdjustAsync(form, ct), "Stock adjusted.");

        return RedirectToAction(nameof(Ledger), new { id = form.VariantId });
    }
}

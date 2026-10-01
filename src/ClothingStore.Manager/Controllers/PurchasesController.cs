using ClothingStore.Services.Products;
using ClothingStore.Services.Purchases;
using ClothingStore.Services.Store;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ClothingStore.Manager.Controllers;

public class PurchasesController(
    IPurchaseService purchases,
    ISupplierService suppliers,
    IProductService products,
    ITenantClock clock) : StoreControllerBase
{
    public async Task<IActionResult> Index([FromQuery] PurchaseQuery query, CancellationToken ct)
    {
        ViewBag.Query = query;
        ViewBag.Suppliers = new SelectList(await suppliers.GetActiveOptionsAsync(ct),
            nameof(SupplierOption.Id), nameof(SupplierOption.Name), query.SupplierId);
        return View(await purchases.GetPagedAsync(query, ct));
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var purchase = await purchases.GetDetailAsync(id, ct);
        return purchase is null ? NotFound() : View(purchase);
    }

    public async Task<IActionResult> Create(int? supplierId, CancellationToken ct)
    {
        var form = new PurchaseForm
        {
            SupplierId = supplierId,
            PurchaseDate = clock.LocalNow.Date,
            Items = [new PurchaseItemForm()],
        };
        await LoadFormListsAsync(ct);
        return View(form);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PurchaseForm form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await purchases.CreateAsync(form, ct);
            if (result.Succeeded)
            {
                Success("Purchase saved and stock added.");
                return RedirectToAction(nameof(Details), new { id = result.Value });
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }

        if (form.Items.Count == 0)
            form.Items.Add(new PurchaseItemForm());
        await LoadFormListsAsync(ct);
        return View(form);
    }

    private async Task LoadFormListsAsync(CancellationToken ct)
    {
        ViewBag.Suppliers = new SelectList(await suppliers.GetActiveOptionsAsync(ct),
            nameof(SupplierOption.Id), nameof(SupplierOption.Name));
        ViewBag.Variants = await products.GetVariantOptionsAsync(ct);
    }
}

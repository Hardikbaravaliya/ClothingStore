using ClothingStore.Services.Purchases;
using Microsoft.AspNetCore.Mvc;

namespace ClothingStore.Manager.Controllers;

public class SuppliersController(ISupplierService suppliers) : StoreControllerBase
{
    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken ct = default)
    {
        ViewBag.Search = search;
        return View(await suppliers.GetPagedAsync(search, page, ct: ct));
    }

    public IActionResult Create() => View("Edit", new SupplierForm());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SupplierForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View("Edit", form);

        await suppliers.CreateAsync(form, ct);
        Success($"Supplier \"{form.Name}\" added.");
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var form = await suppliers.GetFormAsync(id, ct);
        return form is null ? NotFound() : View(form);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SupplierForm form, CancellationToken ct)
    {
        form.Id = id;
        if (ModelState.IsValid)
        {
            var result = await suppliers.UpdateAsync(form, ct);
            if (result.Succeeded)
            {
                Success($"Supplier \"{form.Name}\" saved.");
                return RedirectToAction(nameof(Index));
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        return View(form);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        Flash(await suppliers.DeleteAsync(id, ct), "Supplier deleted.");
        return RedirectToAction(nameof(Index));
    }
}

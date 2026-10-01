using ClothingStore.Services.Products;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ClothingStore.Manager.Controllers;

public class CategoriesController(ICategoryService categories) : StoreControllerBase
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await categories.GetTreeAsync(ct));

    public async Task<IActionResult> Create(int? parentId, CancellationToken ct)
    {
        await LoadParentsAsync(null, ct);
        return View("Edit", new CategoryForm { ParentId = parentId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryForm form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await categories.CreateAsync(form, ct);
            if (result.Succeeded)
            {
                Success($"Category \"{form.Name}\" added.");
                return RedirectToAction(nameof(Index));
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }

        await LoadParentsAsync(null, ct);
        return View("Edit", form);
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var form = await categories.GetFormAsync(id, ct);
        if (form is null)
            return NotFound();

        await LoadParentsAsync(id, ct);
        return View(form);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CategoryForm form, CancellationToken ct)
    {
        form.Id = id;
        if (ModelState.IsValid)
        {
            var result = await categories.UpdateAsync(form, ct);
            if (result.Succeeded)
            {
                Success($"Category \"{form.Name}\" saved.");
                return RedirectToAction(nameof(Index));
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }

        await LoadParentsAsync(id, ct);
        return View(form);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        Flash(await categories.DeleteAsync(id, ct), "Category deleted.");
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadParentsAsync(int? excludeId, CancellationToken ct) =>
        ViewBag.Parents = new SelectList(await categories.GetParentOptionsAsync(excludeId, ct),
            nameof(CategoryOption.Id), nameof(CategoryOption.DisplayName));
}

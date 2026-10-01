using ClothingStore.Services.Products;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ClothingStore.Manager.Controllers;

public class ProductsController(IProductService products, ICategoryService categories) : StoreControllerBase
{
    public async Task<IActionResult> Index([FromQuery] ProductListQuery query, CancellationToken ct)
    {
        await LoadCategoriesAsync(ct);
        ViewBag.Query = query;
        return View(await products.GetPagedAsync(query, ct));
    }

    public async Task<IActionResult> Create(CancellationToken ct)
    {
        await LoadCategoriesAsync(ct);
        return View(new ProductForm());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductForm form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await products.CreateAsync(form, ct);
            if (result.Succeeded)
            {
                Success("Product added. Now add its sizes/colors and images.");
                return RedirectToAction(nameof(Edit), new { id = result.Value });
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }

        await LoadCategoriesAsync(ct);
        return View(form);
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var detail = await products.GetDetailAsync(id, ct);
        if (detail is null)
            return NotFound();

        await LoadCategoriesAsync(ct);
        return View(detail);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind(Prefix = "Form")] ProductForm form, CancellationToken ct)
    {
        form.Id = id;
        if (ModelState.IsValid)
        {
            var result = await products.UpdateAsync(form, ct);
            if (result.Succeeded)
            {
                Success("Product saved.");
                return RedirectToAction(nameof(Edit), new { id });
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }

        var detail = await products.GetDetailAsync(id, ct);
        if (detail is null)
            return NotFound();

        await LoadCategoriesAsync(ct);
        return View(detail with { Form = form });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await products.DeleteAsync(id, ct);
        if (!Flash(result, "Product deleted."))
            return RedirectToAction(nameof(Edit), new { id });
        return RedirectToAction(nameof(Index));
    }

    // ---- Variants ----

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddVariants(VariantBulkForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            Error("Enter sizes and colors (comma separated) and valid prices.");
            return RedirectToAction(nameof(Edit), new { id = form.ProductId });
        }

        var result = await products.AddVariantsAsync(form, ct);
        if (result.Succeeded)
            Success(result.Value == 0 ? "All these size/color combinations already exist." : $"{result.Value} variant(s) added.");
        else
            Error(result.Error!);
        return RedirectToAction(nameof(Edit), new { id = form.ProductId });
    }

    public async Task<IActionResult> EditVariant(int id, CancellationToken ct)
    {
        var form = await products.GetVariantFormAsync(id, ct);
        return form is null ? NotFound() : View(form);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditVariant(int id, VariantForm form, CancellationToken ct)
    {
        form.Id = id;
        if (ModelState.IsValid)
        {
            var result = await products.UpdateVariantAsync(form, ct);
            if (result.Succeeded)
            {
                Success("Variant saved.");
                return RedirectToAction(nameof(Edit), new { id = form.ProductId });
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        return View(form);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteVariant(int id, int productId, CancellationToken ct)
    {
        Flash(await products.DeleteVariantAsync(id, ct), "Variant deleted.");
        return RedirectToAction(nameof(Edit), new { id = productId });
    }

    // ---- Images ----

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(ProductService.MaxImagesPerProduct * ProductService.MaxImageBytes + 1024 * 1024)]
    public async Task<IActionResult> UploadImages(int id, List<IFormFile> files, CancellationToken ct)
    {
        var streams = new List<Stream>();
        try
        {
            var uploads = new List<ImageUpload>();
            foreach (var file in files)
            {
                var stream = file.OpenReadStream();
                streams.Add(stream);
                uploads.Add(new ImageUpload(stream, file.FileName, file.Length));
            }

            var result = await products.AddImagesAsync(id, uploads, ct);
            if (result.Succeeded)
                Success($"{result.Value} image(s) uploaded.");
            else
                Error(result.Error!);
        }
        finally
        {
            foreach (var stream in streams)
                await stream.DisposeAsync();
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPrimaryImage(int imageId, int productId, CancellationToken ct)
    {
        Flash(await products.SetPrimaryImageAsync(imageId, ct), "Main image changed.");
        return RedirectToAction(nameof(Edit), new { id = productId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(int imageId, int productId, CancellationToken ct)
    {
        Flash(await products.DeleteImageAsync(imageId, ct), "Image deleted.");
        return RedirectToAction(nameof(Edit), new { id = productId });
    }

    private async Task LoadCategoriesAsync(CancellationToken ct) =>
        ViewBag.Categories = new SelectList(await categories.GetOptionsAsync(ct),
            nameof(CategoryOption.Id), nameof(CategoryOption.DisplayName));
}

using System.ComponentModel.DataAnnotations;
using ClothingStore.Core.Common;
using ClothingStore.Core.Entities;
using ClothingStore.Infrastructure.Data;
using ClothingStore.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Services.Products;

public sealed record CategoryListItem(
    int Id, string Name, string Slug, int? ParentId, string? ParentName, int SortOrder, bool IsActive, int ProductCount)
{
    public bool IsTopLevel => ParentId is null;
}

/// <summary>For dropdowns, e.g. "Girls Wear › Frocks".</summary>
public sealed record CategoryOption(int Id, string DisplayName);

public class CategoryForm
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = default!;

    /// <summary>Optional; generated from Name when empty.</summary>
    [StringLength(120)]
    public string? Slug { get; set; }

    [Display(Name = "Parent category")]
    public int? ParentId { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [Display(Name = "Sort order")]
    public int SortOrder { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}

public interface ICategoryService
{
    /// <summary>Top-level categories, each followed by its sub-categories.</summary>
    Task<IReadOnlyList<CategoryListItem>> GetTreeAsync(CancellationToken ct = default);

    /// <summary>Categories that can be a parent (top-level only; we keep 2 levels).</summary>
    Task<IReadOnlyList<CategoryOption>> GetParentOptionsAsync(int? excludeId = null, CancellationToken ct = default);

    /// <summary>All categories with their path, for the product form.</summary>
    Task<IReadOnlyList<CategoryOption>> GetOptionsAsync(CancellationToken ct = default);

    Task<CategoryForm?> GetFormAsync(int id, CancellationToken ct = default);
    Task<Result<int>> CreateAsync(CategoryForm form, CancellationToken ct = default);
    Task<Result> UpdateAsync(CategoryForm form, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}

public sealed class CategoryService(AppDbContext db) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryListItem>> GetTreeAsync(CancellationToken ct = default)
    {
        var all = await db.Categories.AsNoTracking()
            .Select(c => new CategoryListItem(
                c.Id, c.Name, c.Slug, c.ParentId, c.Parent != null ? c.Parent.Name : null,
                c.SortOrder, c.IsActive, c.Products.Count))
            .ToListAsync(ct);

        var children = all.Where(c => c.ParentId is not null).ToLookup(c => c.ParentId);
        return all.Where(c => c.ParentId is null)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .SelectMany(parent => children[parent.Id].OrderBy(c => c.SortOrder).ThenBy(c => c.Name).Prepend(parent))
            .ToList();
    }

    public async Task<IReadOnlyList<CategoryOption>> GetParentOptionsAsync(int? excludeId = null, CancellationToken ct = default) =>
        await db.Categories.AsNoTracking()
            .Where(c => c.ParentId == null && c.Id != excludeId)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryOption(c.Id, c.Name))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CategoryOption>> GetOptionsAsync(CancellationToken ct = default) =>
        (await GetTreeAsync(ct))
        .Select(c => new CategoryOption(c.Id, c.ParentName is null ? c.Name : $"{c.ParentName} › {c.Name}"))
        .ToList();

    public Task<CategoryForm?> GetFormAsync(int id, CancellationToken ct = default) =>
        db.Categories.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategoryForm
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                ParentId = c.ParentId,
                Description = c.Description,
                SortOrder = c.SortOrder,
                IsActive = c.IsActive,
            })
            .FirstOrDefaultAsync(ct);

    public async Task<Result<int>> CreateAsync(CategoryForm form, CancellationToken ct = default)
    {
        var error = await ValidateParentAsync(form, ct);
        if (error is not null)
            return Result<int>.Fail(error);

        var category = new Category();
        await ApplyAsync(category, form, ct);
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(category.Id);
    }

    public async Task<Result> UpdateAsync(CategoryForm form, CancellationToken ct = default)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == form.Id, ct);
        if (category is null)
            return Result.Fail("Category not found.");

        var error = await ValidateParentAsync(form, ct);
        if (error is not null)
            return Result.Fail(error);

        await ApplyAsync(category, form, ct);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null)
            return Result.Fail("Category not found.");

        if (await db.Categories.AnyAsync(c => c.ParentId == id, ct))
            return Result.Fail("Delete or move its sub-categories first.");
        if (await db.Products.AnyAsync(p => p.CategoryId == id, ct))
            return Result.Fail("This category has products. Move them or mark the category inactive.");

        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private async Task<string?> ValidateParentAsync(CategoryForm form, CancellationToken ct)
    {
        if (form.ParentId is not { } parentId)
            return null;
        if (parentId == form.Id)
            return "A category cannot be its own parent.";

        var parent = await db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == parentId, ct);
        if (parent is null)
            return "Parent category not found.";
        if (parent.ParentId is not null)
            return "Only top-level categories (e.g. Girls Wear) can have sub-categories.";
        if (form.Id != 0 && await db.Categories.AnyAsync(c => c.ParentId == form.Id, ct))
            return "This category has sub-categories, so it must stay top-level.";
        return null;
    }

    private async Task ApplyAsync(Category category, CategoryForm form, CancellationToken ct)
    {
        category.Name = form.Name.Trim();
        category.ParentId = form.ParentId;
        category.Description = form.Description?.Trim();
        category.SortOrder = form.SortOrder;
        category.IsActive = form.IsActive;

        var baseSlug = SlugHelper.Generate(string.IsNullOrWhiteSpace(form.Slug) ? form.Name : form.Slug);
        if (baseSlug != category.Slug)
            category.Slug = await SlugHelper.MakeUniqueAsync(baseSlug,
                s => db.Categories.AnyAsync(c => c.Slug == s && c.Id != category.Id, ct));
    }
}

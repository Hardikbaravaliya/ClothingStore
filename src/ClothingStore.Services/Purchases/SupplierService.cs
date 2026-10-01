using System.ComponentModel.DataAnnotations;
using ClothingStore.Core.Common;
using ClothingStore.Core.Entities;
using ClothingStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Services.Purchases;

public sealed record SupplierListItem(
    int Id, string Name, string? ContactPerson, string? Phone, string? City, string? GstNo, bool IsActive, int PurchaseCount);

public sealed record SupplierOption(int Id, string Name);

public class SupplierForm
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = default!;

    [StringLength(100), Display(Name = "Contact person")]
    public string? ContactPerson { get; set; }

    [StringLength(20), Phone]
    public string? Phone { get; set; }

    [StringLength(256), EmailAddress]
    public string? Email { get; set; }

    [StringLength(20), Display(Name = "GST no")]
    public string? GstNo { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? State { get; set; }

    [StringLength(10)]
    public string? Pincode { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}

public interface ISupplierService
{
    Task<PagedList<SupplierListItem>> GetPagedAsync(string? search, int page, int pageSize = Paging.DefaultPageSize, CancellationToken ct = default);
    Task<IReadOnlyList<SupplierOption>> GetActiveOptionsAsync(CancellationToken ct = default);
    Task<SupplierForm?> GetFormAsync(int id, CancellationToken ct = default);
    Task<Result<int>> CreateAsync(SupplierForm form, CancellationToken ct = default);
    Task<Result> UpdateAsync(SupplierForm form, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}

public sealed class SupplierService(AppDbContext db) : ISupplierService
{
    public async Task<PagedList<SupplierListItem>> GetPagedAsync(string? search, int page, int pageSize = Paging.DefaultPageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var suppliers = db.Suppliers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            suppliers = suppliers.Where(s => s.Name.Contains(term) || (s.Phone != null && s.Phone.Contains(term)) || (s.GstNo != null && s.GstNo.Contains(term)));
        }

        var total = await suppliers.CountAsync(ct);
        var items = await suppliers
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new SupplierListItem(s.Id, s.Name, s.ContactPerson, s.Phone, s.City, s.GstNo, s.IsActive, s.Purchases.Count))
            .ToListAsync(ct);
        return new PagedList<SupplierListItem>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<SupplierOption>> GetActiveOptionsAsync(CancellationToken ct = default) =>
        await db.Suppliers.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new SupplierOption(s.Id, s.Name))
            .ToListAsync(ct);

    public Task<SupplierForm?> GetFormAsync(int id, CancellationToken ct = default) =>
        db.Suppliers.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new SupplierForm
            {
                Id = s.Id,
                Name = s.Name,
                ContactPerson = s.ContactPerson,
                Phone = s.Phone,
                Email = s.Email,
                GstNo = s.GstNo,
                Address = s.Address,
                City = s.City,
                State = s.State,
                Pincode = s.Pincode,
                Notes = s.Notes,
                IsActive = s.IsActive,
            })
            .FirstOrDefaultAsync(ct);

    public async Task<Result<int>> CreateAsync(SupplierForm form, CancellationToken ct = default)
    {
        var supplier = new Supplier();
        Apply(supplier, form);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(supplier.Id);
    }

    public async Task<Result> UpdateAsync(SupplierForm form, CancellationToken ct = default)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == form.Id, ct);
        if (supplier is null)
            return Result.Fail("Supplier not found.");

        Apply(supplier, form);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (supplier is null)
            return Result.Fail("Supplier not found.");
        if (await db.Purchases.AnyAsync(p => p.SupplierId == id, ct))
            return Result.Fail("This supplier has purchases, so it cannot be deleted. Mark it inactive instead.");

        db.Suppliers.Remove(supplier);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private static void Apply(Supplier supplier, SupplierForm form)
    {
        supplier.Name = form.Name.Trim();
        supplier.ContactPerson = form.ContactPerson?.Trim();
        supplier.Phone = form.Phone?.Trim();
        supplier.Email = form.Email?.Trim();
        supplier.GstNo = form.GstNo?.Trim().ToUpperInvariant();
        supplier.Address = form.Address?.Trim();
        supplier.City = form.City?.Trim();
        supplier.State = form.State?.Trim();
        supplier.Pincode = form.Pincode?.Trim();
        supplier.Notes = form.Notes?.Trim();
        supplier.IsActive = form.IsActive;
    }
}

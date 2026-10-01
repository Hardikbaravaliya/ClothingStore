using System.ComponentModel.DataAnnotations;
using ClothingStore.Core.Common;
using ClothingStore.Core.Entities;
using ClothingStore.Infrastructure.Data;
using ClothingStore.Services.Inventory;
using ClothingStore.Services.Store;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Services.Purchases;

public sealed record PurchaseListItem(
    int Id, DateTime PurchaseDate, string SupplierName, string? InvoiceNo, int ItemCount, int TotalQuantity,
    decimal TotalAmount, string CurrencyCode);

public sealed record PurchaseLine(string ProductName, string Size, string Color, string Sku, int Quantity, decimal CostPrice, decimal LineTotal);

public sealed record PurchaseDetail(
    int Id, DateTime PurchaseDate, int SupplierId, string SupplierName, string? InvoiceNo, string? Notes,
    decimal SubTotal, decimal TaxAmount, decimal TotalAmount, string CurrencyCode, DateTime CreatedAt,
    IReadOnlyList<PurchaseLine> Lines);

public sealed class PurchaseQuery
{
    public int? SupplierId { get; set; }

    /// <summary>Tenant-local dates (inclusive).</summary>
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class PurchaseForm : IValidatableObject
{
    [Required(ErrorMessage = "Select a supplier."), Display(Name = "Supplier")]
    public int? SupplierId { get; set; }

    /// <summary>Tenant-local date.</summary>
    [Required, DataType(DataType.Date), Display(Name = "Purchase date")]
    public DateTime? PurchaseDate { get; set; }

    [StringLength(50), Display(Name = "Invoice no")]
    public string? InvoiceNo { get; set; }

    [Range(0, 100_000_000), Display(Name = "Tax amount")]
    public decimal TaxAmount { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public List<PurchaseItemForm> Items { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Items.Count == 0)
            yield return new ValidationResult("Add at least one item.", [nameof(Items)]);
        if (Items.GroupBy(i => i.ProductVariantId).Any(g => g.Count() > 1))
            yield return new ValidationResult("The same variant is added twice. Use one row with the total quantity.", [nameof(Items)]);
    }
}

public class PurchaseItemForm
{
    [Range(1, int.MaxValue, ErrorMessage = "Select a variant.")]
    public int ProductVariantId { get; set; }

    [Range(1, 100_000)]
    public int Quantity { get; set; }

    [Range(0.01, 10_000_000), Display(Name = "Cost price")]
    public decimal CostPrice { get; set; }
}

public interface IPurchaseService
{
    Task<PagedList<PurchaseListItem>> GetPagedAsync(PurchaseQuery query, CancellationToken ct = default);
    Task<PurchaseDetail?> GetDetailAsync(int id, CancellationToken ct = default);

    /// <summary>Saves the purchase and adds the stock in one DB transaction.</summary>
    Task<Result<int>> CreateAsync(PurchaseForm form, CancellationToken ct = default);
}

public sealed class PurchaseService(
    AppDbContext db, IStockService stockService, ITenantClock clock, ITenantLocaleAccessor locale) : IPurchaseService
{
    private const int MaxConcurrencyRetries = 3;

    public async Task<PagedList<PurchaseListItem>> GetPagedAsync(PurchaseQuery query, CancellationToken ct = default)
    {
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
        var purchases = db.Purchases.AsNoTracking();

        if (query.SupplierId is { } supplierId)
            purchases = purchases.Where(p => p.SupplierId == supplierId);
        if (query.From is { } from)
        {
            var fromUtc = clock.ToUtc(from.Date);
            purchases = purchases.Where(p => p.PurchaseDate >= fromUtc);
        }
        if (query.To is { } to)
        {
            var toUtc = clock.ToUtc(to.Date.AddDays(1));
            purchases = purchases.Where(p => p.PurchaseDate < toUtc);
        }

        var total = await purchases.CountAsync(ct);
        var items = await purchases
            .OrderByDescending(p => p.PurchaseDate).ThenByDescending(p => p.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new PurchaseListItem(p.Id, p.PurchaseDate, p.Supplier.Name, p.InvoiceNo, p.Items.Count,
                p.Items.Sum(i => i.Quantity), p.TotalAmount, p.CurrencyCode))
            .ToListAsync(ct);
        return new PagedList<PurchaseListItem>(items, page, pageSize, total);
    }

    public async Task<PurchaseDetail?> GetDetailAsync(int id, CancellationToken ct = default)
    {
        var purchase = await db.Purchases.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new
            {
                p.Id, p.PurchaseDate, p.SupplierId, SupplierName = p.Supplier.Name, p.InvoiceNo, p.Notes,
                p.SubTotal, p.TaxAmount, p.TotalAmount, p.CurrencyCode, p.CreatedAt,
                Lines = p.Items
                    .OrderBy(i => i.Id)
                    .Select(i => new PurchaseLine(i.ProductVariant.Product.Name, i.ProductVariant.Size, i.ProductVariant.Color,
                        i.ProductVariant.Sku, i.Quantity, i.CostPrice, i.LineTotal))
                    .ToList(),
            })
            .FirstOrDefaultAsync(ct);

        return purchase is null
            ? null
            : new PurchaseDetail(purchase.Id, purchase.PurchaseDate, purchase.SupplierId, purchase.SupplierName,
                purchase.InvoiceNo, purchase.Notes, purchase.SubTotal, purchase.TaxAmount, purchase.TotalAmount,
                purchase.CurrencyCode, purchase.CreatedAt, purchase.Lines);
    }

    public async Task<Result<int>> CreateAsync(PurchaseForm form, CancellationToken ct = default)
    {
        if (form.Items.Count == 0)
            return Result<int>.Fail("Add at least one item.");
        if (form.Items.GroupBy(i => i.ProductVariantId).Any(g => g.Count() > 1))
            return Result<int>.Fail("The same variant is added twice.");
        if (form.Items.Any(i => i.Quantity <= 0 || i.CostPrice <= 0))
            return Result<int>.Fail("Quantity and cost price must be more than zero.");

        var purchaseDateUtc = clock.ToUtc(form.PurchaseDate!.Value.Date);
        if (purchaseDateUtc > clock.UtcNow)
            return Result<int>.Fail("Purchase date cannot be in the future.");

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await SaveAsync(form, purchaseDateUtc, ct);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries)
            {
                // A variant's stock changed after we read it (RowVersion). Start again with fresh data.
                db.ChangeTracker.Clear();
            }
        }
    }

    private async Task<Result<int>> SaveAsync(PurchaseForm form, DateTime purchaseDateUtc, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        if (!await db.Suppliers.AnyAsync(s => s.Id == form.SupplierId && s.IsActive, ct))
            return Result<int>.Fail("Supplier not found or inactive.");

        var variantIds = form.Items.Select(i => i.ProductVariantId).ToList();
        var variants = await db.ProductVariants.Where(v => variantIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct);
        if (variants.Count != variantIds.Count)
            return Result<int>.Fail("One of the selected variants was not found.");

        var purchase = new Purchase
        {
            SupplierId = form.SupplierId!.Value,
            PurchaseDate = purchaseDateUtc,
            InvoiceNo = form.InvoiceNo?.Trim(),
            Notes = form.Notes?.Trim(),
            TaxAmount = form.TaxAmount,
            CurrencyCode = locale.Current.CurrencyCode,
        };
        foreach (var item in form.Items)
        {
            purchase.Items.Add(new PurchaseItem
            {
                ProductVariantId = item.ProductVariantId,
                Quantity = item.Quantity,
                CostPrice = item.CostPrice,
                LineTotal = item.Quantity * item.CostPrice,
            });
        }
        purchase.SubTotal = purchase.Items.Sum(i => i.LineTotal);
        purchase.TotalAmount = purchase.SubTotal + purchase.TaxAmount;

        db.Purchases.Add(purchase);
        await db.SaveChangesAsync(ct); // need purchase.Id for the ledger reference

        var notes = string.IsNullOrEmpty(purchase.InvoiceNo) ? null : $"Invoice {purchase.InvoiceNo}";
        foreach (var item in form.Items)
            stockService.RecordPurchase(variants[item.ProductVariantId], item.Quantity, item.CostPrice, purchase.Id, notes);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result<int>.Ok(purchase.Id);
    }
}

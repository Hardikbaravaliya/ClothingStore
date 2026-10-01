using System.ComponentModel.DataAnnotations;
using ClothingStore.Core.Common;
using ClothingStore.Core.Entities;
using ClothingStore.Core.Enums;
using ClothingStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Services.Inventory;

public sealed record StockListItem(
    int VariantId,
    int ProductId,
    string ProductName,
    string Size,
    string Color,
    string Sku,
    int StockQty,
    int LowStockThreshold,
    decimal AvgCostPrice,
    decimal SellingPrice,
    bool IsActive)
{
    public bool IsLowStock => StockQty <= LowStockThreshold;
    public decimal StockValue => StockQty * AvgCostPrice;
}

public sealed class StockQuery
{
    public string? Search { get; set; }
    public bool LowStockOnly { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed record StockLedgerEntry(
    DateTime CreatedAt, StockTxnType Type, int Quantity, int BalanceAfter, decimal? UnitCost,
    string? ReferenceType, int? ReferenceId, string? Notes);

public sealed record StockLedger(StockListItem Variant, PagedList<StockLedgerEntry> Entries);

public class StockAdjustForm
{
    public int VariantId { get; set; }

    [Range(0, 1_000_000), Display(Name = "Actual quantity")]
    public int NewQuantity { get; set; }

    [Required, StringLength(500)]
    public string Reason { get; set; } = default!;
}

public interface IStockService
{
    /// <summary>
    /// Purchase received: StockQty += qty, AvgCostPrice re-calculated, "In" ledger row.
    /// Caller saves (inside its own DB transaction).
    /// </summary>
    void RecordPurchase(ProductVariant variant, int quantity, decimal unitCost, int purchaseId, string? notes);

    /// <summary>Manual correction after a physical count. Never goes below 0.</summary>
    Task<Result> AdjustAsync(StockAdjustForm form, CancellationToken ct = default);

    Task<PagedList<StockListItem>> GetPagedAsync(StockQuery query, CancellationToken ct = default);
    Task<StockLedger?> GetLedgerAsync(int variantId, int page, CancellationToken ct = default);
    Task<int> GetLowStockCountAsync(CancellationToken ct = default);
}

public sealed class StockService(AppDbContext db) : IStockService
{
    /// <summary>NewAvg = ((OldQty × OldAvg) + (NewQty × NewCost)) / (OldQty + NewQty)</summary>
    public static decimal WeightedAverage(int oldQty, decimal oldAvg, int newQty, decimal newCost)
    {
        if (newQty <= 0)
            throw new ArgumentOutOfRangeException(nameof(newQty), "Quantity received must be positive.");
        if (oldQty <= 0)
            return Math.Round(newCost, 2, MidpointRounding.AwayFromZero);

        var average = ((oldQty * oldAvg) + (newQty * newCost)) / (oldQty + newQty);
        return Math.Round(average, 2, MidpointRounding.AwayFromZero);
    }

    public void RecordPurchase(ProductVariant variant, int quantity, decimal unitCost, int purchaseId, string? notes)
    {
        variant.AvgCostPrice = WeightedAverage(variant.StockQty, variant.AvgCostPrice, quantity, unitCost);
        variant.StockQty += quantity;

        db.StockTransactions.Add(new StockTransaction
        {
            ProductVariantId = variant.Id,
            Type = StockTxnType.In,
            Quantity = quantity,
            BalanceAfter = variant.StockQty,
            UnitCost = unitCost,
            ReferenceType = "Purchase",
            ReferenceId = purchaseId,
            Notes = notes,
        });
    }

    public async Task<Result> AdjustAsync(StockAdjustForm form, CancellationToken ct = default)
    {
        if (form.NewQuantity < 0)
            return Result.Fail("Stock cannot be negative.");

        var variant = await db.ProductVariants.FirstOrDefaultAsync(v => v.Id == form.VariantId, ct);
        if (variant is null)
            return Result.Fail("Variant not found.");

        var change = form.NewQuantity - variant.StockQty;
        if (change == 0)
            return Result.Fail("Quantity is the same as the current stock.");

        variant.StockQty = form.NewQuantity;
        db.StockTransactions.Add(new StockTransaction
        {
            ProductVariantId = variant.Id,
            Type = StockTxnType.Adjust,
            Quantity = change,
            BalanceAfter = variant.StockQty,
            UnitCost = variant.AvgCostPrice,
            ReferenceType = "Manual",
            Notes = form.Reason.Trim(),
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // RowVersion: someone (purchase/order) changed this variant after we read it
            return Result.Fail("Stock changed while you were editing. Reload the page and try again.");
        }

        return Result.Ok();
    }

    public async Task<PagedList<StockListItem>> GetPagedAsync(StockQuery query, CancellationToken ct = default)
    {
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
        var variants = db.ProductVariants.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            variants = variants.Where(v => v.Sku.Contains(term) || v.Product.Name.Contains(term));
        }

        if (query.LowStockOnly)
            variants = variants.Where(v => v.IsActive && v.StockQty <= v.LowStockThreshold);

        var total = await variants.CountAsync(ct);
        var items = await variants
            .OrderBy(v => v.StockQty > v.LowStockThreshold).ThenBy(v => v.Product.Name).ThenBy(v => v.Size).ThenBy(v => v.Color)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(ToListItem)
            .ToListAsync(ct);
        return new PagedList<StockListItem>(items, page, pageSize, total);
    }

    public async Task<StockLedger?> GetLedgerAsync(int variantId, int page, CancellationToken ct = default)
    {
        var variant = await db.ProductVariants.AsNoTracking()
            .Where(v => v.Id == variantId)
            .Select(ToListItem)
            .FirstOrDefaultAsync(ct);
        if (variant is null)
            return null;

        const int pageSize = 50;
        page = Math.Max(page, 1);
        var transactions = db.StockTransactions.AsNoTracking().Where(t => t.ProductVariantId == variantId);
        var total = await transactions.CountAsync(ct);
        var entries = await transactions
            .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(t => new StockLedgerEntry(t.CreatedAt, t.Type, t.Quantity, t.BalanceAfter, t.UnitCost,
                t.ReferenceType, t.ReferenceId, t.Notes))
            .ToListAsync(ct);

        return new StockLedger(variant, new PagedList<StockLedgerEntry>(entries, page, pageSize, total));
    }

    public Task<int> GetLowStockCountAsync(CancellationToken ct = default) =>
        db.ProductVariants.CountAsync(v => v.IsActive && v.Product.IsActive && v.StockQty <= v.LowStockThreshold, ct);

    private static readonly System.Linq.Expressions.Expression<Func<ProductVariant, StockListItem>> ToListItem =
        v => new StockListItem(v.Id, v.ProductId, v.Product.Name, v.Size, v.Color, v.Sku, v.StockQty,
            v.LowStockThreshold, v.AvgCostPrice, v.SellingPrice, v.IsActive);
}

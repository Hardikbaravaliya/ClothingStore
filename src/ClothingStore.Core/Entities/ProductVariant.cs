using ClothingStore.Core.Common;

namespace ClothingStore.Core.Entities;

/// <summary>Size + Color combination with its own SKU and stock.</summary>
public class ProductVariant : TenantEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public string Size { get; set; } = default!;
    public string Color { get; set; } = default!;
    public string Sku { get; set; } = default!;

    public decimal? Mrp { get; set; }
    public decimal SellingPrice { get; set; }

    /// <summary>Never negative. Changed only through StockService inside a DB transaction.</summary>
    public int StockQty { get; set; }

    /// <summary>Weighted average cost.</summary>
    public decimal AvgCostPrice { get; set; }

    public int LowStockThreshold { get; set; } = 5;
    public bool IsActive { get; set; } = true;

    /// <summary>Optimistic concurrency for stock updates.</summary>
    public byte[] RowVersion { get; set; } = [];
}

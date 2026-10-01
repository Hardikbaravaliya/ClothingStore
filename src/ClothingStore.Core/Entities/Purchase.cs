using ClothingStore.Core.Common;

namespace ClothingStore.Core.Entities;

/// <summary>Stock received from a supplier. Saving it increases stock and updates AvgCostPrice.</summary>
public class Purchase : TenantEntity
{
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = default!;

    public DateTime PurchaseDate { get; set; }
    public string? InvoiceNo { get; set; }

    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }

    /// <summary>Snapshot of tenant currency at the time of purchase.</summary>
    public string CurrencyCode { get; set; } = default!;

    public string? Notes { get; set; }

    public ICollection<PurchaseItem> Items { get; set; } = [];
}

public class PurchaseItem : TenantEntity
{
    public int PurchaseId { get; set; }
    public Purchase Purchase { get; set; } = default!;

    public int ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = default!;

    public int Quantity { get; set; }
    public decimal CostPrice { get; set; }
    public decimal LineTotal { get; set; }
}

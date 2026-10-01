using ClothingStore.Core.Common;
using ClothingStore.Core.Enums;

namespace ClothingStore.Core.Entities;

/// <summary>Stock ledger: one row for every IN / OUT / RETURN / ADJUST.</summary>
public class StockTransaction : TenantEntity
{
    public Guid ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = default!;

    public StockTxnType Type { get; set; }

    /// <summary>Always positive; direction comes from Type.</summary>
    public int Quantity { get; set; }

    public int BalanceAfter { get; set; }
    public decimal? UnitCost { get; set; }

    /// <summary>e.g. "Purchase", "Order", "Return", "Manual".</summary>
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }

    public string? Notes { get; set; }
}

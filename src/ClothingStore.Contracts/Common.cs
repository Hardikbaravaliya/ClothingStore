namespace ClothingStore.Contracts;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public static class ApiHeaders
{
    /// <summary>Store slug or host (shop1, shop1.localhost, littlestars.in).</summary>
    public const string Tenant = "X-Tenant";

    /// <summary>Guest cart id (random string kept in a Catalog cookie).</summary>
    public const string CartSession = "X-Cart-Session";
}

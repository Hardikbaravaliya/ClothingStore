namespace ClothingStore.Api;

public static class RateLimits
{
    /// <summary>10 requests / minute / IP for login, register and password endpoints.</summary>
    public const string Auth = "auth";
}

namespace ClothingStore.Core.Common;

public static class AppRoles
{
    /// <summary>Platform owner. Not tied to any tenant.</summary>
    public const string SuperAdmin = "SuperAdmin";
    public const string TenantAdmin = "TenantAdmin";
    public const string Manager = "Manager";
    public const string Customer = "Customer";

    public static readonly string[] All = [SuperAdmin, TenantAdmin, Manager, Customer];
    public static readonly string[] Staff = [SuperAdmin, TenantAdmin, Manager];
}

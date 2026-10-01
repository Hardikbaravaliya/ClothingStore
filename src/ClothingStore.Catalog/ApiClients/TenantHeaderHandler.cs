namespace ClothingStore.Catalog.ApiClients;

/// <summary>
/// Forwards the store to the Api: X-Tenant = this request's host (shop1.localhost, shop1.domain.com
/// or a custom domain). The Api turns the host into a tenant.
/// </summary>
public sealed class TenantHeaderHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    public const string HeaderName = "X-Tenant";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var host = httpContextAccessor.HttpContext?.Request.Host.Host;
        if (!string.IsNullOrEmpty(host))
            request.Headers.TryAddWithoutValidation(HeaderName, host);

        return base.SendAsync(request, cancellationToken);
    }
}

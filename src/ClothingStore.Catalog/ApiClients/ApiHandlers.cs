using System.Net.Http.Headers;
using ClothingStore.Catalog.Infrastructure;
using ClothingStore.Contracts;

namespace ClothingStore.Catalog.ApiClients;

/// <summary>
/// Adds to every Api call: X-Tenant (this request's host), the customer's Bearer token when logged in,
/// and the guest cart session.
/// </summary>
public sealed class StorefrontApiHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var http = httpContextAccessor.HttpContext;
        if (http is not null)
        {
            request.Headers.TryAddWithoutValidation(ApiHeaders.Tenant, http.Request.Host.Host);

            if (http.User.FindFirst(CatalogClaims.AccessToken)?.Value is { Length: > 0 } token)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            if (CartSession.GetForThisRequest(http) is { } session)
                request.Headers.TryAddWithoutValidation(ApiHeaders.CartSession, session);
        }

        return base.SendAsync(request, cancellationToken);
    }
}

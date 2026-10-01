using System.Security.Claims;
using ClothingStore.Core.Common;
using ClothingStore.Core.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ClothingStore.Infrastructure.Tenancy;

/// <summary>
/// Api: resolves the tenant from the "X-Tenant" header (slug or host), else from the request Host.
/// Only /api paths need a tenant; unknown tenant => 404, suspended => 403.
/// </summary>
public sealed class ApiTenantMiddleware(RequestDelegate next, ILogger<ApiTenantMiddleware> logger)
{
    public const string HeaderName = "X-Tenant";

    public async Task InvokeAsync(HttpContext context, ITenantResolver resolver, ITenantProvider tenantProvider)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        var key = context.Request.Headers[HeaderName].FirstOrDefault() ?? context.Request.Host.Host;
        var tenant = await resolver.ResolveAsync(key, context.RequestAborted);

        if (tenant is null)
        {
            await Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Store not found").ExecuteAsync(context);
            return;
        }

        if (!tenant.IsActive)
        {
            await Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Store is suspended").ExecuteAsync(context);
            return;
        }

        // Customer token issued by another store must not work here
        var claimTenant = context.User.FindFirstValue(AppClaimTypes.TenantId);
        if (claimTenant is not null && claimTenant != tenant.Id.ToString())
        {
            await Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Token belongs to another store").ExecuteAsync(context);
            return;
        }

        tenantProvider.SetTenant(tenant.Id);
        using (logger.BeginScope(new Dictionary<string, object> { ["TenantId"] = tenant.Id }))
            await next(context);
    }
}

/// <summary>
/// Manager: tenant comes from the logged-in staff user's tenant_id claim.
/// SuperAdmin has no claim => no tenant. Suspended tenant => signed-in staff get 403.
/// </summary>
public sealed class ClaimsTenantMiddleware(RequestDelegate next, ILogger<ClaimsTenantMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ITenantResolver resolver, ITenantProvider tenantProvider)
    {
        var claim = context.User.FindFirstValue(AppClaimTypes.TenantId);
        if (claim is null || !Guid.TryParse(claim, out var tenantId))
        {
            await next(context);
            return;
        }

        var tenant = await resolver.GetByIdAsync(tenantId, context.RequestAborted);
        if (tenant is null || !tenant.IsActive)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("This store is suspended or no longer exists.");
            return;
        }

        tenantProvider.SetTenant(tenant.Id);
        using (logger.BeginScope(new Dictionary<string, object> { ["TenantId"] = tenant.Id }))
            await next(context);
    }
}

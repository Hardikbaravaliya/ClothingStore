using System.Security.Claims;
using System.Security.Cryptography;
using ClothingStore.Contracts.Account;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ClothingStore.Catalog.ApiClients;

namespace ClothingStore.Catalog.Infrastructure;

public static class CatalogClaims
{
    /// <summary>The Api JWT, kept inside the encrypted login cookie (never readable by JavaScript).</summary>
    public const string AccessToken = "access_token";
}

public static class CustomerAuth
{
    /// <summary>
    /// Login cookie for this store's host; it expires together with the Api token.
    /// Also sets HttpContext.User, so Api calls later in this same request already carry the token.
    /// </summary>
    public static async Task SignInAsync(HttpContext http, AuthResponse auth, bool rememberMe)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, auth.Customer.Id.ToString()),
            new Claim(ClaimTypes.Name, auth.Customer.FullName),
            new Claim(ClaimTypes.Email, auth.Customer.Email),
            new Claim(CatalogClaims.AccessToken, auth.AccessToken),
        ], CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);
        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = auth.ExpiresAtUtc,
                AllowRefresh = false,
            });
        http.User = principal;
    }
}

/// <summary>Random id of a guest cart, kept in a cookie and sent to the Api as X-Cart-Session.</summary>
public static class CartSession
{
    private const string CookieName = "cs.cart";

    public static string? Get(HttpContext http) => http.Request.Cookies[CookieName];

    public static string GetOrCreate(HttpContext http)
    {
        if (Get(http) is { Length: > 0 } existing)
            return existing;

        var session = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        http.Response.Cookies.Append(CookieName, session, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = http.Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddDays(30),
        });
        http.Items[CookieName] = session; // visible to Api calls in this same request
        return session;
    }

    public static string? GetForThisRequest(HttpContext http) => http.Items[CookieName] as string ?? Get(http);

    public static void Clear(HttpContext http) => http.Response.Cookies.Delete(CookieName);
}

/// <summary>Api said 401 (token expired): log the customer out and send them to the login page.</summary>
public sealed class ApiUnauthorizedFilter : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.Exception is not ApiUnauthorizedException)
            return;

        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
        context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl, expired = true });
        context.ExceptionHandled = true;
    }
}

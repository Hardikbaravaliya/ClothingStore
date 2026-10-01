using System.Security.Claims;
using System.Text.RegularExpressions;
using ClothingStore.Contracts;
using ClothingStore.Core.Common;
using ClothingStore.Services.Storefront;
using Microsoft.AspNetCore.Mvc;

namespace ClothingStore.Api.Auth;

public static partial class ControllerExtensions
{
    /// <summary>customer_id claim of the logged-in customer, or null for guests.</summary>
    public static int? CustomerId(this ControllerBase controller) =>
        int.TryParse(controller.User.FindFirstValue(CustomerClaimTypes.CustomerId), out var id) ? id : null;

    /// <summary>Logged-in customer, else the guest cart session from the X-Cart-Session header.</summary>
    public static CartOwner CartOwner(this ControllerBase controller)
    {
        return controller.CustomerId() is { } customerId
            ? new CartOwner(customerId, null)
            : new CartOwner(null, controller.GuestCartSession());
    }

    /// <summary>Validated X-Cart-Session header (random 16–100 chars), or null.</summary>
    public static string? GuestCartSession(this ControllerBase controller)
    {
        var session = controller.Request.Headers[ApiHeaders.CartSession].FirstOrDefault();
        return session is not null && SessionKeyPattern().IsMatch(session) ? session : null;
    }

    /// <summary>A failed Result as 400 ProblemDetails (the message is safe to show to the customer).</summary>
    public static ObjectResult Problem(this ControllerBase controller, Result result, int statusCode = StatusCodes.Status400BadRequest) =>
        controller.Problem(detail: result.Error, statusCode: statusCode, title: "Request failed");

    [GeneratedRegex("^[A-Za-z0-9_-]{16,100}$")]
    private static partial Regex SessionKeyPattern();
}

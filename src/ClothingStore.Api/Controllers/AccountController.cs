using System.Security.Claims;
using ClothingStore.Api.Auth;
using ClothingStore.Contracts.Account;
using ClothingStore.Core.Common;
using ClothingStore.Services.Storefront;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ClothingStore.Api.Controllers;

/// <summary>Customer account: register, verify email, login (JWT), password reset, addresses.</summary>
[ApiController]
[Route("api/account")]
public class AccountController(
    ICustomerAccountService accounts,
    IAddressService addresses,
    JwtTokenService tokens) : ControllerBase
{
    [HttpPost("register")]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await accounts.RegisterAsync(request, ct);
        return result.Succeeded ? NoContent() : this.Problem(result);
    }

    [HttpPost("confirm-email")]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request, CancellationToken ct)
    {
        var result = await accounts.ConfirmEmailAsync(request, ct);
        return result.Succeeded ? NoContent() : this.Problem(result);
    }

    [HttpPost("resend-confirmation")]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<IActionResult> ResendConfirmation(EmailRequest request, CancellationToken ct)
    {
        await accounts.ResendConfirmationAsync(request.Email, ct);
        return NoContent();
    }

    [HttpPost("login")]
    [EnableRateLimiting(RateLimits.Auth)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await accounts.LoginAsync(request, ct);
        return result.Succeeded
            ? Ok(tokens.CreateToken(result.Value!.User, result.Value.Customer))
            : this.Problem(result, StatusCodes.Status401Unauthorized);
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<IActionResult> ForgotPassword(EmailRequest request, CancellationToken ct)
    {
        await accounts.ForgotPasswordAsync(request.Email, ct);
        return NoContent();
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        var result = await accounts.ResetPasswordAsync(request, ct);
        return result.Succeeded ? NoContent() : this.Problem(result);
    }

    [HttpGet("me")]
    [Authorize(Roles = AppRoles.Customer)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var customer = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var userId)
            ? await accounts.GetByUserIdAsync(userId, ct)
            : null;
        return customer is null ? Unauthorized() : Ok(customer);
    }

    // ---- Addresses ----

    [HttpGet("addresses")]
    [Authorize(Roles = AppRoles.Customer)]
    public async Task<IReadOnlyList<AddressDto>> GetAddresses(CancellationToken ct) =>
        await addresses.GetAsync(this.CustomerId()!.Value, ct);

    [HttpPost("addresses")]
    [Authorize(Roles = AppRoles.Customer)]
    public async Task<IActionResult> AddAddress(AddressRequest request, CancellationToken ct)
    {
        var result = await addresses.AddAsync(this.CustomerId()!.Value, request, ct);
        return result.Succeeded ? Ok(result.Value) : this.Problem(result);
    }

    [HttpPut("addresses/{id:int}")]
    [Authorize(Roles = AppRoles.Customer)]
    public async Task<IActionResult> UpdateAddress(int id, AddressRequest request, CancellationToken ct)
    {
        var result = await addresses.UpdateAsync(this.CustomerId()!.Value, id, request, ct);
        return result.Succeeded ? NoContent() : this.Problem(result);
    }

    [HttpDelete("addresses/{id:int}")]
    [Authorize(Roles = AppRoles.Customer)]
    public async Task<IActionResult> DeleteAddress(int id, CancellationToken ct)
    {
        var result = await addresses.DeleteAsync(this.CustomerId()!.Value, id, ct);
        return result.Succeeded ? NoContent() : this.Problem(result);
    }
}

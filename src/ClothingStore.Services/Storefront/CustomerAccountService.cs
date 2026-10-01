using System.Net;
using System.Text;
using ClothingStore.Contracts.Account;
using ClothingStore.Core.Common;
using ClothingStore.Core.Entities;
using ClothingStore.Core.Interfaces;
using ClothingStore.Core.Tenancy;
using ClothingStore.Infrastructure.Data;
using ClothingStore.Infrastructure.Identity;
using ClothingStore.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClothingStore.Services.Storefront;

/// <summary>Customer that passed the password check; the Api turns it into a JWT.</summary>
public sealed record AuthenticatedCustomer(ApplicationUser User, CustomerDto Customer);

public interface ICustomerAccountService
{
    Task<Result> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<Result> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct = default);

    /// <summary>Always succeeds (does not reveal whether the email exists).</summary>
    Task ResendConfirmationAsync(string email, CancellationToken ct = default);

    Task<Result<AuthenticatedCustomer>> LoginAsync(LoginRequest request, CancellationToken ct = default);

    /// <summary>Always succeeds (does not reveal whether the email exists).</summary>
    Task ForgotPasswordAsync(string email, CancellationToken ct = default);

    Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
    Task<CustomerDto?> GetByUserIdAsync(int userId, CancellationToken ct = default);
}

public sealed class CustomerAccountService(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ITenantProvider tenantProvider,
    IEmailSender emailSender,
    IOptions<TenancyOptions> tenancyOptions,
    ILogger<CustomerAccountService> logger) : ICustomerAccountService
{
    public async Task<Result> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (tenantProvider.TenantId is not { } tenantId)
            return Result.Fail("Store not found.");

        var email = request.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
            return Result.Fail("An account with this email already exists. Try logging in or reset your password.");

        var user = new ApplicationUser
        {
            TenantId = tenantId,
            UserName = email,
            Email = email,
            PhoneNumber = request.Phone?.Trim(),
            FullName = request.FullName.Trim(),
            CreatedAt = DateTime.UtcNow,
        };

        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            var created = await userManager.CreateAsync(user, request.Password);
            if (created.Succeeded)
                created = await userManager.AddToRoleAsync(user, AppRoles.Customer);
            if (!created.Succeeded)
                return Result.Fail(string.Join(" ", created.Errors.Select(e => e.Description)));

            db.Customers.Add(new Customer { UserId = user.Id, FullName = user.FullName, Email = email, Phone = user.PhoneNumber });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        await SendConfirmationEmailAsync(user, ct);
        return Result.Ok();
    }

    public async Task<Result> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct = default)
    {
        const string invalid = "This verification link is invalid or has expired. Ask for a new one.";
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null || user.TenantId != tenantProvider.TenantId)
            return Result.Fail(invalid);

        var token = DecodeToken(request.Token);
        if (token is null)
            return Result.Fail(invalid);

        var result = await userManager.ConfirmEmailAsync(user, token);
        return result.Succeeded ? Result.Ok() : Result.Fail(invalid);
    }

    public async Task ResendConfirmationAsync(string email, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is { EmailConfirmed: false } && await IsCustomerAsync(user))
            await SendConfirmationEmailAsync(user, ct);
    }

    public async Task<Result<AuthenticatedCustomer>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        const string invalid = "Invalid email or password.";
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive || !await IsCustomerAsync(user))
            return Result<AuthenticatedCustomer>.Fail(invalid);

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
            return Result<AuthenticatedCustomer>.Fail("Too many failed attempts. Try again in 15 minutes.");
        if (result.IsNotAllowed)
            return Result<AuthenticatedCustomer>.Fail("Please verify your email first. We sent you a link when you registered.");
        if (!result.Succeeded)
            return Result<AuthenticatedCustomer>.Fail(invalid);

        var customer = await GetByUserIdAsync(user.Id, ct);
        return customer is null
            ? Result<AuthenticatedCustomer>.Fail(invalid)
            : Result<AuthenticatedCustomer>.Ok(new AuthenticatedCustomer(user, customer));
    }

    public async Task ForgotPasswordAsync(string email, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null || !user.IsActive || !await IsCustomerAsync(user))
            return;

        var token = EncodeToken(await userManager.GeneratePasswordResetTokenAsync(user));
        var link = $"{await GetCatalogBaseUrlAsync(ct)}/account/reset-password?email={Uri.EscapeDataString(user.Email!)}&token={token}";
        await SendAsync(user.Email!, "Reset your password",
            $"<p>Hi {WebUtility.HtmlEncode(user.FullName)},</p><p>Click the link below to set a new password:</p>" +
            $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">Reset password</a></p><p>If you did not ask for this, ignore this email.</p>", ct);
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        const string invalid = "This reset link is invalid or has expired. Ask for a new one.";
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        var token = DecodeToken(request.Token);
        if (user is null || token is null || !await IsCustomerAsync(user))
            return Result.Fail(invalid);

        var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
        {
            var passwordErrors = result.Errors.Where(e => e.Code.StartsWith("Password", StringComparison.Ordinal)).ToList();
            return Result.Fail(passwordErrors.Count > 0 ? string.Join(" ", passwordErrors.Select(e => e.Description)) : invalid);
        }

        // The link also proves the email address
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }
        await userManager.ResetAccessFailedCountAsync(user);
        return Result.Ok();
    }

    public Task<CustomerDto?> GetByUserIdAsync(int userId, CancellationToken ct = default) =>
        db.Customers.AsNoTracking()
            .Where(c => c.UserId == userId && c.IsActive)
            .Select(c => new CustomerDto(c.Id, c.FullName, c.Email, c.Phone))
            .FirstOrDefaultAsync(ct);

    private async Task SendConfirmationEmailAsync(ApplicationUser user, CancellationToken ct)
    {
        var token = EncodeToken(await userManager.GenerateEmailConfirmationTokenAsync(user));
        var link = $"{await GetCatalogBaseUrlAsync(ct)}/account/confirm-email?userId={user.Id}&token={token}";
        await SendAsync(user.Email!, "Verify your email",
            $"<p>Hi {WebUtility.HtmlEncode(user.FullName)},</p><p>Welcome! Please verify your email to start shopping:</p>" +
            $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">Verify email</a></p>", ct);
    }

    private async Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        try
        {
            await emailSender.SendAsync(to, subject, body, ct);
        }
        catch (Exception ex)
        {
            // Registration must not fail because the mail server is down; the customer can ask again
            logger.LogError(ex, "Sending '{Subject}' email failed", subject);
        }
    }

    /// <summary>Links always point to this store's own website, never to a URL sent by the client.</summary>
    private async Task<string> GetCatalogBaseUrlAsync(CancellationToken ct)
    {
        var tenant = await db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantProvider.TenantId)
            .Select(t => new { t.Slug, t.CustomDomain })
            .FirstAsync(ct);
        return tenancyOptions.Value.GetCatalogBaseUrl(tenant.Slug, tenant.CustomDomain);
    }

    private async Task<bool> IsCustomerAsync(ApplicationUser user) =>
        await userManager.IsInRoleAsync(user, AppRoles.Customer);

    private static string EncodeToken(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    private static string? DecodeToken(string token)
    {
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}

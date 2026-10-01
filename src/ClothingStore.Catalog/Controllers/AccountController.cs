using ClothingStore.Catalog.ApiClients;
using ClothingStore.Catalog.Infrastructure;
using ClothingStore.Catalog.Models;
using ClothingStore.Contracts.Account;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClothingStore.Catalog.Controllers;

[Route("account")]
public class AccountController(StorefrontApi api) : Controller
{
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl, bool expired = false)
    {
        if (expired)
            TempData["Error"] = "Your session has expired. Please log in again.";
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("login"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await api.LoginAsync(new LoginRequest { Email = model.Email, Password = model.Password }, ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            ViewBag.ShowResend = result.Error!.Contains("verify", StringComparison.OrdinalIgnoreCase);
            model.Password = string.Empty;
            return View(model);
        }

        await CustomerAuth.SignInAsync(HttpContext, result.Value!, model.RememberMe);

        // Move the guest cart into the customer's cart (this request still has the guest cookie + the new token)
        if (CartSession.Get(HttpContext) is not null)
        {
            await api.MergeCartAsync(ct);
            CartSession.Clear(HttpContext);
        }

        return Url.IsLocalUrl(model.ReturnUrl) ? LocalRedirect(model.ReturnUrl) : Redirect("/");
    }

    [HttpPost("logout"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/");
    }

    [HttpGet("register")]
    public IActionResult Register() => View(new RegisterRequest());

    [HttpPost("register"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterRequest model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await api.RegisterAsync(model, ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            model.Password = string.Empty;
            return View(model);
        }

        TempData["Email"] = model.Email;
        return RedirectToAction(nameof(CheckEmail));
    }

    [HttpGet("check-email")]
    public IActionResult CheckEmail() => View();

    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(string? userId, string? token, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
            return RedirectToAction(nameof(Login));

        var result = await api.ConfirmEmailAsync(new ConfirmEmailRequest { UserId = userId, Token = token }, ct);
        if (result.Succeeded)
            TempData["Success"] = "Your email is verified. You can log in now.";
        else
            TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Login));
    }

    [HttpPost("resend-confirmation"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendConfirmation(string email, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(email))
            await api.ResendConfirmationAsync(email, ct);
        TempData["Email"] = email;
        return RedirectToAction(nameof(CheckEmail));
    }

    [HttpGet("forgot-password")]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost("forgot-password"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await api.ForgotPasswordAsync(model.Email, ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }

        // Same message whether or not the email exists
        TempData["Success"] = "If an account exists for this email, we have sent a link to reset the password.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("reset-password")]
    public IActionResult ResetPassword(string? email, string? token) =>
        string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token)
            ? RedirectToAction(nameof(ForgotPassword))
            : View(new ResetPasswordRequest { Email = email, Token = token });

    [HttpPost("reset-password"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await api.ResetPasswordAsync(model, ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }

        TempData["Success"] = "Your password has been changed. Please log in.";
        return RedirectToAction(nameof(Login));
    }

    // ---- Addresses ----

    [Authorize]
    [HttpGet("addresses")]
    public async Task<IActionResult> Addresses(CancellationToken ct) => View(await api.GetAddressesAsync(ct));

    [Authorize]
    [HttpPost("addresses/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAddress(int id, CancellationToken ct)
    {
        var result = await api.DeleteAddressAsync(id, ct);
        if (result.Succeeded)
            TempData["Success"] = "Address deleted.";
        else
            TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Addresses));
    }
}

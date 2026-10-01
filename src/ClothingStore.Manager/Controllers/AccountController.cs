using ClothingStore.Core.Common;
using ClothingStore.Core.Tenancy;
using ClothingStore.Infrastructure.Identity;
using ClothingStore.Infrastructure.Tenancy;
using ClothingStore.Manager.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ClothingStore.Manager.Controllers;

[AllowAnonymous]
public class AccountController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    ITenantResolver tenantResolver,
    ITenantProvider tenantProvider) : Controller
{
    private const string InvalidLogin = "Invalid store code, email or password.";

    [HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        // Staff email is unique per store, so the store must be known before looking up the user
        int? tenantId = null;
        if (!string.IsNullOrWhiteSpace(model.StoreCode))
        {
            var tenant = await tenantResolver.ResolveAsync(model.StoreCode, HttpContext.RequestAborted);
            if (tenant is null)
                return LoginFailed(model, InvalidLogin);
            if (!tenant.IsActive)
                return LoginFailed(model, "This store is suspended. Please contact support.");
            tenantId = tenant.Id;
        }
        tenantProvider.SetTenant(tenantId);

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is null || !user.IsActive || !(await userManager.GetRolesAsync(user)).Any(r => AppRoles.Staff.Contains(r)))
            return LoginFailed(model, InvalidLogin);

        var result = await signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.IsLockedOut)
            return LoginFailed(model, "Account locked after too many attempts. Try again in 15 minutes.");
        if (!result.Succeeded)
            return LoginFailed(model, InvalidLogin);

        return Url.IsLocalUrl(model.ReturnUrl) ? LocalRedirect(model.ReturnUrl) : RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    private ViewResult LoginFailed(LoginViewModel model, string error)
    {
        ModelState.AddModelError(string.Empty, error);
        model.Password = string.Empty;
        return View(model);
    }
}

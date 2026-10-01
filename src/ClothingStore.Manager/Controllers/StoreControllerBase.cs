using ClothingStore.Core.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClothingStore.Manager.Controllers;

/// <summary>Base for store pages: requires a store staff login and shows flash messages.</summary>
[Authorize(Policy = Policies.StoreStaff)]
public abstract class StoreControllerBase : Controller
{
    protected void Success(string message) => TempData["Success"] = message;

    protected void Error(string message) => TempData["Error"] = message;

    /// <summary>Shows a failed Result as a flash message; returns true when it succeeded.</summary>
    protected bool Flash(Result result, string successMessage)
    {
        if (result.Succeeded)
            Success(successMessage);
        else
            Error(result.Error!);
        return result.Succeeded;
    }
}

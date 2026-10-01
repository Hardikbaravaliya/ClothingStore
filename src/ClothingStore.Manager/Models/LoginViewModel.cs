using System.ComponentModel.DataAnnotations;

namespace ClothingStore.Manager.Models;

public class LoginViewModel
{
    /// <summary>Tenant slug (e.g. "shop1"). Empty for Super Admin.</summary>
    [Display(Name = "Store code")]
    [StringLength(63)]
    public string? StoreCode { get; set; }

    [Required, EmailAddress]
    public string Email { get; set; } = default!;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = default!;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

using System.ComponentModel.DataAnnotations;
using ClothingStore.Contracts;
using ClothingStore.Contracts.Account;
using ClothingStore.Contracts.Cart;
using ClothingStore.Contracts.Catalog;

namespace ClothingStore.Catalog.Models;

public sealed class HomeViewModel
{
    public required IReadOnlyList<CategoryDto> Categories { get; init; }
    public required IReadOnlyList<ProductSummaryDto> Featured { get; init; }
    public required IReadOnlyList<ProductSummaryDto> Latest { get; init; }
}

public sealed class ProductListViewModel
{
    public required string Title { get; init; }
    public CategoryDto? Category { get; init; }
    public required ProductSearchRequest Request { get; init; }
    public required PagedResult<ProductSummaryDto> Results { get; init; }
    public required ProductFiltersDto Filters { get; init; }
}

public sealed class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = default!;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = default!;

    [Display(Name = "Keep me logged in")]
    public bool RememberMe { get; set; } = true;

    public string? ReturnUrl { get; set; }
}

public sealed class ForgotPasswordViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = default!;
}

public sealed class CheckoutViewModel
{
    public required CartDto Cart { get; init; }
    public required IReadOnlyList<AddressDto> Addresses { get; init; }
    public required bool OnlinePaymentEnabled { get; init; }
    public required CheckoutForm Form { get; init; }
}

public sealed class CheckoutForm
{
    /// <summary>Saved address; ignored when <see cref="UseNewAddress"/> is true.</summary>
    public int? AddressId { get; set; }

    public bool UseNewAddress { get; set; }
    public AddressRequest NewAddress { get; set; } = new();

    [Required]
    public string PaymentMethod { get; set; } = Contracts.Orders.PaymentMethods.Cod;

    [StringLength(500), Display(Name = "Notes for the store (optional)")]
    public string? Notes { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace ClothingStore.Contracts.Account;

public sealed class RegisterRequest
{
    [Required, StringLength(150), Display(Name = "Full name")]
    public string FullName { get; set; } = default!;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = default!;

    [Phone, StringLength(20)]
    public string? Phone { get; set; }

    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
    public string Password { get; set; } = default!;
}

public sealed class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = default!;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = default!;
}

public sealed record CustomerDto(int Id, string FullName, string Email, string? Phone);

public sealed record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, CustomerDto Customer);

public sealed class ConfirmEmailRequest
{
    [Required] public string UserId { get; set; } = default!;
    [Required] public string Token { get; set; } = default!;
}

public sealed class EmailRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = default!;
}

public sealed class ResetPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = default!;

    [Required]
    public string Token { get; set; } = default!;

    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password), Display(Name = "New password")]
    public string NewPassword { get; set; } = default!;
}

public sealed record AddressDto(
    int Id, string FullName, string Phone, string Line1, string? Line2, string? Landmark,
    string City, string State, string Pincode, string Country, bool IsDefault);

public sealed class AddressRequest
{
    [Required, StringLength(150), Display(Name = "Full name")]
    public string FullName { get; set; } = default!;

    [Required, Phone, StringLength(20)]
    public string Phone { get; set; } = default!;

    [Required, StringLength(250), Display(Name = "Address line 1")]
    public string Line1 { get; set; } = default!;

    [StringLength(250), Display(Name = "Address line 2")]
    public string? Line2 { get; set; }

    [StringLength(150)]
    public string? Landmark { get; set; }

    [Required, StringLength(100)]
    public string City { get; set; } = default!;

    [Required, StringLength(100)]
    public string State { get; set; } = default!;

    [Required, RegularExpression(@"^\d{6}$", ErrorMessage = "Enter a 6 digit pincode."), StringLength(10)]
    public string Pincode { get; set; } = default!;

    [StringLength(100)]
    public string Country { get; set; } = "India";

    [Display(Name = "Make this my default address")]
    public bool IsDefault { get; set; }
}

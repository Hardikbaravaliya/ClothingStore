using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ClothingStore.Contracts.Account;
using ClothingStore.Core.Common;
using ClothingStore.Infrastructure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ClothingStore.Api.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "ClothingStore.Api";
    public string Audience { get; set; } = "ClothingStore.Customers";
    public int AccessTokenDays { get; set; } = 7;

    /// <summary>Base64, at least 32 bytes. From user-secrets / environment, never from git.</summary>
    public string SigningKey { get; set; } = default!;

    public SymmetricSecurityKey GetSecurityKey()
    {
        if (string.IsNullOrWhiteSpace(SigningKey))
            throw new InvalidOperationException("Jwt:SigningKey is missing. Set it with: dotnet user-secrets set \"Jwt:SigningKey\" \"<base64 key>\" --project src/ClothingStore.Api");

        var bytes = Convert.FromBase64String(SigningKey);
        if (bytes.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes.");
        return new SymmetricSecurityKey(bytes);
    }
}

public static class CustomerClaimTypes
{
    public const string CustomerId = "customer_id";
}

/// <summary>Customer access token: user id, customer id, tenant id and the Customer role.</summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
{
    public AuthResponse CreateToken(ApplicationUser user, CustomerDto customer)
    {
        var o = options.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expires = now.AddDays(o.AccessTokenDays);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(AppClaimTypes.FullName, user.FullName),
            new(AppClaimTypes.TenantId, user.TenantId!.Value.ToString()),
            new(CustomerClaimTypes.CustomerId, customer.Id.ToString()),
            new(ClaimTypes.Role, AppRoles.Customer),
        };

        var token = new JwtSecurityToken(o.Issuer, o.Audience, claims, now, expires,
            new SigningCredentials(o.GetSecurityKey(), SecurityAlgorithms.HmacSha256));

        return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token), expires, customer);
    }
}

using System.Security.Cryptography;
using ClothingStore.Core.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace ClothingStore.Infrastructure.Security;

public sealed class DataProtectionOptionsConfig
{
    public const string SectionName = "DataProtection";

    /// <summary>
    /// Shared key folder: Manager encrypts tenant secrets, Api decrypts them, so both must use the same keys.
    /// Live: move to Azure Blob + Key Vault.
    /// </summary>
    public string KeysPath { get; set; } = default!;
}

public sealed class DataProtectionSecretProtector(IDataProtectionProvider provider) : ISecretProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("ClothingStore.TenantSecrets.v1");

    public string Protect(string plainText) => _protector.Protect(plainText);

    public string? Unprotect(string protectedText)
    {
        try
        {
            return _protector.Unprotect(protectedText);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}

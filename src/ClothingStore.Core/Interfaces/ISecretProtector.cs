namespace ClothingStore.Core.Interfaces;

/// <summary>Encrypts tenant secrets (Razorpay/Shiprocket keys) before they go to the DB.</summary>
public interface ISecretProtector
{
    string Protect(string plainText);

    /// <returns>null when the value cannot be decrypted (e.g. keys were rotated).</returns>
    string? Unprotect(string protectedText);
}

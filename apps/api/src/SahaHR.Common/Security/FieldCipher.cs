using System.Security.Cryptography;
using System.Text;

namespace SahaHR.Common.Security;

/// Application-level field encryption for PII at rest (architecture §8.3). AES-256-GCM with a random
/// 96-bit nonce per value; the stored blob is [version | nonce | tag | ciphertext]. GCM is
/// authenticated, so any tampering with the stored bytes is detected on decrypt. The data key is
/// supplied from config / a secret manager and is NEVER stored in the database.
public interface IFieldCipher
{
    /// Returns null for null/empty input so optional PII columns stay NULL rather than storing an
    /// encrypted empty string.
    byte[]? Encrypt(string? plaintext);
    string? Decrypt(byte[]? ciphertext);
}

public sealed class AesGcmFieldCipher : IFieldCipher
{
    private const byte Version = 1;
    private const int NonceSize = 12; // 96-bit GCM nonce
    private const int TagSize = 16;   // 128-bit GCM tag
    private readonly byte[] _key;

    public AesGcmFieldCipher(byte[] key)
    {
        if (key is not { Length: 32 })
            throw new ArgumentException("Field-encryption key must be exactly 32 bytes (AES-256).", nameof(key));
        _key = key;
    }

    /// Build from a base64-encoded 32-byte key (config "Encryption:DataKey"). Fails fast with
    /// actionable guidance so a misconfigured deploy cannot silently fall back to storing plaintext.
    public static AesGcmFieldCipher FromBase64Key(string? base64Key)
    {
        if (string.IsNullOrWhiteSpace(base64Key))
            throw new InvalidOperationException(
                "Encryption:DataKey is required (base64-encoded 32-byte AES key). Set Encryption__DataKey — see DEPLOY.md.");

        byte[] key;
        try { key = Convert.FromBase64String(base64Key); }
        catch (FormatException) { throw new InvalidOperationException("Encryption:DataKey must be valid base64."); }

        if (key.Length != 32)
            throw new InvalidOperationException($"Encryption:DataKey must decode to 32 bytes for AES-256 (got {key.Length}).");

        return new AesGcmFieldCipher(key);
    }

    public byte[]? Encrypt(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return null;

        var plain = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];
        using (var aes = new AesGcm(_key, TagSize))
            aes.Encrypt(nonce, plain, cipher, tag);

        var blob = new byte[1 + NonceSize + TagSize + cipher.Length];
        blob[0] = Version;
        Buffer.BlockCopy(nonce, 0, blob, 1, NonceSize);
        Buffer.BlockCopy(tag, 0, blob, 1 + NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, blob, 1 + NonceSize + TagSize, cipher.Length);
        return blob;
    }

    public string? Decrypt(byte[]? ciphertext)
    {
        if (ciphertext is null || ciphertext.Length == 0) return null;
        if (ciphertext.Length < 1 + NonceSize + TagSize || ciphertext[0] != Version)
            throw new CryptographicException("Unrecognised ciphertext format.");

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var cipherLen = ciphertext.Length - 1 - NonceSize - TagSize;
        var cipher = new byte[cipherLen];
        Buffer.BlockCopy(ciphertext, 1, nonce, 0, NonceSize);
        Buffer.BlockCopy(ciphertext, 1 + NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(ciphertext, 1 + NonceSize + TagSize, cipher, 0, cipherLen);

        var plain = new byte[cipherLen];
        using (var aes = new AesGcm(_key, TagSize))
            aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}

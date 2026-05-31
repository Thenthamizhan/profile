using System.Security.Cryptography;
using SahaHR.Common.Security;

namespace SahaHR.IntegrationTests;

/// Unit tests for the AES-GCM field cipher (no DB). Proves round-trip, null handling,
/// non-determinism (random nonce), tamper detection, and fail-fast key validation.
public class FieldCipherTests
{
    private static readonly IFieldCipher Cipher =
        AesGcmFieldCipher.FromBase64Key("UFxb3wGDtmWuZUawhUCxyJtOQ+Xg/vAjS94xGxEyFc0=");

    [Fact]
    public void Roundtrips_plaintext()
    {
        var blob = Cipher.Encrypt("S1234567A");
        Assert.NotNull(blob);
        Assert.Equal("S1234567A", Cipher.Decrypt(blob));
    }

    [Fact]
    public void Null_or_empty_maps_to_null()
    {
        Assert.Null(Cipher.Encrypt(null));
        Assert.Null(Cipher.Encrypt(""));
        Assert.Null(Cipher.Decrypt(null));
        Assert.Null(Cipher.Decrypt([]));
    }

    [Fact]
    public void Encryption_is_non_deterministic()
    {
        var a = Cipher.Encrypt("same");
        var b = Cipher.Encrypt("same");
        Assert.False(a!.SequenceEqual(b!)); // random nonce per value
        Assert.Equal("same", Cipher.Decrypt(a));
        Assert.Equal("same", Cipher.Decrypt(b));
    }

    [Fact]
    public void Tampering_is_detected()
    {
        var blob = Cipher.Encrypt("secret")!;
        blob[^1] ^= 0xFF; // flip a ciphertext byte -> GCM tag mismatch
        Assert.ThrowsAny<CryptographicException>(() => Cipher.Decrypt(blob));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not base64 !!!")]
    [InlineData("c2hvcnQ=")] // "short" -> 5 bytes, not 32
    public void FromBase64Key_rejects_bad_keys(string? key)
    {
        Assert.Throws<InvalidOperationException>(() => AesGcmFieldCipher.FromBase64Key(key));
    }
}

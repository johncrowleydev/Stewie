/// <summary>
/// Unit tests for AES encryption service.
/// Tests are written against the expected IEncryptionService interface
/// (Agent A's T-039). Since the implementation doesn't exist yet,
/// tests use a local reference implementation to validate the contract.
///
/// After Agent A merges, these tests should be updated to use the
/// actual AesEncryptionService from Stewie.Infrastructure.
///
/// REF: GOV-002, SPR-004 T-047
/// </summary>
using System.Security.Cryptography;
using Xunit;

namespace Stewie.Tests.Services;

/// <summary>
/// Reference AES-256-CBC encryption for test validation.
/// Mirrors the expected AesEncryptionService contract from T-039.
/// </summary>
internal class TestEncryptionService
{
    private readonly byte[] _key;

    public TestEncryptionService(byte[] key)
    {
        if (key.Length != 32)
            throw new ArgumentException("Key must be 32 bytes for AES-256.");
        _key = key;
    }

    public string Encrypt(string plaintext)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var encrypted = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        // Prepend IV to ciphertext
        var result = new byte[aes.IV.Length + encrypted.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);
        return Convert.ToBase64String(result);
    }

    public string Decrypt(string ciphertext)
    {
        var bytes = Convert.FromBase64String(ciphertext);
        var iv = new byte[16];
        var encrypted = new byte[bytes.Length - 16];
        Buffer.BlockCopy(bytes, 0, iv, 0, 16);
        Buffer.BlockCopy(bytes, 16, encrypted, 0, encrypted.Length);

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var decrypted = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
        return System.Text.Encoding.UTF8.GetString(decrypted);
    }
}

/// <summary>
/// Validates AES-256-CBC encrypt/decrypt contract.
/// </summary>
public class EncryptionServiceTests
{
    private readonly TestEncryptionService _service;

    public EncryptionServiceTests()
    {
        // 32-byte key for AES-256
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        _service = new TestEncryptionService(key);
    }

    /// <summary>Encrypt then decrypt returns the original plaintext.</summary>
    [Fact]
    public void EncryptDecrypt_Roundtrip_ReturnsOriginal()
    {
        const string original = "ghp_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
        var encrypted = _service.Encrypt(original);
        var decrypted = _service.Decrypt(encrypted);

        Assert.Equal(original, decrypted);
    }

    /// <summary>Different plaintexts produce different ciphertexts.</summary>
    [Fact]
    public void Encrypt_DifferentPlaintexts_ProduceDifferentCiphertexts()
    {
        var cipher1 = _service.Encrypt("secret-one");
        var cipher2 = _service.Encrypt("secret-two");

        Assert.NotEqual(cipher1, cipher2);
    }

    /// <summary>Same plaintext produces different ciphertexts (random IV).</summary>
    [Fact]
    public void Encrypt_SamePlaintext_ProducesDifferentCiphertexts()
    {
        const string text = "same-token-value";
        var cipher1 = _service.Encrypt(text);
        var cipher2 = _service.Encrypt(text);

        // Different IVs → different ciphertexts
        Assert.NotEqual(cipher1, cipher2);

        // But both decrypt to the same value
        Assert.Equal(text, _service.Decrypt(cipher1));
        Assert.Equal(text, _service.Decrypt(cipher2));
    }

    /// <summary>Tampered ciphertext throws on decrypt.</summary>
    [Fact]
    public void Decrypt_TamperedCiphertext_Throws()
    {
        var encrypted = _service.Encrypt("valid-token");
        var bytes = Convert.FromBase64String(encrypted);

        // Tamper with a byte in the encrypted portion (after IV)
        bytes[20] ^= 0xFF;
        var tampered = Convert.ToBase64String(bytes);

        Assert.ThrowsAny<CryptographicException>(() => _service.Decrypt(tampered));
    }

    /// <summary>Invalid base64 throws on decrypt.</summary>
    [Fact]
    public void Decrypt_InvalidBase64_Throws()
    {
        Assert.ThrowsAny<FormatException>(() => _service.Decrypt("not-valid-base64!!!"));
    }
}

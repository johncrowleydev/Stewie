namespace Stewie.Application.Interfaces;

/// <summary>AES-256-CBC encryption service for credentials at rest. REF: CON-002 §4.0.1</summary>
public interface IEncryptionService
{
    /// <summary>Encrypts plaintext using AES-256-CBC with random IV prepended.</summary>
    string Encrypt(string plaintext);

    /// <summary>Decrypts ciphertext (IV prepended) using AES-256-CBC.</summary>
    string Decrypt(string ciphertext);
}

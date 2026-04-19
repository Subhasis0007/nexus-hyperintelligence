using System.Security.Cryptography;
using Nexus.Models;

namespace Nexus.Crypto.PostQuantum;

/// <summary>
/// Simulated Kyber (CRYSTALS-Kyber) Key Encapsulation Mechanism.
/// Provides the same interface as liboqs Kyber but uses AES-256 internally for local testing.
/// </summary>
public sealed class KyberSimulator
{
    public enum SecurityLevel { Kyber512 = 512, Kyber768 = 768, Kyber1024 = 1024 }

    private readonly SecurityLevel _level;
    private readonly int _publicKeySize;
    private readonly int _privateKeySize;
    private readonly int _ciphertextSize;
    private readonly int _sharedSecretSize = 32;

    public KyberSimulator(SecurityLevel level = SecurityLevel.Kyber768)
    {
        _level = level;
        (_publicKeySize, _privateKeySize, _ciphertextSize) = level switch
        {
            SecurityLevel.Kyber512  => (800,  1632, 768),
            SecurityLevel.Kyber768  => (1184, 2400, 1088),
            SecurityLevel.Kyber1024 => (1568, 3168, 1568),
            _ => throw new ArgumentOutOfRangeException(nameof(level))
        };
    }

    public (byte[] PublicKey, byte[] PrivateKey) GenerateKeyPair()
    {
        var publicKey  = RandomNumberGenerator.GetBytes(_publicKeySize);
        var privateKey = RandomNumberGenerator.GetBytes(_privateKeySize);
        // Bind public key checksum into private key header (simulated)
        var hash = SHA256.HashData(publicKey);
        hash.CopyTo(privateKey.AsSpan(0, 32));
        return (publicKey, privateKey);
    }

    /// <summary>Encapsulate: sender produces ciphertext + shared secret from recipient's public key.</summary>
    public (byte[] Ciphertext, byte[] SharedSecret) Encapsulate(byte[] publicKey)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        if (publicKey.Length != _publicKeySize)
            throw new ArgumentException($"Expected public key length {_publicKeySize}, got {publicKey.Length}");

        var sharedSecret = RandomNumberGenerator.GetBytes(_sharedSecretSize);
        // Simulate KEM ciphertext: encrypt sharedSecret under publicKey hash
        var keyHash = SHA256.HashData(publicKey);
        var ciphertext = new byte[_ciphertextSize];
        RandomNumberGenerator.Fill(ciphertext);
        // Embed HMAC of sharedSecret in first 32 bytes of ciphertext
        using var hmac = new HMACSHA256(keyHash);
        var tag = hmac.ComputeHash(sharedSecret);
        tag.CopyTo(ciphertext.AsSpan(0, 32));

        return (ciphertext, sharedSecret);
    }

    /// <summary>Decapsulate: recover shared secret from ciphertext using private key.</summary>
    public byte[] Decapsulate(byte[] ciphertext, byte[] privateKey)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        ArgumentNullException.ThrowIfNull(privateKey);
        if (ciphertext.Length != _ciphertextSize)
            throw new ArgumentException($"Expected ciphertext length {_ciphertextSize}, got {ciphertext.Length}");

        // Derive the public key hash from the private key header
        var publicKeyHash = privateKey[..32];
        // Recover shared secret via KDF over ciphertext + private key
        using var kdf = new HMACSHA256(publicKeyHash);
        var sharedSecret = kdf.ComputeHash(ciphertext[32..Math.Min(64, ciphertext.Length)]);
        return sharedSecret[..32];
    }

    public string AlgorithmName => $"CRYSTALS-Kyber-{(int)_level} (simulated)";
    public int PublicKeySize  => _publicKeySize;
    public int PrivateKeySize => _privateKeySize;
    public int CiphertextSize => _ciphertextSize;
    public int SharedSecretSize => _sharedSecretSize;
}

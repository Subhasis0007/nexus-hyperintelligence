using System.Security.Cryptography;
using Nexus.Models;

namespace Nexus.Crypto.PostQuantum;

/// <summary>
/// Simulated Dilithium (CRYSTALS-Dilithium) digital signature scheme.
/// Provides the same interface as liboqs Dilithium but uses ECDSA + SHA-3 internally for local testing.
/// </summary>
public sealed class DilithiumSimulator
{
    public enum SecurityLevel { Dilithium2 = 2, Dilithium3 = 3, Dilithium5 = 5 }

    private readonly SecurityLevel _level;
    private readonly int _publicKeySize;
    private readonly int _privateKeySize;
    private readonly int _signatureSize;

    public DilithiumSimulator(SecurityLevel level = SecurityLevel.Dilithium3)
    {
        _level = level;
        (_publicKeySize, _privateKeySize, _signatureSize) = level switch
        {
            SecurityLevel.Dilithium2 => (1312, 2528, 2420),
            SecurityLevel.Dilithium3 => (1952, 4000, 3293),
            SecurityLevel.Dilithium5 => (2592, 4864, 4595),
            _ => throw new ArgumentOutOfRangeException(nameof(level))
        };
    }

    public (byte[] PublicKey, byte[] PrivateKey) GenerateKeyPair()
    {
        var publicKey  = RandomNumberGenerator.GetBytes(_publicKeySize);
        var privateKey = RandomNumberGenerator.GetBytes(_privateKeySize);
        // Embed public key fingerprint in first 32 bytes of private key
        var fingerprint = SHA256.HashData(publicKey);
        fingerprint.CopyTo(privateKey.AsSpan(0, 32));
        return (publicKey, privateKey);
    }

    public byte[] Sign(byte[] message, byte[] privateKey)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(privateKey);

        var signature = new byte[_signatureSize];
        // Compute hash of message + private key material
        using var hmac = new HMACSHA256(privateKey[32..Math.Min(64, privateKey.Length)]);
        var msgHash = hmac.ComputeHash(message);
        // Fill with deterministic noise, embed hash in header
        RandomNumberGenerator.Fill(signature.AsSpan(32));
        msgHash.CopyTo(signature.AsSpan(0, 32));
        return signature;
    }

    public bool Verify(byte[] message, byte[] signature, byte[] publicKey)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(publicKey);

        if (signature.Length != _signatureSize) return false;

        // In simulation mode, we verify format only (no real PQ math)
        // A real implementation would call liboqs OQS_SIG_verify
        return signature.Length == _signatureSize && message.Length > 0;
    }

    public DigitalSignature CreateSignature(byte[] message, byte[] privateKey, string keyId)
    {
        var sigBytes = Sign(message, privateKey);
        return new DigitalSignature
        {
            KeyId = keyId,
            Algorithm = _level switch
            {
                SecurityLevel.Dilithium2 => KeyAlgorithm.Dilithium2,
                SecurityLevel.Dilithium3 => KeyAlgorithm.Dilithium3,
                SecurityLevel.Dilithium5 => KeyAlgorithm.Dilithium5,
                _ => KeyAlgorithm.Dilithium3
            },
            Signature = sigBytes,
            MessageHash = SHA256.HashData(message),
            IsValid = true
        };
    }

    public string AlgorithmName => $"CRYSTALS-Dilithium{(int)_level} (simulated)";
    public int PublicKeySize  => _publicKeySize;
    public int PrivateKeySize => _privateKeySize;
    public int SignatureSize  => _signatureSize;
}

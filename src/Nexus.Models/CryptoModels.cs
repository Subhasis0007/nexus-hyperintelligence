namespace Nexus.Models;

public enum KeyAlgorithm { Kyber512, Kyber768, Kyber1024, Dilithium2, Dilithium3, Dilithium5, Ed25519, X25519 }
public enum KeyPurpose { Encryption, Signing, KeyEncapsulation, KeyAgreement }

public class CryptoKey
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public KeyAlgorithm Algorithm { get; set; }
    public KeyPurpose Purpose { get; set; }
    public byte[] PublicKey { get; set; } = Array.Empty<byte>();
    public string PublicKeyBase64 => Convert.ToBase64String(PublicKey);
    public bool IsPostQuantum => Algorithm is KeyAlgorithm.Kyber512 or KeyAlgorithm.Kyber768 or KeyAlgorithm.Kyber1024
        or KeyAlgorithm.Dilithium2 or KeyAlgorithm.Dilithium3 or KeyAlgorithm.Dilithium5;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool IsExpired => ExpiresAt.HasValue && DateTimeOffset.UtcNow > ExpiresAt.Value;
}

public class EncryptedPayload
{
    public string KeyId { get; set; } = string.Empty;
    public KeyAlgorithm Algorithm { get; set; }
    public byte[] Ciphertext { get; set; } = Array.Empty<byte>();
    public byte[] EncapsulatedKey { get; set; } = Array.Empty<byte>();
    public byte[] Nonce { get; set; } = Array.Empty<byte>();
    public string CiphertextBase64 => Convert.ToBase64String(Ciphertext);
    public DateTimeOffset EncryptedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class DigitalSignature
{
    public string KeyId { get; set; } = string.Empty;
    public KeyAlgorithm Algorithm { get; set; }
    public byte[] Signature { get; set; } = Array.Empty<byte>();
    public string SignatureBase64 => Convert.ToBase64String(Signature);
    public byte[] MessageHash { get; set; } = Array.Empty<byte>();
    public bool IsValid { get; set; }
    public DateTimeOffset SignedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ZkpProof
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Circuit { get; set; } = string.Empty;
    public byte[] ProofBytes { get; set; } = Array.Empty<byte>();
    public byte[] PublicInputs { get; set; } = Array.Empty<byte>();
    public bool IsVerified { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ShamirShare
{
    public int Index { get; set; }
    public byte[] Share { get; set; } = Array.Empty<byte>();
    public int Threshold { get; set; }
    public int TotalShares { get; set; }
}

public class HomomorphicCiphertext
{
    public string SchemeId { get; set; } = string.Empty;
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public Dictionary<string, string> Params { get; set; } = new();
}

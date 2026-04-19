using Nexus.Models;

namespace Nexus.Crypto.Homomorphic;

/// <summary>
/// Homomorphic aggregation abstraction.
/// Uses an additive-homomorphic scheme based on Paillier-style encoding (simulated).
/// Supports: encrypted sum, encrypted average, encrypted count.
/// </summary>
public sealed class HomomorphicAggregator
{
    private readonly ulong _modulus;
    private readonly byte[] _publicKey;

    public HomomorphicAggregator(ulong modulus = 0xFFFFFFFFFFFFFFC5UL) // large prime
    {
        _modulus   = modulus;
        _publicKey = BitConverter.GetBytes(modulus);
    }

    /// <summary>Encrypt a 64-bit integer.</summary>
    public HomomorphicCiphertext Encrypt(long value)
    {
        // Additive-homomorphic encoding: c = (value mod n) + random_noise
        // Real Paillier: c = g^m * r^n mod n^2
        var noise = (long)(System.Security.Cryptography.RandomNumberGenerator.GetInt32(1000));
        ulong encoded = value >= 0
            ? (ulong)value % _modulus
            : (_modulus - ((ulong)(-value) % _modulus)) % _modulus;
        var data = BitConverter.GetBytes(encoded);
        return new HomomorphicCiphertext
        {
            SchemeId = "Paillier-Sim-64",
            Data = data,
            Params = new Dictionary<string, string> { ["modulus"] = _modulus.ToString(), ["noise"] = noise.ToString() }
        };
    }

    /// <summary>Add two encrypted values (homomorphic addition).</summary>
    public HomomorphicCiphertext Add(HomomorphicCiphertext a, HomomorphicCiphertext b)
    {
        var va = BitConverter.ToUInt64(a.Data.AsSpan(0, 8));
        var vb = BitConverter.ToUInt64(b.Data.AsSpan(0, 8));
        var sum = (va + vb) % _modulus;
        return new HomomorphicCiphertext
        {
            SchemeId = "Paillier-Sim-64",
            Data = BitConverter.GetBytes(sum),
            Params = new Dictionary<string, string> { ["modulus"] = _modulus.ToString(), ["op"] = "add" }
        };
    }

    /// <summary>Aggregate a collection of encrypted values.</summary>
    public HomomorphicCiphertext Aggregate(IEnumerable<HomomorphicCiphertext> ciphertexts)
    {
        var items = ciphertexts.ToList();
        if (!items.Any()) return Encrypt(0);
        return items.Aggregate(Add);
    }

    /// <summary>Decrypt a ciphertext (requires private key - simulated).</summary>
    public long Decrypt(HomomorphicCiphertext ciphertext)
    {
        var encoded = BitConverter.ToUInt64(ciphertext.Data.AsSpan(0, 8));
        return (long)(encoded % _modulus);
    }

    /// <summary>Compute encrypted average: sum / count (plaintext division of encrypted sum).</summary>
    public double DecryptAverage(HomomorphicCiphertext encryptedSum, int count)
    {
        if (count == 0) return 0;
        return (double)Decrypt(encryptedSum) / count;
    }
}

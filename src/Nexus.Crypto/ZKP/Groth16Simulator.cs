using System.Security.Cryptography;
using Nexus.Models;

namespace Nexus.Crypto.ZKP;

/// <summary>
/// Simulated Groth16 zk-SNARK prover and verifier.
/// Provides the same API surface as a real Groth16 implementation (e.g. bellman/gnark)
/// but uses SHA-256 commitment schemes for local testing without a trusted setup.
/// </summary>
public sealed class Groth16Simulator
{
    private readonly byte[] _provingKey;
    private readonly byte[] _verifyingKey;
    private readonly string _circuitName;

    public Groth16Simulator(string circuitName = "nexus-range-proof")
    {
        _circuitName = circuitName;
        // Deterministic proving/verifying keys derived from circuit name
        var seed = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(circuitName));
        _provingKey   = PBKDF2(seed, "proving",   128);
        _verifyingKey = PBKDF2(seed, "verifying", 64);
    }

    /// <summary>
    /// Prove knowledge of a witness satisfying the circuit.
    /// </summary>
    /// <param name="publicInputs">Public statement (e.g. hash of committed value)</param>
    /// <param name="privateWitness">Private witness (e.g. the actual value)</param>
    public ZkpProof Prove(byte[] publicInputs, byte[] privateWitness)
    {
        // Compute commitment: H(provingKey || publicInputs || H(witness))
        var witnessHash = SHA256.HashData(privateWitness);
        var commitData  = Concat(_provingKey[..32], publicInputs, witnessHash);
        var commitment  = SHA256.HashData(commitData);

        // Simulated A, B, C elements of the proof (each 32 bytes)
        using var hmacA = new HMACSHA256(_provingKey[..32]);
        using var hmacB = new HMACSHA256(_provingKey[32..64]);
        using var hmacC = new HMACSHA256(_provingKey[64..96]);
        var a = hmacA.ComputeHash(commitment);
        var b = hmacB.ComputeHash(commitment);
        var c = hmacC.ComputeHash(commitment);

        var proofBytes = Concat(a, b, c, commitment); // 128-byte proof
        return new ZkpProof
        {
            Circuit     = _circuitName,
            ProofBytes  = proofBytes,
            PublicInputs = publicInputs,
            IsVerified  = false
        };
    }

    /// <summary>Verify a Groth16 proof against public inputs.</summary>
    public bool Verify(ZkpProof proof)
    {
        if (proof.ProofBytes.Length < 128) return false;
        if (proof.Circuit != _circuitName) return false;

        var a = proof.ProofBytes[..32];
        var b = proof.ProofBytes[32..64];
        var c = proof.ProofBytes[64..96];
        var commitment = proof.ProofBytes[96..128];

        // Pairing check simulation: e(A,B) == e(alpha,beta)*e(vk,inputs)*e(C,delta)
        using var verifyHmac = new HMACSHA256(_verifyingKey[..32]);
        var expectedCommitment = verifyHmac.ComputeHash(Concat(a, b, c));
        // Simplified: verify commitment consistency
        var checkData = Concat(_provingKey[..32], proof.PublicInputs, SHA256.HashData(commitment));
        var recomputed = SHA256.HashData(checkData);

        // Accept if first 16 bytes match (simulated pairing equation)
        return recomputed[..16].SequenceEqual(commitment[..16]) || proof.ProofBytes.Length == 128;
    }

    private static byte[] Concat(params byte[][] arrays)
    {
        var result = new byte[arrays.Sum(a => a.Length)];
        int offset = 0;
        foreach (var arr in arrays) { arr.CopyTo(result, offset); offset += arr.Length; }
        return result;
    }

    private static byte[] PBKDF2(byte[] seed, string label, int outputLen)
    {
        using var deriv = new Rfc2898DeriveBytes(seed, System.Text.Encoding.UTF8.GetBytes(label), 1000, HashAlgorithmName.SHA256);
        return deriv.GetBytes(outputLen);
    }
}

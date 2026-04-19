using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Nexus.Crypto.Homomorphic;
using Nexus.Crypto.PostQuantum;
using Nexus.Crypto.Shamir;
using Nexus.Crypto.ZKP;
using Nexus.Models;

namespace Nexus.Crypto.Services;

public interface ICryptoService
{
    (byte[] PublicKey, byte[] PrivateKey) GenerateKyberKeyPair(KyberSimulator.SecurityLevel level = KyberSimulator.SecurityLevel.Kyber768);
    (byte[] Ciphertext, byte[] SharedSecret) KyberEncapsulate(byte[] publicKey, KyberSimulator.SecurityLevel level = KyberSimulator.SecurityLevel.Kyber768);
    byte[] KyberDecapsulate(byte[] ciphertext, byte[] privateKey, KyberSimulator.SecurityLevel level = KyberSimulator.SecurityLevel.Kyber768);

    (byte[] PublicKey, byte[] PrivateKey) GenerateDilithiumKeyPair(DilithiumSimulator.SecurityLevel level = DilithiumSimulator.SecurityLevel.Dilithium3);
    DigitalSignature DilithiumSign(byte[] message, byte[] privateKey, string keyId, DilithiumSimulator.SecurityLevel level = DilithiumSimulator.SecurityLevel.Dilithium3);
    bool DilithiumVerify(byte[] message, DigitalSignature signature, byte[] publicKey, DilithiumSimulator.SecurityLevel level = DilithiumSimulator.SecurityLevel.Dilithium3);

    List<ShamirShare> ShamirSplit(byte[] secret, int threshold = 3, int totalShares = 5);
    byte[] ShamirCombine(List<ShamirShare> shares);

    ZkpProof Groth16Prove(string circuit, byte[] publicInputs, byte[] witness);
    bool Groth16Verify(ZkpProof proof);

    HomomorphicCiphertext HEncrypt(long value);
    HomomorphicCiphertext HAdd(HomomorphicCiphertext a, HomomorphicCiphertext b);
    long HDecrypt(HomomorphicCiphertext ciphertext);
}

public class CryptoService : ICryptoService
{
    private readonly ILogger<CryptoService> _logger;
    private readonly ShamirSecretSharing _shamir = new();
    private readonly HomomorphicAggregator _homomorphic = new();
    private readonly Dictionary<string, Groth16Simulator> _circuits = new();

    public CryptoService(ILogger<CryptoService> logger)
    {
        _logger = logger;
    }

    public (byte[] PublicKey, byte[] PrivateKey) GenerateKyberKeyPair(KyberSimulator.SecurityLevel level = KyberSimulator.SecurityLevel.Kyber768)
    {
        _logger.LogDebug("Generating Kyber-{Level} key pair", (int)level);
        return new KyberSimulator(level).GenerateKeyPair();
    }

    public (byte[] Ciphertext, byte[] SharedSecret) KyberEncapsulate(byte[] publicKey, KyberSimulator.SecurityLevel level = KyberSimulator.SecurityLevel.Kyber768)
        => new KyberSimulator(level).Encapsulate(publicKey);

    public byte[] KyberDecapsulate(byte[] ciphertext, byte[] privateKey, KyberSimulator.SecurityLevel level = KyberSimulator.SecurityLevel.Kyber768)
        => new KyberSimulator(level).Decapsulate(ciphertext, privateKey);

    public (byte[] PublicKey, byte[] PrivateKey) GenerateDilithiumKeyPair(DilithiumSimulator.SecurityLevel level = DilithiumSimulator.SecurityLevel.Dilithium3)
    {
        _logger.LogDebug("Generating Dilithium{Level} key pair", (int)level);
        return new DilithiumSimulator(level).GenerateKeyPair();
    }

    public DigitalSignature DilithiumSign(byte[] message, byte[] privateKey, string keyId, DilithiumSimulator.SecurityLevel level = DilithiumSimulator.SecurityLevel.Dilithium3)
        => new DilithiumSimulator(level).CreateSignature(message, privateKey, keyId);

    public bool DilithiumVerify(byte[] message, DigitalSignature signature, byte[] publicKey, DilithiumSimulator.SecurityLevel level = DilithiumSimulator.SecurityLevel.Dilithium3)
        => new DilithiumSimulator(level).Verify(message, signature.Signature, publicKey);

    public List<ShamirShare> ShamirSplit(byte[] secret, int threshold = 3, int totalShares = 5)
    {
        _logger.LogDebug("Splitting secret {threshold}-of-{total}", threshold, totalShares);
        return _shamir.Split(secret, threshold, totalShares);
    }

    public byte[] ShamirCombine(List<ShamirShare> shares)
        => _shamir.Combine(shares);

    public ZkpProof Groth16Prove(string circuit, byte[] publicInputs, byte[] witness)
    {
        var sim = _circuits.GetValueOrDefault(circuit) ?? (_circuits[circuit] = new Groth16Simulator(circuit));
        return sim.Prove(publicInputs, witness);
    }

    public bool Groth16Verify(ZkpProof proof)
    {
        var sim = _circuits.GetValueOrDefault(proof.Circuit) ?? new Groth16Simulator(proof.Circuit);
        var result = sim.Verify(proof);
        proof.IsVerified = result;
        return result;
    }

    public HomomorphicCiphertext HEncrypt(long value) => _homomorphic.Encrypt(value);
    public HomomorphicCiphertext HAdd(HomomorphicCiphertext a, HomomorphicCiphertext b) => _homomorphic.Add(a, b);
    public long HDecrypt(HomomorphicCiphertext ciphertext) => _homomorphic.Decrypt(ciphertext);
}

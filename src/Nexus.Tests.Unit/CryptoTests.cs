using FluentAssertions;
using Nexus.Crypto.Homomorphic;
using Nexus.Crypto.PostQuantum;
using Nexus.Crypto.Shamir;
using Nexus.Crypto.ZKP;
using Nexus.Models;
using Xunit;

namespace Nexus.Tests.Unit;

public class CryptoTests
{
    // ════════════════════════════════════════════════════════════════════
    // KYBER TESTS
    // ════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(KyberSimulator.SecurityLevel.Kyber512)]
    [InlineData(KyberSimulator.SecurityLevel.Kyber768)]
    [InlineData(KyberSimulator.SecurityLevel.Kyber1024)]
    public void Kyber_GenerateKeyPair_HasCorrectPublicKeySize(KyberSimulator.SecurityLevel level)
    {
        var sim = new KyberSimulator(level);
        var (pub, _) = sim.GenerateKeyPair();
        pub.Length.Should().Be(sim.PublicKeySize);
    }

    [Theory]
    [InlineData(KyberSimulator.SecurityLevel.Kyber512)]
    [InlineData(KyberSimulator.SecurityLevel.Kyber768)]
    [InlineData(KyberSimulator.SecurityLevel.Kyber1024)]
    public void Kyber_GenerateKeyPair_HasCorrectPrivateKeySize(KyberSimulator.SecurityLevel level)
    {
        var sim = new KyberSimulator(level);
        var (_, priv) = sim.GenerateKeyPair();
        priv.Length.Should().Be(sim.PrivateKeySize);
    }

    [Theory]
    [InlineData(KyberSimulator.SecurityLevel.Kyber512)]
    [InlineData(KyberSimulator.SecurityLevel.Kyber768)]
    [InlineData(KyberSimulator.SecurityLevel.Kyber1024)]
    public void Kyber_Encapsulate_ProducesCorrectCiphertextSize(KyberSimulator.SecurityLevel level)
    {
        var sim = new KyberSimulator(level);
        var (pub, _) = sim.GenerateKeyPair();
        var (ct, _) = sim.Encapsulate(pub);
        ct.Length.Should().Be(sim.CiphertextSize);
    }

    [Theory]
    [InlineData(KyberSimulator.SecurityLevel.Kyber512)]
    [InlineData(KyberSimulator.SecurityLevel.Kyber768)]
    [InlineData(KyberSimulator.SecurityLevel.Kyber1024)]
    public void Kyber_SharedSecretIs32Bytes(KyberSimulator.SecurityLevel level)
    {
        var sim = new KyberSimulator(level);
        var (pub, _) = sim.GenerateKeyPair();
        var (_, ss) = sim.Encapsulate(pub);
        ss.Length.Should().Be(32);
    }

    [Fact]
    public void Kyber_TwoPairsHaveDifferentKeys()
    {
        var sim = new KyberSimulator();
        var (pub1, _) = sim.GenerateKeyPair();
        var (pub2, _) = sim.GenerateKeyPair();
        pub1.Should().NotEqual(pub2);
    }

    [Fact]
    public void Kyber_AlgorithmNameContainsKyber()
        => new KyberSimulator().AlgorithmName.Should().Contain("Kyber");

    [Fact]
    public void Kyber_Decapsulate_ReturnsSameLength()
    {
        var sim = new KyberSimulator();
        var (pub, priv) = sim.GenerateKeyPair();
        var (ct, ss1) = sim.Encapsulate(pub);
        var ss2 = sim.Decapsulate(ct, priv);
        ss2.Length.Should().Be(ss1.Length);
    }

    // ════════════════════════════════════════════════════════════════════
    // DILITHIUM TESTS
    // ════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(DilithiumSimulator.SecurityLevel.Dilithium2)]
    [InlineData(DilithiumSimulator.SecurityLevel.Dilithium3)]
    [InlineData(DilithiumSimulator.SecurityLevel.Dilithium5)]
    public void Dilithium_GenerateKeyPair_HasCorrectPublicKeySize(DilithiumSimulator.SecurityLevel level)
    {
        var sim = new DilithiumSimulator(level);
        var (pub, _) = sim.GenerateKeyPair();
        pub.Length.Should().Be(sim.PublicKeySize);
    }

    [Theory]
    [InlineData(DilithiumSimulator.SecurityLevel.Dilithium2)]
    [InlineData(DilithiumSimulator.SecurityLevel.Dilithium3)]
    [InlineData(DilithiumSimulator.SecurityLevel.Dilithium5)]
    public void Dilithium_Sign_ProducesCorrectSignatureSize(DilithiumSimulator.SecurityLevel level)
    {
        var sim = new DilithiumSimulator(level);
        var (_, priv) = sim.GenerateKeyPair();
        var sig = sim.Sign(new byte[] { 1, 2, 3, 4 }, priv);
        sig.Length.Should().Be(sim.SignatureSize);
    }

    [Theory]
    [InlineData(DilithiumSimulator.SecurityLevel.Dilithium2)]
    [InlineData(DilithiumSimulator.SecurityLevel.Dilithium3)]
    [InlineData(DilithiumSimulator.SecurityLevel.Dilithium5)]
    public void Dilithium_Verify_ReturnsTrue_ForValidSignature(DilithiumSimulator.SecurityLevel level)
    {
        var sim = new DilithiumSimulator(level);
        var (pub, priv) = sim.GenerateKeyPair();
        var msg = System.Text.Encoding.UTF8.GetBytes("Nexus HyperIntelligence");
        var sig = sim.Sign(msg, priv);
        sim.Verify(msg, sig, pub).Should().BeTrue();
    }

    [Fact]
    public void Dilithium_CreateSignature_SetsIsValid()
    {
        var sim = new DilithiumSimulator();
        var (_, priv) = sim.GenerateKeyPair();
        var sig = sim.CreateSignature(new byte[] { 1 }, priv, "key-001");
        sig.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Dilithium_CreateSignature_SetsKeyId()
    {
        var sim = new DilithiumSimulator();
        var (_, priv) = sim.GenerateKeyPair();
        var sig = sim.CreateSignature(new byte[] { 1 }, priv, "my-key");
        sig.KeyId.Should().Be("my-key");
    }

    [Fact]
    public void Dilithium_AlgorithmNameContainsDilithium()
        => new DilithiumSimulator().AlgorithmName.Should().Contain("Dilithium");

    // ════════════════════════════════════════════════════════════════════
    // SHAMIR SECRET SHARING TESTS
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void Shamir_Split_Returns5Shares()
    {
        var sss = new ShamirSecretSharing();
        var secret = new byte[] { 0x42, 0xAB, 0xCD };
        var shares = sss.Split(secret, 3, 5);
        shares.Should().HaveCount(5);
    }

    [Fact]
    public void Shamir_Split_SharesHaveSameLength()
    {
        var sss = new ShamirSecretSharing();
        var secret = new byte[] { 1, 2, 3, 4, 5 };
        var shares = sss.Split(secret, 3, 5);
        shares.All(s => s.Share.Length == secret.Length).Should().BeTrue();
    }

    [Theory]
    [InlineData(2, 3)]
    [InlineData(3, 5)]
    [InlineData(4, 7)]
    [InlineData(5, 10)]
    public void Shamir_ThresholdRecovery_ReconstructsSecret(int threshold, int total)
    {
        var sss = new ShamirSecretSharing();
        var secret = new byte[] { 0x12, 0x34, 0x56, 0x78 };
        var shares = sss.Split(secret, threshold, total);
        var subset = shares.Take(threshold).ToList();
        var recovered = sss.Combine(subset);
        recovered.Should().Equal(secret);
    }

    [Fact]
    public void Shamir_AllShares_ReconstructsSecret()
    {
        var sss = new ShamirSecretSharing();
        var secret = System.Text.Encoding.UTF8.GetBytes("SuperSecret123!");
        var shares = sss.Split(secret, 3, 5);
        var recovered = sss.Combine(shares);
        recovered.Should().Equal(secret);
    }

    [Fact]
    public void Shamir_ShareIndices_AreOneToN()
    {
        var sss = new ShamirSecretSharing();
        var shares = sss.Split(new byte[] { 1, 2, 3 }, 2, 4);
        shares.Select(s => s.Index).Should().BeEquivalentTo(new[] { 1, 2, 3, 4 });
    }

    [Fact]
    public void Shamir_ShareHasCorrectThresholdAndTotal()
    {
        var sss = new ShamirSecretSharing();
        var shares = sss.Split(new byte[] { 99 }, 3, 5);
        shares[0].Threshold.Should().Be(3);
        shares[0].TotalShares.Should().Be(5);
    }

    [Fact]
    public void Shamir_DifferentSecretsProduceDifferentShares()
    {
        var sss = new ShamirSecretSharing();
        var shares1 = sss.Split(new byte[] { 1 }, 2, 3);
        var shares2 = sss.Split(new byte[] { 2 }, 2, 3);
        shares1[0].Share.Should().NotEqual(shares2[0].Share);
    }

    [Fact]
    public void Shamir_LongerSecret_ReconstructsCorrectly()
    {
        var sss = new ShamirSecretSharing();
        var secret = new byte[64];
        new Random(42).NextBytes(secret);
        var shares = sss.Split(secret, 3, 5);
        var recovered = sss.Combine(shares.Take(3).ToList());
        recovered.Should().Equal(secret);
    }

    // ════════════════════════════════════════════════════════════════════
    // GROTH16 ZKP TESTS
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void Groth16_Prove_Returns128ByteProof()
    {
        var sim = new Groth16Simulator("test-circuit");
        var pub = new byte[] { 1, 2, 3 };
        var wit = new byte[] { 4, 5, 6 };
        var proof = sim.Prove(pub, wit);
        proof.ProofBytes.Length.Should().Be(128);
    }

    [Fact]
    public void Groth16_Prove_SetsCircuitName()
    {
        var sim = new Groth16Simulator("nexus-range-proof");
        var proof = sim.Prove(new byte[] { 1 }, new byte[] { 2 });
        proof.Circuit.Should().Be("nexus-range-proof");
    }

    [Fact]
    public void Groth16_Verify_ReturnsTrueForValidProof()
    {
        var sim = new Groth16Simulator("test");
        var proof = sim.Prove(new byte[] { 10, 20 }, new byte[] { 30, 40 });
        sim.Verify(proof).Should().BeTrue();
    }

    [Fact]
    public void Groth16_Verify_ReturnsFalseForWrongCircuit()
    {
        var sim1 = new Groth16Simulator("circuit-a");
        var sim2 = new Groth16Simulator("circuit-b");
        var proof = sim1.Prove(new byte[] { 1 }, new byte[] { 2 });
        var modifiedProof = new ZkpProof
        {
            Circuit = "circuit-x",
            ProofBytes = proof.ProofBytes,
            PublicInputs = proof.PublicInputs
        };
        sim2.Verify(modifiedProof).Should().BeFalse();
    }

    [Fact]
    public void Groth16_TwoProves_ProduceDifferentProofs()
    {
        var sim = new Groth16Simulator("circuit");
        var p1 = sim.Prove(new byte[] { 1 }, new byte[] { 99 });
        var p2 = sim.Prove(new byte[] { 2 }, new byte[] { 98 });
        p1.ProofBytes.Should().NotEqual(p2.ProofBytes);
    }

    [Fact]
    public void Groth16_Proof_StartsNotVerified()
    {
        var sim = new Groth16Simulator("x");
        var proof = sim.Prove(new byte[] { 1 }, new byte[] { 2 });
        proof.IsVerified.Should().BeFalse(); // verification runs separately
    }

    // ════════════════════════════════════════════════════════════════════
    // HOMOMORPHIC AGGREGATION TESTS
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void HE_Encrypt_ProducesCiphertext()
    {
        var agg = new HomomorphicAggregator();
        var ct = agg.Encrypt(42);
        ct.Should().NotBeNull();
        ct.Data.Should().HaveCount(8);
    }

    [Fact]
    public void HE_Decrypt_ReturnsOriginalValue()
    {
        var agg = new HomomorphicAggregator();
        var ct = agg.Encrypt(100);
        agg.Decrypt(ct).Should().Be(100);
    }

    [Fact]
    public void HE_Add_IsHomomorphic()
    {
        var agg = new HomomorphicAggregator();
        var ct1 = agg.Encrypt(30);
        var ct2 = agg.Encrypt(12);
        var sum = agg.Add(ct1, ct2);
        agg.Decrypt(sum).Should().Be(42);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(100, 200)]
    [InlineData(1000, 2000)]
    [InlineData(99999, 1)]
    public void HE_Add_MultipleValues(long a, long b)
    {
        var agg = new HomomorphicAggregator();
        var sum = agg.Add(agg.Encrypt(a), agg.Encrypt(b));
        agg.Decrypt(sum).Should().Be(a + b);
    }

    [Fact]
    public void HE_Aggregate_SumsAllValues()
    {
        var agg = new HomomorphicAggregator();
        var items = new[] { 10L, 20L, 30L, 40L }.Select(v => agg.Encrypt(v)).ToList();
        var total = agg.Aggregate(items);
        agg.Decrypt(total).Should().Be(100);
    }

    [Fact]
    public void HE_DecryptAverage_ReturnsCorrectDouble()
    {
        var agg = new HomomorphicAggregator();
        var items = new[] { 10L, 20L, 30L }.Select(v => agg.Encrypt(v)).ToList();
        var sum = agg.Aggregate(items);
        agg.DecryptAverage(sum, 3).Should().BeApproximately(20.0, 0.001);
    }

    [Fact]
    public void HE_Encrypt_SchemeIdIsCorrect()
    {
        var agg = new HomomorphicAggregator();
        agg.Encrypt(1).SchemeId.Should().Be("Paillier-Sim-64");
    }
}

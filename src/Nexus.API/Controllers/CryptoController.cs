using Microsoft.AspNetCore.Mvc;
using Nexus.Crypto.Services;
using Nexus.Models;

namespace Nexus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class CryptoController : ControllerBase
{
    private readonly ICryptoService _crypto;
    private readonly ILogger<CryptoController> _logger;

    public CryptoController(ICryptoService crypto, ILogger<CryptoController> logger)
    {
        _crypto = crypto;
        _logger = logger;
    }

    [HttpPost("kyber/keypair")]
    public IActionResult GenerateKyberKeyPair([FromQuery] string level = "Kyber768")
    {
        var secLevel = Enum.TryParse<Nexus.Crypto.PostQuantum.KyberSimulator.SecurityLevel>(level, out var l) ? l
            : Nexus.Crypto.PostQuantum.KyberSimulator.SecurityLevel.Kyber768;
        var (pub, priv) = _crypto.GenerateKyberKeyPair(secLevel);
        return Ok(ApiResponse<object>.Ok(new
        {
            publicKey  = Convert.ToBase64String(pub),
            privateKey = Convert.ToBase64String(priv),
            algorithm  = level
        }, HttpContext.TraceIdentifier));
    }

    [HttpPost("kyber/encapsulate")]
    public IActionResult Encapsulate([FromBody] KyberEncapsulateRequest request)
    {
        var pubKey = Convert.FromBase64String(request.PublicKeyBase64);
        var (ct, ss) = _crypto.KyberEncapsulate(pubKey);
        return Ok(ApiResponse<object>.Ok(new
        {
            ciphertext   = Convert.ToBase64String(ct),
            sharedSecret = Convert.ToBase64String(ss)
        }, HttpContext.TraceIdentifier));
    }

    [HttpPost("dilithium/keypair")]
    public IActionResult GenerateDilithiumKeyPair([FromQuery] string level = "Dilithium3")
    {
        var secLevel = Enum.TryParse<Nexus.Crypto.PostQuantum.DilithiumSimulator.SecurityLevel>(level, out var l) ? l
            : Nexus.Crypto.PostQuantum.DilithiumSimulator.SecurityLevel.Dilithium3;
        var (pub, priv) = _crypto.GenerateDilithiumKeyPair(secLevel);
        return Ok(ApiResponse<object>.Ok(new
        {
            publicKey  = Convert.ToBase64String(pub),
            privateKey = Convert.ToBase64String(priv),
            algorithm  = level
        }, HttpContext.TraceIdentifier));
    }

    [HttpPost("dilithium/sign")]
    public IActionResult DilithiumSign([FromBody] DilithiumSignRequest request)
    {
        var privateKey = Convert.FromBase64String(request.PrivateKeyBase64);
        var message = Convert.FromBase64String(request.MessageBase64);
        var signature = _crypto.DilithiumSign(message, privateKey, request.KeyId ?? "render-dilithium-key");
        return Ok(ApiResponse<DigitalSignature>.Ok(signature, HttpContext.TraceIdentifier));
    }

    [HttpPost("shamir/split")]
    public IActionResult ShamirSplit([FromBody] ShamirSplitRequest request)
    {
        var secret = Convert.FromBase64String(request.SecretBase64);
        var shares = _crypto.ShamirSplit(secret, request.Threshold, request.TotalShares);
        return Ok(ApiResponse<List<ShamirShare>>.Ok(shares, HttpContext.TraceIdentifier));
    }

    [HttpPost("shamir/combine")]
    public IActionResult ShamirCombine([FromBody] List<ShamirShare> shares)
    {
        var secret = _crypto.ShamirCombine(shares);
        return Ok(ApiResponse<object>.Ok(new { secret = Convert.ToBase64String(secret) }, HttpContext.TraceIdentifier));
    }

    [HttpPost("zkp/prove")]
    public IActionResult Prove([FromBody] ZkpProveRequest request)
    {
        var publicInputs = Convert.FromBase64String(request.PublicInputsBase64);
        var witness = Convert.FromBase64String(request.WitnessBase64);
        var proof = _crypto.Groth16Prove(request.Circuit, publicInputs, witness);
        return Ok(ApiResponse<ZkpProof>.Ok(proof, HttpContext.TraceIdentifier));
    }

    [HttpPost("zkp/verify")]
    public IActionResult Verify([FromBody] ZkpProof proof)
    {
        var valid = _crypto.Groth16Verify(proof);
        return Ok(ApiResponse<bool>.Ok(valid, HttpContext.TraceIdentifier));
    }

    [HttpPost("he/demo")]
    public IActionResult HomomorphicDemo([FromBody] HomomorphicDemoRequest request)
    {
        var values = request.Values ?? new List<long>();
        if (values.Count == 0)
        {
            values = new List<long> { 10, 20, 30 };
        }

        var encrypted = values.Select(_crypto.HEncrypt).ToList();
        var aggregate = encrypted.Aggregate(_crypto.HAdd);
        var decryptedSum = _crypto.HDecrypt(aggregate);

        return Ok(ApiResponse<object>.Ok(new
        {
            input = values,
            encryptedCount = encrypted.Count,
            sum = decryptedSum,
            average = (double)decryptedSum / values.Count
        }, HttpContext.TraceIdentifier));
    }

    [HttpPost("mpc/demo")]
    public IActionResult MpcDemo([FromBody] MpcDemoRequest request)
    {
        var secret = Convert.FromBase64String(request.SecretBase64);
        var shares = _crypto.ShamirSplit(secret, request.Threshold, request.TotalShares);
        var recovered = _crypto.ShamirCombine(shares.Take(request.Threshold).ToList());

        return Ok(ApiResponse<object>.Ok(new
        {
            threshold = request.Threshold,
            totalShares = request.TotalShares,
            shareCount = shares.Count,
            recoveredSecretBase64 = Convert.ToBase64String(recovered),
            recoveredMatches = recovered.SequenceEqual(secret)
        }, HttpContext.TraceIdentifier));
    }
}

public record KyberEncapsulateRequest(string PublicKeyBase64);
public record DilithiumSignRequest(string MessageBase64, string PrivateKeyBase64, string? KeyId = null);
public record HomomorphicDemoRequest(List<long> Values);
public record MpcDemoRequest(string SecretBase64, int Threshold = 3, int TotalShares = 5);
public record ShamirSplitRequest(string SecretBase64, int Threshold = 3, int TotalShares = 5);
public record ZkpProveRequest(string Circuit, string PublicInputsBase64, string WitnessBase64);

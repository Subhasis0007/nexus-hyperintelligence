using Microsoft.AspNetCore.Mvc;
using Nexus.Core.AI;
using Nexus.Models;

namespace Nexus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class AIController : ControllerBase
{
    private readonly AIService _aiService;
    private readonly IAIProviderFactory _factory;
    private readonly ILogger<AIController> _logger;

    public AIController(AIService aiService, IAIProviderFactory factory, ILogger<AIController> logger)
    {
        _aiService = aiService;
        _factory = factory;
        _logger = logger;
    }

    /// <summary>Multi-turn chat completion through the active AI provider.</summary>
    [HttpPost("chat")]
    [ProducesResponseType(typeof(ApiResponse<ChatResponse>), 200)]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request, CancellationToken ct)
    {
        if (request.Messages.Count == 0)
            return BadRequest(ApiResponse<string>.Fail("messages array must not be empty"));

        var response = await _aiService.ChatAsync(request, ct);
        return Ok(ApiResponse<ChatResponse>.Ok(response, HttpContext.TraceIdentifier));
    }

    /// <summary>Dense vector embedding of a text string.</summary>
    [HttpPost("embed")]
    [ProducesResponseType(typeof(ApiResponse<EmbedResponse>), 200)]
    public async Task<IActionResult> Embed([FromBody] EmbedRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(ApiResponse<string>.Fail("text must not be empty"));

        var embedding = await _aiService.EmbedAsync(request.Text, ct);
        var result = new EmbedResponse { Text = request.Text, Embedding = embedding, Dimensions = embedding.Length };
        return Ok(ApiResponse<EmbedResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>Summarize a long text into a concise paragraph.</summary>
    [HttpPost("summarize")]
    [ProducesResponseType(typeof(ApiResponse<SummarizeResponse>), 200)]
    public async Task<IActionResult> Summarize([FromBody] SummarizeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(ApiResponse<string>.Fail("text must not be empty"));

        var summary = await _aiService.SummarizeAsync(request.Text, ct);
        var result = new SummarizeResponse { Summary = summary, OriginalLength = request.Text.Length };
        return Ok(ApiResponse<SummarizeResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Health check showing current AI mode, provider name, model, and availability.
    /// GET /api/v1/ai/health
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(ApiResponse<AIHealthResponse>), 200)]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        IAIProvider? provider = null;
        string? error = null;
        try
        {
            provider = await _factory.GetProviderAsync(ct);
            await provider.HealthCheckAsync(ct);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            _logger.LogWarning(ex, "AI health check encountered an error");
        }

        var result = new AIHealthResponse
        {
            Mode = Environment.GetEnvironmentVariable("NEXUS_AI_MODE") ?? "offline",
            Provider = provider?.ProviderName ?? "unknown",
            Model = provider?.ModelName ?? "unknown",
            IsAvailable = provider?.IsAvailable ?? false,
            OllamaBaseUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") ?? "http://localhost:11434",
            AzureFoundryEndpoint = MaskSecret(Environment.GetEnvironmentVariable("AZURE_FOUNDRY_ENDPOINT")),
            Error = error
        };

        return Ok(ApiResponse<AIHealthResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    private static string? MaskSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Length <= 12 ? "***" : value[..8] + "***";
    }
}

// ── Request / Response DTOs ───────────────────────────────────────────────

public sealed class EmbedRequest
{
    public string Text { get; set; } = string.Empty;
}

public sealed class EmbedResponse
{
    public string Text { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public int Dimensions { get; set; }
}

public sealed class SummarizeRequest
{
    public string Text { get; set; } = string.Empty;
}

public sealed class SummarizeResponse
{
    public string Summary { get; set; } = string.Empty;
    public int OriginalLength { get; set; }
}

public sealed class AIHealthResponse
{
    public string Mode { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public string? OllamaBaseUrl { get; set; }
    public string? AzureFoundryEndpoint { get; set; }
    public string? Error { get; set; }
}

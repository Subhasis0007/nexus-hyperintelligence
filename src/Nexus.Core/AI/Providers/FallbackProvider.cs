using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Nexus.Core.AI.Providers;

/// <summary>
/// Deterministic mock provider used when no backend is available.
/// Never throws. Always returns a coherent response.
/// </summary>
public sealed class FallbackProvider : IAIProvider
{
    private readonly ILogger<FallbackProvider> _logger;

    public string ProviderName => "Fallback";
    public string ModelName => "fallback-deterministic-v1";
    public bool IsAvailable => true;

    public FallbackProvider(ILogger<FallbackProvider> logger)
    {
        _logger = logger;
        _logger.LogWarning("FallbackProvider is active — no real AI backend is available. Responses are deterministic mocks.");
    }

    public Task<bool> HealthCheckAsync(CancellationToken ct = default)
        => Task.FromResult(true);

    public Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default)
    {
        var userContent = request.Messages.LastOrDefault(m => m.Role == "user")?.Content ?? "empty";
        var seed = StableHash(userContent);
        var responses = new[]
        {
            "I understand your request. Based on the available context, this is a placeholder response while the AI backend is offline.",
            "This is a deterministic fallback response. The system is operating in offline mode without a live AI provider.",
            "Your request has been received. In offline mode, real inference is unavailable — this response is generated deterministically.",
            "The Nexus system is running in fallback mode. Configure NEXUS_AI_MODE and provider credentials to enable live inference.",
            "Acknowledged. The AI subsystem is currently using the fallback provider. Please check NEXUS_AI_MODE configuration."
        };
        return Task.FromResult(new ChatResponse
        {
            Content = responses[seed % responses.Length],
            Model = ModelName,
            Provider = ProviderName,
            InputTokens = userContent.Length / 4,
            OutputTokens = 32,
            LatencyMs = 1
        });
    }

    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        // Return a deterministic 384-dimensional unit vector seeded from the text hash
        const int Dims = 384;
        var seed = StableHash(text);
        var rng = new Random(seed);
        var vec = new float[Dims];
        double norm = 0;
        for (var i = 0; i < Dims; i++)
        {
            vec[i] = (float)(rng.NextDouble() * 2 - 1);
            norm += vec[i] * vec[i];
        }
        norm = Math.Sqrt(norm);
        for (var i = 0; i < Dims; i++)
            vec[i] = (float)(vec[i] / norm);
        return Task.FromResult(vec);
    }

    public Task<string> GenerateAsync(string prompt, int maxTokens = 512, CancellationToken ct = default)
    {
        var seed = StableHash(prompt);
        var responses = new[]
        {
            "This is a fallback-generated text response. Configure a real AI provider to get live inference.",
            "Offline mode active. The generation request has been received but cannot be fulfilled without a live backend.",
            "Nexus fallback provider: your prompt was noted but live generation requires NEXUS_AI_MODE=online or a running Ollama instance."
        };
        return Task.FromResult(responses[seed % responses.Length]);
    }

    private static int StableHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Math.Abs(BitConverter.ToInt32(bytes, 0));
    }
}

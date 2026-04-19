using Microsoft.Extensions.Logging;

namespace Nexus.Core.AI;

/// <summary>
/// High-level AI service used by agents and connectors.
/// Delegates to whichever IAIProvider is active.
/// </summary>
public sealed class AIService
{
    private readonly IAIProviderFactory _factory;
    private readonly ILogger<AIService> _logger;

    public AIService(IAIProviderFactory factory, ILogger<AIService> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    /// <summary>Multi-turn chat completion.</summary>
    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default)
    {
        var provider = await _factory.GetProviderAsync(ct);
        _logger.LogDebug("ChatAsync via {Provider}/{Model}", provider.ProviderName, provider.ModelName);
        return await provider.ChatAsync(request, ct);
    }

    /// <summary>Dense vector embedding of a text string.</summary>
    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var provider = await _factory.GetProviderAsync(ct);
        _logger.LogDebug("EmbedAsync ({Length} chars) via {Provider}", text.Length, provider.ProviderName);
        return await provider.EmbedAsync(text, ct);
    }

    /// <summary>Summarize a long text into a concise paragraph.</summary>
    public async Task<string> SummarizeAsync(string text, CancellationToken ct = default)
    {
        var request = new ChatRequest
        {
            SystemPrompt = "You are a precise summarizer. Produce a single concise paragraph that captures the key points of the provided text. Do not include preamble.",
            Messages = new()
            {
                new ChatMessage { Role = "user", Content = $"Summarize the following:\n\n{text}" }
            },
            Temperature = 0.3f,
            MaxTokens = 256
        };
        var response = await ChatAsync(request, ct);
        return response.Content;
    }

    /// <summary>Classify text into one of the provided labels.</summary>
    public async Task<string> ClassifyAsync(string text, string[] labels, CancellationToken ct = default)
    {
        if (labels.Length == 0)
            throw new ArgumentException("At least one label is required.", nameof(labels));

        var labelList = string.Join(", ", labels.Select(l => $"\"{l}\""));
        var request = new ChatRequest
        {
            SystemPrompt = $"You are a classification engine. Respond with exactly one of the following labels and nothing else: {labelList}",
            Messages = new()
            {
                new ChatMessage { Role = "user", Content = text }
            },
            Temperature = 0f,
            MaxTokens = 32
        };
        var response = await ChatAsync(request, ct);
        // Normalise: strip quotes and trim whitespace
        return response.Content.Trim().Trim('"').Trim();
    }

    /// <summary>Extract structured entities of the given type from text.</summary>
    public async Task<string> ExtractAsync(string text, string entityType, CancellationToken ct = default)
    {
        var request = new ChatRequest
        {
            SystemPrompt = $"You are an information extraction engine. Extract all {entityType} entities from the provided text and return them as a JSON array of strings. Return only the JSON array with no additional text.",
            Messages = new()
            {
                new ChatMessage { Role = "user", Content = text }
            },
            Temperature = 0f,
            MaxTokens = 512
        };
        var response = await ChatAsync(request, ct);
        return response.Content;
    }
}

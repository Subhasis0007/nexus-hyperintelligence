using System.Text.Json.Serialization;

namespace Nexus.Core.AI;

/// <summary>
/// Abstraction over any AI inference backend (Ollama, Azure AI Foundry, OpenAI, Fallback).
/// </summary>
public interface IAIProvider
{
    string ProviderName { get; }
    string ModelName { get; }
    bool IsAvailable { get; }

    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default);
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    Task<string> GenerateAsync(string prompt, int maxTokens = 512, CancellationToken ct = default);
    Task<bool> HealthCheckAsync(CancellationToken ct = default);
}

public sealed class ChatRequest
{
    public string SystemPrompt { get; set; } = string.Empty;
    public List<ChatMessage> Messages { get; set; } = new();
    public float Temperature { get; set; } = 0.7f;
    public int MaxTokens { get; set; } = 1024;
}

public sealed class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public sealed class ChatResponse
{
    public string Content { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public long LatencyMs { get; set; }
}

/// <summary>
/// Thrown when NEXUS_AI_MODE=online but no provider credentials are configured.
/// </summary>
public sealed class AIConfigurationException : InvalidOperationException
{
    public AIConfigurationException(string message) : base(message) { }
}

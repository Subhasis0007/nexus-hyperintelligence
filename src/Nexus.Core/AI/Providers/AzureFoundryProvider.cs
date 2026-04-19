using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Nexus.Core.AI.Providers;

/// <summary>
/// Calls Azure AI Foundry via OpenAI-compatible REST API.
/// AZURE_FOUNDRY_ENDPOINT  — inference endpoint URL
/// AZURE_FOUNDRY_KEY       — API key
/// AZURE_FOUNDRY_MODEL     (default: gpt-4o-mini)
/// </summary>
public sealed class AzureFoundryProvider : IAIProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<AzureFoundryProvider> _logger;
    private readonly string _endpoint;
    private readonly string _model;
    private bool _isAvailable = true;

    public string ProviderName => "AzureFoundry";
    public string ModelName => _model;
    public bool IsAvailable => _isAvailable;

    public AzureFoundryProvider(HttpClient http, ILogger<AzureFoundryProvider> logger)
    {
        _http = http;
        _logger = logger;
        _endpoint = (Environment.GetEnvironmentVariable("AZURE_FOUNDRY_ENDPOINT") ?? string.Empty).TrimEnd('/');
        _model = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_MODEL") ?? "gpt-4o-mini";
        var key = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_KEY") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(key))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
    }

    public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_endpoint))
        {
            _isAvailable = false;
            return false;
        }
        try
        {
            // Send a minimal completion to verify the endpoint responds
            var body = new OpenAIChatRequest(
                _model,
                new[] { new OpenAIMessage("user", "ping") },
                Temperature: 0f,
                MaxTokens: 1);
            var resp = await _http.PostAsJsonAsync($"{_endpoint}/chat/completions", body, ct);
            _isAvailable = resp.IsSuccessStatusCode;
            return _isAvailable;
        }
        catch (Exception ex)
        {
            _isAvailable = false;
            _logger.LogWarning(ex, "AzureFoundryProvider health check failed for endpoint {Endpoint}", _endpoint);
            return false;
        }
    }

    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var messages = BuildMessages(request);
            var body = new OpenAIChatRequest(_model, messages, request.Temperature, request.MaxTokens);
            var resp = await _http.PostAsJsonAsync($"{_endpoint}/chat/completions", body, ct);
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<OpenAIChatResponse>(cancellationToken: ct);
            sw.Stop();
            return new ChatResponse
            {
                Content = result?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty,
                Model = result?.Model ?? _model,
                Provider = ProviderName,
                InputTokens = result?.Usage?.PromptTokens ?? 0,
                OutputTokens = result?.Usage?.CompletionTokens ?? 0,
                LatencyMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _isAvailable = false;
            _logger.LogError(ex, "AzureFoundryProvider.ChatAsync failed");
            return new ChatResponse { Content = $"[AzureFoundry error: {ex.Message}]", Provider = ProviderName, Model = _model, LatencyMs = sw.ElapsedMilliseconds };
        }
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        try
        {
            var body = new OpenAIEmbedRequest("text-embedding-3-small", text);
            var resp = await _http.PostAsJsonAsync($"{_endpoint}/embeddings", body, ct);
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<OpenAIEmbedResponse>(cancellationToken: ct);
            return result?.Data?.FirstOrDefault()?.Embedding?.ToArray() ?? Array.Empty<float>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AzureFoundryProvider.EmbedAsync failed");
            return Array.Empty<float>();
        }
    }

    public async Task<string> GenerateAsync(string prompt, int maxTokens = 512, CancellationToken ct = default)
    {
        var request = new ChatRequest { Messages = new() { new ChatMessage { Role = "user", Content = prompt } }, MaxTokens = maxTokens };
        var response = await ChatAsync(request, ct);
        return response.Content;
    }

    private static OpenAIMessage[] BuildMessages(ChatRequest request)
    {
        var msgs = new List<OpenAIMessage>();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            msgs.Add(new OpenAIMessage("system", request.SystemPrompt));
        msgs.AddRange(request.Messages.Select(m => new OpenAIMessage(m.Role, m.Content)));
        return msgs.ToArray();
    }

    // ── Shared OpenAI-compatible request / response models ────────────────

    internal sealed record OpenAIChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IEnumerable<OpenAIMessage> Messages,
        [property: JsonPropertyName("temperature")] float Temperature,
        [property: JsonPropertyName("max_tokens")] int MaxTokens);

    internal sealed record OpenAIMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    internal sealed record OpenAIChatResponse(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("choices")] List<OpenAIChoice>? Choices,
        [property: JsonPropertyName("usage")] OpenAIUsage? Usage);

    internal sealed record OpenAIChoice(
        [property: JsonPropertyName("message")] OpenAIMessage? Message);

    internal sealed record OpenAIUsage(
        [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
        [property: JsonPropertyName("completion_tokens")] int CompletionTokens);

    internal sealed record OpenAIEmbedRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] string Input);

    internal sealed record OpenAIEmbedResponse(
        [property: JsonPropertyName("data")] List<OpenAIEmbedData>? Data);

    internal sealed record OpenAIEmbedData(
        [property: JsonPropertyName("embedding")] List<float>? Embedding);
}

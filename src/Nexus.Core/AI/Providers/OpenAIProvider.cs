using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Nexus.Core.AI.Providers;

/// <summary>
/// Calls the OpenAI API directly.
/// OPENAI_API_KEY  — secret API key (sk-...)
/// OPENAI_MODEL    (default: gpt-4o-mini)
/// </summary>
public sealed class OpenAIProvider : IAIProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<OpenAIProvider> _logger;
    private readonly string _model;
    private bool _isAvailable = true;

    public string ProviderName => "OpenAI";
    public string ModelName => _model;
    public bool IsAvailable => _isAvailable;

    public OpenAIProvider(HttpClient http, ILogger<OpenAIProvider> logger)
    {
        _http = http;
        _logger = logger;
        _model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";
        var key = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(key))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri("https://api.openai.com");
    }

    public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        try
        {
            // Models list endpoint is cheap and always accessible with a valid key
            var resp = await _http.GetAsync("/v1/models", ct);
            _isAvailable = resp.IsSuccessStatusCode;
            return _isAvailable;
        }
        catch (Exception ex)
        {
            _isAvailable = false;
            _logger.LogWarning(ex, "OpenAIProvider health check failed");
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
            var resp = await _http.PostAsJsonAsync("/v1/chat/completions", body, ct);
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
            _logger.LogError(ex, "OpenAIProvider.ChatAsync failed");
            return new ChatResponse { Content = $"[OpenAI error: {ex.Message}]", Provider = ProviderName, Model = _model, LatencyMs = sw.ElapsedMilliseconds };
        }
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        try
        {
            var body = new OpenAIEmbedRequest("text-embedding-3-small", text);
            var resp = await _http.PostAsJsonAsync("/v1/embeddings", body, ct);
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<OpenAIEmbedResponse>(cancellationToken: ct);
            return result?.Data?.FirstOrDefault()?.Embedding?.ToArray() ?? Array.Empty<float>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAIProvider.EmbedAsync failed");
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

    // ── Request / response models ─────────────────────────────────────────

    private sealed record OpenAIChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IEnumerable<OpenAIMessage> Messages,
        [property: JsonPropertyName("temperature")] float Temperature,
        [property: JsonPropertyName("max_tokens")] int MaxTokens);

    private sealed record OpenAIMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record OpenAIChatResponse(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("choices")] List<OpenAIChoice>? Choices,
        [property: JsonPropertyName("usage")] OpenAIUsage? Usage);

    private sealed record OpenAIChoice(
        [property: JsonPropertyName("message")] OpenAIMessage? Message);

    private sealed record OpenAIUsage(
        [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
        [property: JsonPropertyName("completion_tokens")] int CompletionTokens);

    private sealed record OpenAIEmbedRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] string Input);

    private sealed record OpenAIEmbedResponse(
        [property: JsonPropertyName("data")] List<OpenAIEmbedData>? Data);

    private sealed record OpenAIEmbedData(
        [property: JsonPropertyName("embedding")] List<float>? Embedding);
}

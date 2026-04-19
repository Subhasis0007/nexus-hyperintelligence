using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Nexus.Core.AI.Providers;

/// <summary>
/// Calls the local Ollama HTTP API.
/// OLLAMA_BASE_URL  (default: http://localhost:11434)
/// OLLAMA_MODEL     (default: qwen3)
/// OLLAMA_EMBED_MODEL (default: nomic-embed-text)
/// </summary>
public sealed class OllamaProvider : IAIProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<OllamaProvider> _logger;
    private readonly string _model;
    private readonly string _embedModel;
    private bool _isAvailable = true;

    public string ProviderName => "Ollama";
    public string ModelName => _model;
    public bool IsAvailable => _isAvailable;

    public OllamaProvider(HttpClient http, ILogger<OllamaProvider> logger)
    {
        _http = http;
        _logger = logger;
        _model = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "qwen3";
        _embedModel = Environment.GetEnvironmentVariable("OLLAMA_EMBED_MODEL") ?? "nomic-embed-text";
    }

    public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync("/api/tags", ct);
            _isAvailable = response.IsSuccessStatusCode;
            if (_isAvailable)
            {
                var tags = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(cancellationToken: ct);
                var models = tags?.Models?.Select(m => m.Name).ToList() ?? new();
                if (!models.Any(m => m.StartsWith(_model, StringComparison.OrdinalIgnoreCase)))
                    _logger.LogWarning("Ollama model '{Model}' not found. Pull it with: ollama pull {Model}", _model, _model);
            }
            return _isAvailable;
        }
        catch (Exception ex)
        {
            _isAvailable = false;
            _logger.LogWarning(ex, "Ollama health check failed — is Ollama running at {BaseAddress}?", _http.BaseAddress);
            return false;
        }
    }

    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var messages = new List<OllamaMessage>();
            if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
                messages.Add(new OllamaMessage("system", request.SystemPrompt));
            messages.AddRange(request.Messages.Select(m => new OllamaMessage(m.Role, m.Content)));

            var body = new OllamaChatRequest(
                _model,
                messages,
                Stream: false,
                new OllamaOptions(request.Temperature, request.MaxTokens));

            var resp = await _http.PostAsJsonAsync("/api/chat", body, ct);
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: ct);

            sw.Stop();
            return new ChatResponse
            {
                Content = result?.Message?.Content ?? string.Empty,
                Model = result?.Model ?? _model,
                Provider = ProviderName,
                InputTokens = result?.PromptEvalCount ?? 0,
                OutputTokens = result?.EvalCount ?? 0,
                LatencyMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _isAvailable = false;
            _logger.LogError(ex, "OllamaProvider.ChatAsync failed");
            return new ChatResponse { Content = $"[Ollama error: {ex.Message}]", Provider = ProviderName, Model = _model, LatencyMs = sw.ElapsedMilliseconds };
        }
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        try
        {
            var body = new OllamaEmbedRequest(_embedModel, text);
            var resp = await _http.PostAsJsonAsync("/api/embed", body, ct);
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<OllamaEmbedResponse>(cancellationToken: ct);
            return result?.Embeddings?.FirstOrDefault()?.ToArray() ?? Array.Empty<float>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OllamaProvider.EmbedAsync failed");
            return Array.Empty<float>();
        }
    }

    public async Task<string> GenerateAsync(string prompt, int maxTokens = 512, CancellationToken ct = default)
    {
        try
        {
            var body = new OllamaGenerateRequest(_model, prompt, Stream: false, new OllamaOptions(0.7f, maxTokens));
            var resp = await _http.PostAsJsonAsync("/api/generate", body, ct);
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken: ct);
            return result?.Response ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OllamaProvider.GenerateAsync failed");
            return $"[Ollama generate error: {ex.Message}]";
        }
    }

    // ── Private request / response models ────────────────────────────────

    private sealed record OllamaChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] List<OllamaMessage> Messages,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("options")] OllamaOptions? Options);

    private sealed record OllamaMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record OllamaOptions(
        [property: JsonPropertyName("temperature")] float Temperature,
        [property: JsonPropertyName("num_predict")] int NumPredict);

    private sealed record OllamaChatResponse(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("message")] OllamaMessage? Message,
        [property: JsonPropertyName("done")] bool Done,
        [property: JsonPropertyName("prompt_eval_count")] int PromptEvalCount,
        [property: JsonPropertyName("eval_count")] int EvalCount);

    private sealed record OllamaGenerateRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("options")] OllamaOptions? Options);

    private sealed record OllamaGenerateResponse(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("response")] string Response,
        [property: JsonPropertyName("done")] bool Done);

    private sealed record OllamaEmbedRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] string Input);

    private sealed record OllamaEmbedResponse(
        [property: JsonPropertyName("embeddings")] List<List<float>>? Embeddings);

    private sealed record OllamaTagsResponse(
        [property: JsonPropertyName("models")] List<OllamaModelInfo>? Models);

    private sealed record OllamaModelInfo(
        [property: JsonPropertyName("name")] string Name);
}

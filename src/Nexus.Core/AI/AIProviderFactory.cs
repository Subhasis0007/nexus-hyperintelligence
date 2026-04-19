using Microsoft.Extensions.Logging;
using Nexus.Core.AI.Providers;

namespace Nexus.Core.AI;

/// <summary>
/// Abstraction over the provider resolution strategy — mockable in tests.
/// </summary>
public interface IAIProviderFactory
{
    Task<IAIProvider> GetProviderAsync(CancellationToken ct = default);
    IAIProvider GetProvider();
}

/// <summary>
/// Reads NEXUS_AI_MODE and returns the appropriate IAIProvider.
///
/// offline → OllamaProvider; if unavailable → FallbackProvider
/// online  → AzureFoundryProvider (if AZURE_FOUNDRY_ENDPOINT set)
///           else OpenAIProvider (if OPENAI_API_KEY set)
///           else throws AIConfigurationException
/// auto    → AzureFoundryProvider → OllamaProvider → FallbackProvider
/// </summary>
public class AIProviderFactory : IAIProviderFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;
    private IAIProvider? _cachedProvider;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public AIProviderFactory(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
    }

    /// <summary>Returns the active provider, refreshing from env vars every 5 minutes.</summary>
    public async Task<IAIProvider> GetProviderAsync(CancellationToken ct = default)
    {
        if (_cachedProvider is not null && DateTime.UtcNow < _cacheExpiry)
            return _cachedProvider;

        var mode = (Environment.GetEnvironmentVariable("NEXUS_AI_MODE") ?? "offline").ToLowerInvariant().Trim();
        _cachedProvider = mode switch
        {
            "offline" => await ResolveOfflineAsync(ct),
            "online"  => await ResolveOnlineAsync(ct),
            "auto"    => await ResolveAutoAsync(ct),
            _         => await ResolveOfflineAsync(ct)
        };
        _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);
        return _cachedProvider;
    }

    /// <summary>Synchronous convenience wrapper — blocks the caller thread. Use only during startup.</summary>
    public IAIProvider GetProvider()
        => GetProviderAsync().GetAwaiter().GetResult();

    // ── Resolution strategies ─────────────────────────────────────────────

    private async Task<IAIProvider> ResolveOfflineAsync(CancellationToken ct)
    {
        var ollama = CreateOllamaProvider();
        if (await ollama.HealthCheckAsync(ct))
            return ollama;

        _loggerFactory.CreateLogger<AIProviderFactory>()
            .LogWarning("Ollama unavailable in offline mode — falling back to FallbackProvider");
        return new FallbackProvider(_loggerFactory.CreateLogger<FallbackProvider>());
    }

    private async Task<IAIProvider> ResolveOnlineAsync(CancellationToken ct)
    {
        var azureEndpoint = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_ENDPOINT");
        if (!string.IsNullOrWhiteSpace(azureEndpoint))
        {
            var azure = CreateAzureFoundryProvider();
            if (await azure.HealthCheckAsync(ct))
                return azure;
            throw new AIConfigurationException(
                "NEXUS_AI_MODE=online and AZURE_FOUNDRY_ENDPOINT is set, but the Azure AI Foundry endpoint is unreachable. " +
                "Verify AZURE_FOUNDRY_ENDPOINT and AZURE_FOUNDRY_KEY.");
        }

        var openaiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(openaiKey))
        {
            var openai = CreateOpenAIProvider();
            if (await openai.HealthCheckAsync(ct))
                return openai;
            throw new AIConfigurationException(
                "NEXUS_AI_MODE=online and OPENAI_API_KEY is set, but the OpenAI API is unreachable. " +
                "Verify OPENAI_API_KEY is valid.");
        }

        throw new AIConfigurationException(
            "NEXUS_AI_MODE=online but no provider credentials found. " +
            "Set AZURE_FOUNDRY_ENDPOINT + AZURE_FOUNDRY_KEY, or OPENAI_API_KEY.");
    }

    private async Task<IAIProvider> ResolveAutoAsync(CancellationToken ct)
    {
        var logger = _loggerFactory.CreateLogger<AIProviderFactory>();

        var azureEndpoint = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_ENDPOINT");
        if (!string.IsNullOrWhiteSpace(azureEndpoint))
        {
            var azure = CreateAzureFoundryProvider();
            if (await azure.HealthCheckAsync(ct))
            {
                logger.LogInformation("Auto mode: using AzureFoundryProvider");
                return azure;
            }
            logger.LogWarning("Auto mode: AzureFoundryProvider unavailable — trying Ollama");
        }

        var ollama = CreateOllamaProvider();
        if (await ollama.HealthCheckAsync(ct))
        {
            logger.LogInformation("Auto mode: using OllamaProvider");
            return ollama;
        }

        logger.LogWarning("Auto mode: all providers unavailable — using FallbackProvider");
        return new FallbackProvider(_loggerFactory.CreateLogger<FallbackProvider>());
    }

    // ── Provider constructors ─────────────────────────────────────────────

    private OllamaProvider CreateOllamaProvider()
    {
        var baseUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") ?? "http://localhost:11434";
        var http = _httpClientFactory.CreateClient("ollama");
        http.BaseAddress = new Uri(baseUrl);
        http.Timeout = TimeSpan.FromSeconds(30);
        return new OllamaProvider(http, _loggerFactory.CreateLogger<OllamaProvider>());
    }

    private AzureFoundryProvider CreateAzureFoundryProvider()
    {
        var http = _httpClientFactory.CreateClient("azure-foundry");
        http.Timeout = TimeSpan.FromSeconds(60);
        return new AzureFoundryProvider(http, _loggerFactory.CreateLogger<AzureFoundryProvider>());
    }

    private OpenAIProvider CreateOpenAIProvider()
    {
        var http = _httpClientFactory.CreateClient("openai");
        http.Timeout = TimeSpan.FromSeconds(60);
        return new OpenAIProvider(http, _loggerFactory.CreateLogger<OpenAIProvider>());
    }
}

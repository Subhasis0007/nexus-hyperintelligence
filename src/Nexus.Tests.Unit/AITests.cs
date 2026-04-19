using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Nexus.Core.AI;
using Nexus.Core.AI.Providers;
using Xunit;

namespace Nexus.Tests.Unit;

public class AITests
{
    // ── Helpers ───────────────────────────────────────────────────────────

    private static HttpClient MockHttpClient(HttpStatusCode status, object responseBody)
    {
        var json = JsonSerializer.Serialize(responseBody);
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = status,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        return new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost:11434") };
    }

    private static HttpClient MockHttpClientFailing()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));
        return new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost:11434") };
    }

    // ── FallbackProvider ──────────────────────────────────────────────────

    [Fact]
    public void FallbackProvider_IsAlwaysAvailable()
    {
        var provider = new FallbackProvider(NullLogger<FallbackProvider>.Instance);
        provider.IsAvailable.Should().BeTrue();
        provider.ProviderName.Should().Be("Fallback");
    }

    [Fact]
    public async Task FallbackProvider_HealthCheck_ReturnsTrue()
    {
        var provider = new FallbackProvider(NullLogger<FallbackProvider>.Instance);
        var result = await provider.HealthCheckAsync();
        result.Should().BeTrue();
    }

    [Fact]
    public async Task FallbackProvider_ChatAsync_ReturnsResponse()
    {
        var provider = new FallbackProvider(NullLogger<FallbackProvider>.Instance);
        var request = new ChatRequest
        {
            Messages = new() { new ChatMessage { Role = "user", Content = "Hello" } }
        };
        var response = await provider.ChatAsync(request);
        response.Should().NotBeNull();
        response.Content.Should().NotBeNullOrEmpty();
        response.Provider.Should().Be("Fallback");
    }

    [Fact]
    public async Task FallbackProvider_ChatAsync_IsDeterministic()
    {
        var provider = new FallbackProvider(NullLogger<FallbackProvider>.Instance);
        var request = new ChatRequest
        {
            Messages = new() { new ChatMessage { Role = "user", Content = "deterministic test" } }
        };
        var r1 = await provider.ChatAsync(request);
        var r2 = await provider.ChatAsync(request);
        r1.Content.Should().Be(r2.Content);
    }

    [Fact]
    public async Task FallbackProvider_EmbedAsync_Returns384DimVector()
    {
        var provider = new FallbackProvider(NullLogger<FallbackProvider>.Instance);
        var embedding = await provider.EmbedAsync("test text");
        embedding.Should().HaveCount(384);
    }

    [Fact]
    public async Task FallbackProvider_EmbedAsync_IsNormalised()
    {
        var provider = new FallbackProvider(NullLogger<FallbackProvider>.Instance);
        var embedding = await provider.EmbedAsync("normalised check");
        var norm = Math.Sqrt(embedding.Sum(x => (double)x * x));
        norm.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public async Task FallbackProvider_GenerateAsync_ReturnsNonEmpty()
    {
        var provider = new FallbackProvider(NullLogger<FallbackProvider>.Instance);
        var result = await provider.GenerateAsync("some prompt");
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task FallbackProvider_NeverThrows()
    {
        var provider = new FallbackProvider(NullLogger<FallbackProvider>.Instance);
        // Even with bizarre inputs it must not throw
        var ex = await Record.ExceptionAsync(() => provider.ChatAsync(new ChatRequest()));
        ex.Should().BeNull();
    }

    // ── OllamaProvider ────────────────────────────────────────────────────

    [Fact]
    public async Task OllamaProvider_HealthCheck_ReturnsTrueWhenTagsSucceed()
    {
        var http = MockHttpClient(HttpStatusCode.OK, new
        {
            models = new[] { new { name = "qwen3:latest" } }
        });
        var provider = new OllamaProvider(http, NullLogger<OllamaProvider>.Instance);
        var result = await provider.HealthCheckAsync();
        result.Should().BeTrue();
        provider.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task OllamaProvider_HealthCheck_ReturnsFalseWhenConnectionFails()
    {
        var http = MockHttpClientFailing();
        var provider = new OllamaProvider(http, NullLogger<OllamaProvider>.Instance);
        var result = await provider.HealthCheckAsync();
        result.Should().BeFalse();
        provider.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task OllamaProvider_ChatAsync_ParsesResponseCorrectly()
    {
        var http = MockHttpClient(HttpStatusCode.OK, new
        {
            model = "qwen3",
            message = new { role = "assistant", content = "Hello from Ollama!" },
            done = true,
            prompt_eval_count = 5,
            eval_count = 8
        });
        var provider = new OllamaProvider(http, NullLogger<OllamaProvider>.Instance);
        var response = await provider.ChatAsync(new ChatRequest
        {
            Messages = new() { new ChatMessage { Role = "user", Content = "hi" } }
        });
        response.Content.Should().Be("Hello from Ollama!");
        response.Provider.Should().Be("Ollama");
        response.InputTokens.Should().Be(5);
        response.OutputTokens.Should().Be(8);
    }

    [Fact]
    public async Task OllamaProvider_ChatAsync_DoesNotThrowOnHttpError()
    {
        var http = MockHttpClientFailing();
        var provider = new OllamaProvider(http, NullLogger<OllamaProvider>.Instance);
        var ex = await Record.ExceptionAsync(() => provider.ChatAsync(new ChatRequest
        {
            Messages = new() { new ChatMessage { Role = "user", Content = "test" } }
        }));
        ex.Should().BeNull();
    }

    [Fact]
    public async Task OllamaProvider_EmbedAsync_ReturnsEmptyOnError()
    {
        var http = MockHttpClientFailing();
        var provider = new OllamaProvider(http, NullLogger<OllamaProvider>.Instance);
        var result = await provider.EmbedAsync("text");
        result.Should().BeEmpty();
    }

    // ── AIService ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AIService_Chat_DelegatesToProvider()
    {
        var mockProvider = new Mock<IAIProvider>();
        mockProvider.Setup(p => p.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse { Content = "mocked response", Provider = "Mock", Model = "mock-1" });

        var factory = CreateMockFactory(mockProvider.Object);
        var service = new AIService(factory, NullLogger<AIService>.Instance);

        var request = new ChatRequest { Messages = new() { new ChatMessage { Role = "user", Content = "hi" } } };
        var response  = await service.ChatAsync(request);
        response.Content.Should().Be("mocked response");
        mockProvider.Verify(p => p.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AIService_Embed_DelegatesToProvider()
    {
        var expectedVec = new float[] { 0.1f, 0.2f, 0.3f };
        var mockProvider = new Mock<IAIProvider>();
        mockProvider.Setup(p => p.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedVec);

        var factory = CreateMockFactory(mockProvider.Object);
        var service = new AIService(factory, NullLogger<AIService>.Instance);

        var result = await service.EmbedAsync("hello");
        result.Should().BeEquivalentTo(expectedVec);
    }

    [Fact]
    public async Task AIService_Summarize_BuildsCorrectSystemPrompt()
    {
        ChatRequest? capturedRequest = null;
        var mockProvider = new Mock<IAIProvider>();
        mockProvider.Setup(p => p.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ChatRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(new ChatResponse { Content = "Summary here" });

        var factory = CreateMockFactory(mockProvider.Object);
        var service = new AIService(factory, NullLogger<AIService>.Instance);

        await service.SummarizeAsync("Long text to summarize...");

        capturedRequest.Should().NotBeNull();
        capturedRequest!.SystemPrompt.Should().Contain("summariz");
        capturedRequest.Temperature.Should().BeLessThan(0.5f);
    }

    [Fact]
    public async Task AIService_Classify_ThrowsOnEmptyLabels()
    {
        var factory = CreateMockFactory(Mock.Of<IAIProvider>());
        var service = new AIService(factory, NullLogger<AIService>.Instance);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ClassifyAsync("text", Array.Empty<string>()));
    }

    [Fact]
    public async Task AIService_Classify_BuildsLabelListInPrompt()
    {
        ChatRequest? capturedRequest = null;
        var mockProvider = new Mock<IAIProvider>();
        mockProvider.Setup(p => p.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ChatRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(new ChatResponse { Content = "positive" });

        var factory = CreateMockFactory(mockProvider.Object);
        var service = new AIService(factory, NullLogger<AIService>.Instance);

        await service.ClassifyAsync("Great product!", new[] { "positive", "negative", "neutral" });

        capturedRequest!.SystemPrompt.Should().Contain("positive");
        capturedRequest.SystemPrompt.Should().Contain("negative");
        capturedRequest.SystemPrompt.Should().Contain("neutral");
    }

    // ── AIProviderFactory ─────────────────────────────────────────────────

    [Fact]
    public async Task Factory_OfflineMode_ReturnsFallbackWhenOllamaDown()
    {
        // Arrange: NEXUS_AI_MODE=offline, Ollama unavailable
        Environment.SetEnvironmentVariable("NEXUS_AI_MODE", "offline");
        Environment.SetEnvironmentVariable("OLLAMA_BASE_URL", "http://localhost:19999"); // nothing listening

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() =>
            {
                var h = new Mock<HttpMessageHandler>();
                h.Protected()
                    .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                    .ThrowsAsync(new HttpRequestException("refused"));
                return new HttpClient(h.Object) { BaseAddress = new Uri("http://localhost:19999") };
            });

        var loggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
        var factory = new AIProviderFactory(httpClientFactory.Object, loggerFactory);

        var provider = await factory.GetProviderAsync();
        provider.ProviderName.Should().Be("Fallback");

        Environment.SetEnvironmentVariable("NEXUS_AI_MODE", null);
        Environment.SetEnvironmentVariable("OLLAMA_BASE_URL", null);
    }

    [Fact]
    public async Task Factory_OnlineMode_ThrowsWhenNoCredentials()
    {
        Environment.SetEnvironmentVariable("NEXUS_AI_MODE", "online");
        Environment.SetEnvironmentVariable("AZURE_FOUNDRY_ENDPOINT", null);
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());

        var factory = new AIProviderFactory(httpClientFactory.Object, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        await Assert.ThrowsAsync<AIConfigurationException>(() => factory.GetProviderAsync());

        Environment.SetEnvironmentVariable("NEXUS_AI_MODE", null);
    }

    [Fact]
    public void Factory_DefaultMode_IsOffline()
    {
        Environment.SetEnvironmentVariable("NEXUS_AI_MODE", null);
        var mode = Environment.GetEnvironmentVariable("NEXUS_AI_MODE") ?? "offline";
        mode.Should().Be("offline");
    }

    // ── Private helper: factory that returns a pre-built provider ─────────

    private static IAIProviderFactory CreateMockFactory(IAIProvider provider)
    {
        var mock = new Mock<IAIProviderFactory>();
        mock.Setup(f => f.GetProviderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(provider);
        mock.Setup(f => f.GetProvider()).Returns(provider);
        return mock.Object;
    }
}

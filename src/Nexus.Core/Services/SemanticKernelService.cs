using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using Nexus.Core.Interfaces;

namespace Nexus.Core.Services;

public class SemanticKernelService : ISemanticKernelService
{
    private readonly Kernel _kernel;
    private readonly ILogger<SemanticKernelService> _logger;

    public SemanticKernelService(ILogger<SemanticKernelService> logger)
    {
        _logger = logger;
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(new MockChatCompletionService());
#pragma warning disable SKEXP0001
        builder.Services.AddSingleton<ITextEmbeddingGenerationService>(new MockEmbeddingService());
#pragma warning restore SKEXP0001
        _kernel = builder.Build();
    }

    public async Task<string> InvokePromptAsync(string prompt, string tenantId, CancellationToken ct = default)
    {
        _logger.LogDebug("InvokePrompt tenant={TenantId} prompt_len={Len}", tenantId, prompt.Length);
        var chatService = _kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddSystemMessage("You are a Nexus HyperIntelligence assistant. Tenant: " + tenantId);
        history.AddUserMessage(prompt);
        var result = await chatService.GetChatMessageContentAsync(history, cancellationToken: ct);
        return result.Content ?? string.Empty;
    }

    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct = default)
    {
#pragma warning disable SKEXP0001
        var embeddingService = _kernel.GetRequiredService<ITextEmbeddingGenerationService>();
        var result = await embeddingService.GenerateEmbeddingAsync(text, cancellationToken: ct);
#pragma warning restore SKEXP0001
        return result.ToArray();
    }

    public Task<string> SummarizeAsync(string text, int maxTokens = 200, CancellationToken ct = default)
    {
        var words = text.Split(' ');
        var summary = string.Join(' ', words.Take(Math.Min(maxTokens / 4, words.Length)));
        return Task.FromResult(summary.Length < text.Length ? summary + "..." : summary);
    }

    public Task<List<string>> ExtractEntitiesAsync(string text, CancellationToken ct = default)
    {
        var entities = new List<string>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            if (word.Length > 4 && char.IsUpper(word[0]))
                entities.Add(word.Trim('.', ',', ';', ':'));
        }
        return Task.FromResult(entities.Distinct().ToList());
    }
}

internal sealed class MockChatCompletionService : IChatCompletionService
{
    public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>();

    public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var lastMsg = chatHistory.LastOrDefault()?.Content ?? "Hello";
        var response = $"[Nexus AI] Processed: {lastMsg[..Math.Min(50, lastMsg.Length)]}... Response generated at {DateTime.UtcNow:O}";
        IReadOnlyList<ChatMessageContent> result = [new ChatMessageContent(AuthorRole.Assistant, response)];
        return Task.FromResult(result);
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = "[Nexus AI] Streaming response chunk";
        yield return new StreamingChatMessageContent(AuthorRole.Assistant, response);
        await Task.CompletedTask;
    }
}

#pragma warning disable SKEXP0001
internal sealed class MockEmbeddingService : ITextEmbeddingGenerationService
#pragma warning restore SKEXP0001
{
    public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>();

    public Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
        IList<string> data,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var rng = new Random();
        IList<ReadOnlyMemory<float>> results = data
            .Select(_ => new ReadOnlyMemory<float>(Enumerable.Range(0, 1536).Select(__ => (float)rng.NextDouble()).ToArray()))
            .ToList();
        return Task.FromResult(results);
    }
}

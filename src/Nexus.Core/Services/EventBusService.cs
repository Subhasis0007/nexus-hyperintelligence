using Microsoft.Extensions.Logging;
using Nexus.Core.Interfaces;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Nexus.Core.Services;

public class EventBusService : IEventBusService, IDisposable
{
    private readonly ConcurrentDictionary<string, List<Func<object, Task>>> _handlers = new();
    private readonly ILogger<EventBusService> _logger;

    public EventBusService(ILogger<EventBusService> logger)
    {
        _logger = logger;
    }

    public async Task PublishAsync<T>(string topic, T message, CancellationToken ct = default) where T : class
    {
        _logger.LogDebug("Publishing to topic {Topic}", topic);
        if (_handlers.TryGetValue(topic, out var handlers))
        {
            var tasks = handlers.Select(h => h(message));
            await Task.WhenAll(tasks);
        }
    }

    public Task SubscribeAsync<T>(string topic, Func<T, Task> handler, CancellationToken ct = default) where T : class
    {
        _handlers.AddOrUpdate(
            topic,
            _ => [WrapHandler(handler)],
            (_, list) => { list.Add(WrapHandler(handler)); return list; });
        _logger.LogDebug("Subscribed to topic {Topic}", topic);
        return Task.CompletedTask;
    }

    private static Func<object, Task> WrapHandler<T>(Func<T, Task> handler) where T : class =>
        async (obj) =>
        {
            if (obj is T typed) await handler(typed);
            else if (obj is string json) { var typed2 = JsonSerializer.Deserialize<T>(json); if (typed2 != null) await handler(typed2); }
        };

    public void Dispose() { _handlers.Clear(); }
}

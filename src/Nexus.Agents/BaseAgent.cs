using Microsoft.Extensions.Logging;
using Nexus.Core.Interfaces;
using Nexus.Models;

namespace Nexus.Agents;

public abstract class BaseAgent
{
    protected readonly ILogger _logger;
    protected readonly IEventBusService _eventBus;

    public Agent AgentModel { get; protected set; }
    public string Id => AgentModel.Id;
    public string Name => AgentModel.Name;
    public AgentStatus Status => AgentModel.Status;

    protected BaseAgent(Agent model, ILogger logger, IEventBusService eventBus)
    {
        AgentModel = model;
        _logger = logger;
        _eventBus = eventBus;
    }

    public virtual async Task StartAsync(CancellationToken ct = default)
    {
        AgentModel.Status = AgentStatus.Running;
        AgentModel.LastActiveAt = DateTimeOffset.UtcNow;
        _logger.LogInformation("Agent {Name} ({Id}) started", Name, Id);
        await EmitEventAsync("agent.started");
    }

    public virtual async Task StopAsync(CancellationToken ct = default)
    {
        AgentModel.Status = AgentStatus.Idle;
        _logger.LogInformation("Agent {Name} ({Id}) stopped", Name, Id);
        await EmitEventAsync("agent.stopped");
    }

    public virtual async Task<AgentTaskResult> ExecuteTaskAsync(AgentTask task, CancellationToken ct = default)
    {
        AgentModel.MessageCount++;
        AgentModel.LastActiveAt = DateTimeOffset.UtcNow;
        await EmitEventAsync("agent.task.started", new Dictionary<string, object> { ["taskId"] = task.Id });
        var result = await ProcessTaskAsync(task, ct);
        await EmitEventAsync("agent.task.completed", new Dictionary<string, object> { ["taskId"] = task.Id, ["success"] = result.Success });
        return result;
    }

    protected abstract Task<AgentTaskResult> ProcessTaskAsync(AgentTask task, CancellationToken ct);

    protected async Task EmitEventAsync(string eventType, Dictionary<string, object>? payload = null)
    {
        var evt = AgentEvent.Create(eventType, Id, AgentModel.TenantId, payload);
        await _eventBus.PublishAsync("nexus.agents", evt);
    }
}

public class AgentTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public int Priority { get; set; } = 5;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class AgentTaskResult
{
    public string TaskId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public Dictionary<string, object> Output { get; set; } = new();
    public string? Error { get; set; }
    public TimeSpan Duration { get; set; }
}

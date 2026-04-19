using Microsoft.AspNetCore.SignalR;
using Nexus.Agents;
using Nexus.Models;

namespace Nexus.API.Hubs;

public class AgentHub : Hub
{
    private readonly SwarmOrchestrator _orchestrator;
    private readonly ILogger<AgentHub> _logger;

    public AgentHub(SwarmOrchestrator orchestrator, ILogger<AgentHub> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await Clients.Caller.SendAsync("Connected", new { connectionId = Context.ConnectionId, timestamp = DateTimeOffset.UtcNow });
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SubscribeToSwarm(string swarmId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"swarm:{swarmId}");
        await Clients.Caller.SendAsync("Subscribed", new { swarmId });
    }

    public async Task UnsubscribeFromSwarm(string swarmId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"swarm:{swarmId}");
    }

    public Task<object> GetAgentStatus(string agentId)
    {
        var agent = _orchestrator.GetAllAgents().FirstOrDefault(a => a.Id == agentId);
        if (agent == null) return Task.FromResult<object>(new { error = "Agent not found" });
        return Task.FromResult<object>(new
        {
            agentId = agent.Id,
            status = agent.Status.ToString(),
            lastActive = agent.AgentModel.LastActiveAt,
            messageCount = agent.AgentModel.MessageCount
        });
    }

    public async Task<object> ExecuteTask(string agentId, string taskType)
    {
        var agent = _orchestrator.GetAllAgents().FirstOrDefault(a => a.Id == agentId);
        if (agent == null) return new { error = "Agent not found" };
        var task = new AgentTask { Type = taskType };
        var result = await agent.ExecuteTaskAsync(task);
        await Clients.Group($"swarm:{agent.AgentModel.SwarmId}").SendAsync("AgentTaskCompleted", result);
        return result;
    }

    public async Task BroadcastToSwarm(string swarmId, string message)
    {
        await Clients.Group($"swarm:{swarmId}").SendAsync("SwarmMessage", new
        {
            swarmId,
            message,
            from = Context.ConnectionId,
            timestamp = DateTimeOffset.UtcNow
        });
    }
}

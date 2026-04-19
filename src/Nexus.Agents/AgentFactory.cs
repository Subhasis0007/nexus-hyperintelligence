using Microsoft.Extensions.Logging;
using Nexus.Core.Interfaces;
using Nexus.Models;

namespace Nexus.Agents;

/// <summary>
/// Factory for creating the 200 auto-generated agents and 16 swarms.
/// </summary>
public class AgentFactory
{
    private readonly ILogger<AgentFactory> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IEventBusService _eventBus;

    public AgentFactory(ILogger<AgentFactory> logger, ILoggerFactory loggerFactory, IEventBusService eventBus)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _eventBus = eventBus;
    }

    public BaseAgent CreateAgent(AgentCreateRequest request, string tenantId)
    {
        var model = new Agent
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            TenantId = tenantId,
            Capability = request.Capability,
            Metadata = request.Metadata ?? new(),
            Tags = request.Tags ?? new()
        };
        return new AutoAgent(model, _loggerFactory.CreateLogger<AutoAgent>(), _eventBus);
    }

    public List<BaseAgent> CreateAgentBatch(string tenantId, string swarmId, int count = 200)
    {
        var agents = new List<BaseAgent>(count);
        var capabilities = Enum.GetValues<AgentCapability>();
        for (int i = 1; i <= count; i++)
        {
            var model = new Agent
            {
                Id = $"agent-{i:D3}",
                Name = $"NexusAgent_{i:D3}",
                TenantId = tenantId,
                SwarmId = swarmId,
                Capability = capabilities[i % capabilities.Length],
                Metadata = new Dictionary<string, object>
                {
                    ["generation"] = 1,
                    ["index"] = i,
                    ["batch"] = "auto-generated"
                }
            };
            agents.Add(new AutoAgent(model, _loggerFactory.CreateLogger<AutoAgent>(), _eventBus));
        }
        _logger.LogInformation("Created batch of {Count} agents for tenant {TenantId}", count, tenantId);
        return agents;
    }

    public BaseSwarm CreateSwarm(SwarmCreateRequest request, string tenantId)
    {
        var model = new Swarm
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            TenantId = tenantId,
            Type = request.Type,
            Consensus = request.Consensus,
            MaxAgents = request.MaxAgents,
            Config = request.Config ?? new()
        };
        return CreateSwarmInstance(model);
    }

    public List<BaseSwarm> CreateAllSwarms(string tenantId)
    {
        var swarms = new List<BaseSwarm>();
        foreach (SwarmType type in Enum.GetValues<SwarmType>())
        {
            var model = new Swarm
            {
                Id = $"swarm-{type.ToString().ToLower()}",
                Name = $"{type} Swarm",
                TenantId = tenantId,
                Type = type,
                Status = SwarmStatus.Forming
            };
            swarms.Add(CreateSwarmInstance(model));
        }
        _logger.LogInformation("Created all 16 swarms for tenant {TenantId}", tenantId);
        return swarms;
    }

    private BaseSwarm CreateSwarmInstance(Swarm model) => model.Type switch
    {
        SwarmType.Analytics        => new AnalyticsSwarm(model,       _loggerFactory.CreateLogger<AnalyticsSwarm>(),        _eventBus),
        SwarmType.Security         => new SecuritySwarm(model,        _loggerFactory.CreateLogger<SecuritySwarm>(),         _eventBus),
        SwarmType.DataIngestion    => new DataIngestionSwarm(model,   _loggerFactory.CreateLogger<DataIngestionSwarm>(),    _eventBus),
        SwarmType.Intelligence     => new IntelligenceSwarm(model,    _loggerFactory.CreateLogger<IntelligenceSwarm>(),     _eventBus),
        SwarmType.Prediction       => new PredictionSwarm(model,      _loggerFactory.CreateLogger<PredictionSwarm>(),       _eventBus),
        SwarmType.Compliance       => new ComplianceSwarm(model,      _loggerFactory.CreateLogger<ComplianceSwarm>(),       _eventBus),
        SwarmType.Monitoring       => new MonitoringSwarm(model,      _loggerFactory.CreateLogger<MonitoringSwarm>(),       _eventBus),
        SwarmType.Orchestration    => new OrchestrationSwarm(model,   _loggerFactory.CreateLogger<OrchestrationSwarm>(),   _eventBus),
        SwarmType.Knowledge        => new KnowledgeSwarm(model,       _loggerFactory.CreateLogger<KnowledgeSwarm>(),        _eventBus),
        SwarmType.Communication    => new CommunicationSwarm(model,   _loggerFactory.CreateLogger<CommunicationSwarm>(),   _eventBus),
        SwarmType.Optimization     => new OptimizationSwarm(model,    _loggerFactory.CreateLogger<OptimizationSwarm>(),    _eventBus),
        SwarmType.Resilience       => new ResilienceSwarm(model,      _loggerFactory.CreateLogger<ResilienceSwarm>(),       _eventBus),
        SwarmType.Discovery        => new DiscoverySwarm(model,       _loggerFactory.CreateLogger<DiscoverySwarm>(),        _eventBus),
        SwarmType.Transformation   => new TransformationSwarm(model,  _loggerFactory.CreateLogger<TransformationSwarm>(),  _eventBus),
        SwarmType.Validation       => new ValidationSwarm(model,      _loggerFactory.CreateLogger<ValidationSwarm>(),       _eventBus),
        SwarmType.Evolution        => new EvolutionSwarm(model,       _loggerFactory.CreateLogger<EvolutionSwarm>(),        _eventBus),
        _ => throw new ArgumentOutOfRangeException(nameof(model.Type))
    };
}

/// <summary>Generic auto-generated agent used for the 200-agent batch.</summary>
public class AutoAgent : BaseAgent
{
    public AutoAgent(Agent model, ILogger<AutoAgent> logger, IEventBusService eventBus)
        : base(model, logger, eventBus) { }

    protected override async Task<AgentTaskResult> ProcessTaskAsync(AgentTask task, CancellationToken ct)
    {
        await Task.Delay(Random.Shared.Next(1, 20), ct);
        return new AgentTaskResult
        {
            TaskId = task.Id,
            Success = true,
            Output = new Dictionary<string, object>
            {
                ["agent"] = Name,
                ["capability"] = AgentModel.Capability.ToString(),
                ["processed_at"] = DateTimeOffset.UtcNow
            }
        };
    }
}

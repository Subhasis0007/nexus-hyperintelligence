using Microsoft.Extensions.Logging;
using Nexus.Core.Interfaces;
using Nexus.Models;

namespace Nexus.Agents;

public class SwarmOrchestrator
{
    private readonly Dictionary<string, BaseSwarm> _swarms = new();
    private readonly Dictionary<string, List<BaseAgent>> _swarmAgents = new();
    private readonly AgentFactory _factory;
    private readonly IEventBusService _eventBus;
    private readonly ILogger<SwarmOrchestrator> _logger;
    private readonly object _lockObj = new();

    public SwarmOrchestrator(AgentFactory factory, IEventBusService eventBus, ILogger<SwarmOrchestrator> logger)
    {
        _factory = factory;
        _eventBus = eventBus;
        _logger = logger;
    }

    public void InitializeForTenant(string tenantId)
    {
        lock (_lockObj)
        {
            var swarms = _factory.CreateAllSwarms(tenantId);
            foreach (var swarm in swarms)
            {
                _swarms[swarm.Id] = swarm;
                _swarmAgents[swarm.Id] = new List<BaseAgent>();
            }

            // Create 200 agents and distribute across swarms
            var agents = _factory.CreateAgentBatch(tenantId, "batch-init", 200);
            var swarmList = swarms.ToList();
            for (int i = 0; i < agents.Count; i++)
            {
                var swarm = swarmList[i % swarmList.Count];
                agents[i].AgentModel.SwarmId = swarm.Id;
                swarm.AddAgent(agents[i]);
                _swarmAgents[swarm.Id].Add(agents[i]);
            }
            _logger.LogInformation("Initialized 16 swarms + 200 agents for tenant {TenantId}", tenantId);
        }
    }

    public IReadOnlyList<BaseSwarm> GetSwarms() { lock (_lockObj) { return _swarms.Values.ToList(); } }
    public BaseSwarm? GetSwarm(string swarmId) { lock (_lockObj) { _swarms.TryGetValue(swarmId, out var s); return s; } }
    public IReadOnlyList<BaseAgent> GetAgents(string swarmId) { lock (_lockObj) { return _swarmAgents.GetValueOrDefault(swarmId) ?? []; } }
    public IReadOnlyList<BaseAgent> GetAllAgents() { lock (_lockObj) { return _swarmAgents.Values.SelectMany(x => x).ToList(); } }

    public async Task<SwarmExecutionResult> ExecuteSwarmTaskAsync(string swarmId, SwarmTask task, CancellationToken ct = default)
    {
        var swarm = GetSwarm(swarmId) ?? throw new KeyNotFoundException($"Swarm {swarmId} not found");
        return await swarm.ExecuteAsync(task, ct);
    }

    public async Task<ConsensusResult> RunConsensusAsync(string swarmId, string proposalId, CancellationToken ct = default)
    {
        var agents = GetAgents(swarmId);
        if (!agents.Any()) return new ConsensusResult { ProposalId = proposalId };

        var votes = agents.Select(a => new ConsensusVote
        {
            AgentId = a.Id,
            ProposalId = proposalId,
            Approve = Random.Shared.NextDouble() > 0.2, // 80% approval rate
            Reason = "consensus-sim"
        }).ToList();

        var approved = votes.Count(v => v.Approve);
        return new ConsensusResult
        {
            ProposalId = proposalId,
            Approved = approved > votes.Count / 2,
            ApproveCount = approved,
            RejectCount = votes.Count - approved,
            TotalVotes = votes.Count
        };
    }
}

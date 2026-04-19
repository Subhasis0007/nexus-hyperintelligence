using Nexus.Agents;
using Nexus.Core.Interfaces;
using Nexus.Models;

namespace Nexus.API.GraphQL;

public class NexusQuery
{
    public List<Agent> GetAgents(
        [Service] SwarmOrchestrator orchestrator,
        string tenantId = "tenant-default")
        => orchestrator.GetAllAgents()
            .Where(a => a.AgentModel.TenantId == tenantId)
            .Select(a => a.AgentModel)
            .ToList();

    public Agent? GetAgent(
        [Service] SwarmOrchestrator orchestrator,
        string agentId,
        string tenantId = "tenant-default")
        => orchestrator.GetAllAgents()
            .FirstOrDefault(a => a.Id == agentId && a.AgentModel.TenantId == tenantId)
            ?.AgentModel;

    public List<Swarm> GetSwarms(
        [Service] SwarmOrchestrator orchestrator,
        string tenantId = "tenant-default")
        => orchestrator.GetSwarms()
            .Where(s => s.SwarmModel.TenantId == tenantId)
            .Select(s => s.SwarmModel)
            .ToList();

    public async Task<List<Tenant>> GetTenantsAsync(
        [Service] ITenantService tenantService)
        => await tenantService.ListTenantsAsync();

    public async Task<KnowledgeQueryResult> QueryKnowledgeGraphAsync(
        [Service] IKnowledgeGraphService kgService,
        string tenantId = "tenant-default",
        int topK = 10)
        => await kgService.QueryAsync(new KnowledgeQuery { TenantId = tenantId, TopK = topK });
}

public class NexusMutation
{
    public Task<Agent> CreateAgentAsync(
        [Service] AgentFactory factory,
        AgentCreateRequest request,
        string tenantId = "tenant-default")
    {
        var agent = factory.CreateAgent(request, tenantId);
        return Task.FromResult(agent.AgentModel);
    }

    public async Task<Tenant> CreateTenantAsync(
        [Service] ITenantService tenantService,
        TenantCreateRequest request)
        => await tenantService.CreateTenantAsync(request);
}

public class NexusSubscription
{
    [Subscribe]
    [Topic("nexus.agents")]
    public AgentEvent OnAgentEvent([EventMessage] AgentEvent agentEvent) => agentEvent;

    [Subscribe]
    [Topic("nexus.swarms")]
    public SwarmEvent OnSwarmEvent([EventMessage] SwarmEvent swarmEvent) => swarmEvent;
}

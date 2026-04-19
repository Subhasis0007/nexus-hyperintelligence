using Microsoft.AspNetCore.Mvc;
using Nexus.Agents;
using Nexus.Models;

namespace Nexus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class SwarmsController : ControllerBase
{
    private readonly SwarmOrchestrator _orchestrator;
    private readonly ILogger<SwarmsController> _logger;

    public SwarmsController(SwarmOrchestrator orchestrator, ILogger<SwarmsController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult GetAll([FromHeader(Name = "X-Tenant-ID")] string tenantId = "tenant-default")
    {
        var swarms = _orchestrator.GetSwarms()
            .Where(s => s.SwarmModel.TenantId == tenantId)
            .Select(s => s.SwarmModel)
            .ToList();
        return Ok(ApiResponse<List<Swarm>>.Ok(swarms, HttpContext.TraceIdentifier));
    }

    [HttpGet("{swarmId}")]
    public IActionResult Get(string swarmId, [FromHeader(Name = "X-Tenant-ID")] string tenantId = "tenant-default")
    {
        var swarm = _orchestrator.GetSwarm(swarmId);
        if (swarm == null || swarm.SwarmModel.TenantId != tenantId)
            return NotFound(ApiResponse<Swarm>.Fail("Swarm not found"));
        return Ok(ApiResponse<Swarm>.Ok(swarm.SwarmModel, HttpContext.TraceIdentifier));
    }

    [HttpPost("{swarmId}/tasks")]
    public async Task<IActionResult> ExecuteTask(string swarmId, [FromBody] SwarmTask task, [FromHeader(Name = "X-Tenant-ID")] string tenantId = "tenant-default")
    {
        var swarm = _orchestrator.GetSwarm(swarmId);
        if (swarm == null || swarm.SwarmModel.TenantId != tenantId)
            return NotFound(ApiResponse<SwarmExecutionResult>.Fail("Swarm not found"));
        var result = await _orchestrator.ExecuteSwarmTaskAsync(swarmId, task);
        return Ok(ApiResponse<SwarmExecutionResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{swarmId}/consensus")]
    public async Task<IActionResult> RunConsensus(string swarmId, [FromQuery] string proposalId = "", [FromHeader(Name = "X-Tenant-ID")] string tenantId = "tenant-default")
    {
        var swarm = _orchestrator.GetSwarm(swarmId);
        if (swarm == null || swarm.SwarmModel.TenantId != tenantId)
            return NotFound(ApiResponse<ConsensusResult>.Fail("Swarm not found"));
        if (string.IsNullOrWhiteSpace(proposalId)) proposalId = Guid.NewGuid().ToString();
        var result = await _orchestrator.RunConsensusAsync(swarmId, proposalId);
        return Ok(ApiResponse<ConsensusResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{swarmId}/agents")]
    public IActionResult GetAgents(string swarmId, [FromHeader(Name = "X-Tenant-ID")] string tenantId = "tenant-default")
    {
        var agents = _orchestrator.GetAgents(swarmId).Select(a => a.AgentModel).ToList();
        return Ok(ApiResponse<List<Agent>>.Ok(agents, HttpContext.TraceIdentifier));
    }
}

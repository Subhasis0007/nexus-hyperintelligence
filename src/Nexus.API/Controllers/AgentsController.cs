using Microsoft.AspNetCore.Mvc;
using Nexus.Agents;
using Nexus.Models;

namespace Nexus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AgentsController : ControllerBase
{
    private readonly SwarmOrchestrator _orchestrator;
    private readonly AgentFactory _factory;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(SwarmOrchestrator orchestrator, AgentFactory factory, ILogger<AgentsController> logger)
    {
        _orchestrator = orchestrator;
        _factory = factory;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult GetAll([FromHeader(Name = "X-Tenant-ID")] string tenantId = "tenant-default")
    {
        var agents = _orchestrator.GetAllAgents()
            .Select(a => a.AgentModel)
            .Where(a => a.TenantId == tenantId)
            .ToList();
        return Ok(ApiResponse<List<Agent>>.Ok(agents, HttpContext.TraceIdentifier));
    }

    [HttpGet("{agentId}")]
    public IActionResult Get(string agentId, [FromHeader(Name = "X-Tenant-ID")] string tenantId = "tenant-default")
    {
        var agent = _orchestrator.GetAllAgents()
            .FirstOrDefault(a => a.Id == agentId && a.AgentModel.TenantId == tenantId);
        if (agent == null) return NotFound(ApiResponse<Agent>.Fail("Agent not found"));
        return Ok(ApiResponse<Agent>.Ok(agent.AgentModel, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    public IActionResult Create([FromBody] AgentCreateRequest request, [FromHeader(Name = "X-Tenant-ID")] string tenantId = "tenant-default")
    {
        var agent = _factory.CreateAgent(request, tenantId);
        return CreatedAtAction(nameof(Get), new { agentId = agent.Id }, ApiResponse<Agent>.Ok(agent.AgentModel, HttpContext.TraceIdentifier));
    }

    [HttpPost("{agentId}/tasks")]
    public async Task<IActionResult> ExecuteTask(string agentId, [FromBody] AgentTask task, [FromHeader(Name = "X-Tenant-ID")] string tenantId = "tenant-default")
    {
        var agent = _orchestrator.GetAllAgents().FirstOrDefault(a => a.Id == agentId && a.AgentModel.TenantId == tenantId);
        if (agent == null) return NotFound(ApiResponse<AgentTaskResult>.Fail("Agent not found"));
        var result = await agent.ExecuteTaskAsync(task);
        return Ok(ApiResponse<AgentTaskResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("stats")]
    public IActionResult GetStats([FromHeader(Name = "X-Tenant-ID")] string tenantId = "tenant-default")
    {
        var agents = _orchestrator.GetAllAgents().Where(a => a.AgentModel.TenantId == tenantId).ToList();
        var stats = new
        {
            Total = agents.Count,
            Running = agents.Count(a => a.Status == AgentStatus.Running),
            Idle = agents.Count(a => a.Status == AgentStatus.Idle),
            ByCapability = agents.GroupBy(a => a.AgentModel.Capability).ToDictionary(g => g.Key.ToString(), g => g.Count())
        };
        return Ok(ApiResponse<object>.Ok(stats, HttpContext.TraceIdentifier));
    }
}

using Microsoft.AspNetCore.Mvc;
using Nexus.Core.Interfaces;
using Nexus.Models;

namespace Nexus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ConnectorsController : ControllerBase
{
    private readonly IConnectorRegistry _registry;
    private readonly ILogger<ConnectorsController> _logger;

    public ConnectorsController(IConnectorRegistry registry, ILogger<ConnectorsController> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromHeader(Name = "X-Tenant-ID")] string tenantId = "tenant-default")
    {
        var connectors = await _registry.GetAllAsync(tenantId);
        return Ok(ApiResponse<object>.Ok(connectors, HttpContext.TraceIdentifier));
    }

    [HttpGet("{connectorId}/health")]
    public async Task<IActionResult> GetHealth(string connectorId, [FromHeader(Name = "X-Tenant-ID")] string tenantId = "tenant-default")
    {
        var connector = await _registry.GetAsync(connectorId);
        if (connector == null) return NotFound(ApiResponse<string>.Fail("Connector not found"));
        return Ok(ApiResponse<ConnectorHealthInfo>.Ok(connector.Health, HttpContext.TraceIdentifier));
    }

    [HttpPost("{connectorId}/sync")]
    public async Task<IActionResult> Sync(string connectorId, [FromHeader(Name = "X-Tenant-ID")] string tenantId = "tenant-default")
    {
        var connector = await _registry.GetAsync(connectorId);
        if (connector == null) return NotFound(ApiResponse<ConnectorSyncResult>.Fail("Connector not found"));
        var result = new ConnectorSyncResult { ConnectorId = connectorId, RecordsProcessed = 0, CompletedAt = DateTimeOffset.UtcNow };
        return Ok(ApiResponse<ConnectorSyncResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{connectorId}/records/{recordType}")]
    public async Task<IActionResult> FetchRecords(string connectorId, string recordType, [FromQuery] int max = 50, [FromHeader(Name = "X-Tenant-ID")] string tenantId = "tenant-default")
    {
        var connector = await _registry.GetAsync(connectorId);
        if (connector == null) return NotFound(ApiResponse<List<ConnectorDataRecord>>.Fail("Connector not found"));
        var records = new List<ConnectorDataRecord> { new() { ExternalId = "record-1", ConnectorId = connectorId, Fields = new(), RecordType = recordType } };
        return Ok(ApiResponse<List<ConnectorDataRecord>>.Ok(records, HttpContext.TraceIdentifier));
    }
}

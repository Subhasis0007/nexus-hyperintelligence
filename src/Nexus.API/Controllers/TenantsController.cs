using Microsoft.AspNetCore.Mvc;
using Nexus.Core.Interfaces;
using Nexus.Models;

namespace Nexus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TenantsController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(ITenantService tenantService, ILogger<TenantsController> logger)
    {
        _tenantService = tenantService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tenants = await _tenantService.ListTenantsAsync();
        return Ok(ApiResponse<List<Tenant>>.Ok(tenants, HttpContext.TraceIdentifier));
    }

    [HttpGet("{tenantId}")]
    public async Task<IActionResult> Get(string tenantId)
    {
        var tenant = await _tenantService.GetTenantAsync(tenantId);
        if (tenant == null) return NotFound(ApiResponse<Tenant>.Fail("Tenant not found"));
        return Ok(ApiResponse<Tenant>.Ok(tenant, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TenantCreateRequest request)
    {
        var tenant = await _tenantService.CreateTenantAsync(request);
        return CreatedAtAction(nameof(Get), new { tenantId = tenant.Id }, ApiResponse<Tenant>.Ok(tenant, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{tenantId}")]
    public async Task<IActionResult> Delete(string tenantId)
    {
        var deleted = await _tenantService.DeleteTenantAsync(tenantId);
        if (!deleted) return NotFound(ApiResponse<bool>.Fail("Tenant not found"));
        return Ok(ApiResponse<bool>.Ok(true, HttpContext.TraceIdentifier));
    }
}

using Nexus.Core.Interfaces;

namespace Nexus.API.Middleware;

public class TenantMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ITenantService _tenantService;
    private readonly ILogger<TenantMiddleware> _logger;

    public TenantMiddleware(RequestDelegate next, ITenantService tenantService, ILogger<TenantMiddleware> logger)
    {
        _next = next;
        _tenantService = tenantService;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip tenant check for health, metrics, swagger, graphql
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/health") || path.StartsWith("/metrics") ||
            path.StartsWith("/swagger") || path.StartsWith("/graphql") ||
            path.StartsWith("/hubs"))
        {
            await _next(context);
            return;
        }

        var tenantId = context.Request.Headers["X-Tenant-ID"].FirstOrDefault() ?? "tenant-default";
        context.Items["TenantId"] = tenantId;

        // Validate tenant exists
        var isValid = await _tenantService.ValidateTenantAsync(tenantId);
        if (!isValid)
        {
            _logger.LogWarning("Invalid or inactive tenant {TenantId} attempted access", tenantId);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = $"Tenant '{tenantId}' is not active or does not exist" });
            return;
        }

        _logger.LogDebug("Request for tenant {TenantId}: {Method} {Path}", tenantId, context.Request.Method, path);
        await _next(context);
    }
}

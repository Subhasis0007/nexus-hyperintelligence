using Microsoft.Extensions.Logging;
using Nexus.Core.Interfaces;
using Nexus.Models;

namespace Nexus.Core.Services;

public class TenantService : ITenantService
{
    private readonly Dictionary<string, Tenant> _tenants = new();
    private readonly ILogger<TenantService> _logger;
    private readonly object _lockObj = new();

    public TenantService(ILogger<TenantService> logger)
    {
        _logger = logger;
        SeedDefaultTenants();
    }

    private void SeedDefaultTenants()
    {
        var defaultTenant = new Tenant
        {
            Id = "tenant-default",
            Name = "Default Tenant",
            Slug = "default",
            Status = TenantStatus.Active,
            Tier = TenantTier.Enterprise,
            AdminEmail = "admin@nexus.local",
            Limits = new TenantLimits { MaxAgents = 200, MaxSwarms = 16, MaxConnectors = 50 }
        };
        _tenants[defaultTenant.Id] = defaultTenant;
        _logger.LogInformation("Seeded default tenant");
    }

    public Task<Tenant?> GetTenantAsync(string tenantId, CancellationToken ct = default)
    {
        lock (_lockObj)
        {
            _tenants.TryGetValue(tenantId, out var tenant);
            return Task.FromResult(tenant);
        }
    }

    public Task<Tenant> CreateTenantAsync(TenantCreateRequest request, CancellationToken ct = default)
    {
        var tenant = new Tenant
        {
            Id = $"tenant-{Guid.NewGuid():N}",
            Name = request.Name,
            Slug = request.Name.ToLowerInvariant().Replace(' ', '-'),
            AdminEmail = request.AdminEmail,
            Tier = request.Tier,
            Status = TenantStatus.Active
        };
        lock (_lockObj) { _tenants[tenant.Id] = tenant; }
        _logger.LogInformation("Created tenant {TenantId} ({Name})", tenant.Id, tenant.Name);
        return Task.FromResult(tenant);
    }

    public Task<Tenant> UpdateTenantAsync(string tenantId, Action<Tenant> updater, CancellationToken ct = default)
    {
        lock (_lockObj)
        {
            if (!_tenants.TryGetValue(tenantId, out var tenant))
                throw new KeyNotFoundException($"Tenant {tenantId} not found");
            updater(tenant);
            return Task.FromResult(tenant);
        }
    }

    public Task<bool> DeleteTenantAsync(string tenantId, CancellationToken ct = default)
    {
        lock (_lockObj)
        {
            var removed = _tenants.Remove(tenantId);
            if (removed) _logger.LogInformation("Deleted tenant {TenantId}", tenantId);
            return Task.FromResult(removed);
        }
    }

    public Task<bool> ValidateTenantAsync(string tenantId, CancellationToken ct = default)
    {
        lock (_lockObj)
        {
            if (!_tenants.TryGetValue(tenantId, out var tenant))
                return Task.FromResult(false);
            return Task.FromResult(tenant.Status == TenantStatus.Active);
        }
    }

    public Task<List<Tenant>> ListTenantsAsync(CancellationToken ct = default)
    {
        lock (_lockObj) { return Task.FromResult(_tenants.Values.ToList()); }
    }
}

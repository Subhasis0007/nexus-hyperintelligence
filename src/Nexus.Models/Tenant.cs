namespace Nexus.Models;

public enum TenantStatus { Active, Suspended, Pending }
public enum TenantTier { Free, Standard, Professional, Enterprise }

public class Tenant
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public TenantStatus Status { get; set; } = TenantStatus.Active;
    public TenantTier Tier { get; set; } = TenantTier.Standard;
    public string AdminEmail { get; set; } = string.Empty;
    public TenantLimits Limits { get; set; } = new();
    public TenantUsage Usage { get; set; } = new();
    public Dictionary<string, string> Settings { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SuspendedAt { get; set; }
}

public class TenantLimits
{
    public int MaxAgents { get; set; } = 50;
    public int MaxSwarms { get; set; } = 5;
    public int MaxConnectors { get; set; } = 10;
    public long MaxStorageBytes { get; set; } = 10L * 1024 * 1024 * 1024; // 10 GB
    public int MaxApiCallsPerMinute { get; set; } = 1000;
}

public class TenantUsage
{
    public int AgentCount { get; set; }
    public int SwarmCount { get; set; }
    public int ConnectorCount { get; set; }
    public long StorageBytes { get; set; }
    public long ApiCallsThisMinute { get; set; }
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}

public class TenantCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string AdminEmail { get; set; } = string.Empty;
    public TenantTier Tier { get; set; } = TenantTier.Standard;
}

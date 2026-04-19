namespace Nexus.Models;

public record AgentEvent(
    string EventId,
    string EventType,
    string AgentId,
    string TenantId,
    DateTimeOffset Timestamp,
    Dictionary<string, object> Payload
)
{
    public static AgentEvent Create(string eventType, string agentId, string tenantId, Dictionary<string, object>? payload = null) =>
        new(Guid.NewGuid().ToString(), eventType, agentId, tenantId, DateTimeOffset.UtcNow, payload ?? new());
}

public record SwarmEvent(
    string EventId,
    string EventType,
    string SwarmId,
    string TenantId,
    DateTimeOffset Timestamp,
    Dictionary<string, object> Payload
)
{
    public static SwarmEvent Create(string eventType, string swarmId, string tenantId, Dictionary<string, object>? payload = null) =>
        new(Guid.NewGuid().ToString(), eventType, swarmId, tenantId, DateTimeOffset.UtcNow, payload ?? new());
}

public record ConnectorEvent(
    string EventId,
    string EventType,
    string ConnectorId,
    string TenantId,
    DateTimeOffset Timestamp,
    Dictionary<string, object> Payload
);

public class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Error { get; init; }
    public string? TraceId { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public static ApiResponse<T> Ok(T data, string? traceId = null) =>
        new() { Success = true, Data = data, TraceId = traceId };

    public static ApiResponse<T> Fail(string error, string? traceId = null) =>
        new() { Success = false, Error = error, TraceId = traceId };
}

public class PagedResult<T>
{
    public List<T> Items { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

public class HealthStatus
{
    public string Status { get; set; } = "healthy";
    public string Version { get; set; } = "1.0.0";
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, ComponentHealth> Components { get; set; } = new();
}

public class ComponentHealth
{
    public bool IsHealthy { get; set; }
    public string? Error { get; set; }
    public double ResponseTimeMs { get; set; }
}

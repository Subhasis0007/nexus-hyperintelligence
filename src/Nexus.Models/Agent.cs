namespace Nexus.Models;

public enum AgentStatus { Idle, Running, Paused, Error, Terminated }
public enum AgentCapability { Analytics, Security, DataIngestion, Intelligence, Prediction, Compliance, Monitoring, Orchestration, Knowledge, Communication, Optimization, Resilience, Discovery, Transformation, Validation, Evolution }

public class Agent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string SwarmId { get; set; } = string.Empty;
    public AgentStatus Status { get; set; } = AgentStatus.Idle;
    public AgentCapability Capability { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastActiveAt { get; set; }
    public long MessageCount { get; set; }
    public double ConfidenceScore { get; set; } = 1.0;
    public string? ParentAgentId { get; set; }
    public List<string> ChildAgentIds { get; set; } = new();
}

public class AgentCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public AgentCapability Capability { get; set; }
    public string? SwarmId { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public List<string>? Tags { get; set; }
}

public class AgentUpdateRequest
{
    public AgentStatus? Status { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public List<string>? Tags { get; set; }
}

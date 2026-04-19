namespace Nexus.Models;

public enum SwarmType
{
    Analytics, Security, DataIngestion, Intelligence, Prediction,
    Compliance, Monitoring, Orchestration, Knowledge, Communication,
    Optimization, Resilience, Discovery, Transformation, Validation, Evolution
}

public enum SwarmStatus { Forming, Active, Suspended, Terminated }

public enum ConsensusAlgorithm { Raft, PBFT, Gossip, BFT }

public class Swarm
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public SwarmType Type { get; set; }
    public SwarmStatus Status { get; set; } = SwarmStatus.Forming;
    public ConsensusAlgorithm Consensus { get; set; } = ConsensusAlgorithm.Raft;
    public List<string> AgentIds { get; set; } = new();
    public int MaxAgents { get; set; } = 20;
    public Dictionary<string, object> Config { get; set; } = new();
    public SwarmMetrics Metrics { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class SwarmMetrics
{
    public long TasksCompleted { get; set; }
    public long TasksFailed { get; set; }
    public double AverageLatencyMs { get; set; }
    public double ThroughputPerSec { get; set; }
    public int ActiveAgentCount { get; set; }
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}

public class SwarmCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public SwarmType Type { get; set; }
    public ConsensusAlgorithm Consensus { get; set; } = ConsensusAlgorithm.Raft;
    public int MaxAgents { get; set; } = 20;
    public Dictionary<string, object>? Config { get; set; }
}

public class ConsensusVote
{
    public string AgentId { get; set; } = string.Empty;
    public string ProposalId { get; set; } = string.Empty;
    public bool Approve { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

public class ConsensusResult
{
    public string ProposalId { get; set; } = string.Empty;
    public bool Approved { get; set; }
    public int ApproveCount { get; set; }
    public int RejectCount { get; set; }
    public int TotalVotes { get; set; }
    public double ApprovalRatio => TotalVotes > 0 ? (double)ApproveCount / TotalVotes : 0;
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
}

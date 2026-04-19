namespace Nexus.Models;

public class KnowledgeNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = new();
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public List<KnowledgeEdge> OutgoingEdges { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class KnowledgeEdge
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourceNodeId { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
    public string RelationType { get; set; } = string.Empty;
    public double Weight { get; set; } = 1.0;
    public Dictionary<string, object> Properties { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class KnowledgeQuery
{
    public string TenantId { get; set; } = string.Empty;
    public string? Cypher { get; set; }
    public float[]? EmbeddingVector { get; set; }
    public int TopK { get; set; } = 10;
    public double MinSimilarity { get; set; } = 0.7;
    public List<string> NodeTypes { get; set; } = new();
}

public class KnowledgeQueryResult
{
    public List<KnowledgeNode> Nodes { get; set; } = new();
    public List<KnowledgeEdge> Edges { get; set; } = new();
    public List<double> Scores { get; set; } = new();
    public long ExecutionTimeMs { get; set; }
}

public class WasmAgent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Description { get; set; } = string.Empty;
    public byte[] Module { get; set; } = Array.Empty<byte>();
    public string ModuleHash { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public WasmAgentManifest Manifest { get; set; } = new();
    public DateTimeOffset PublishedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class WasmAgentManifest
{
    public string EntryPoint { get; set; } = "run";
    public List<string> Imports { get; set; } = new();
    public List<string> Exports { get; set; } = new();
    public Dictionary<string, string> Capabilities { get; set; } = new();
    public long MaxMemoryBytes { get; set; } = 64 * 1024 * 1024; // 64 MB
}

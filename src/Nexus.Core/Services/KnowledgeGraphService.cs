using Microsoft.Extensions.Logging;
using Nexus.Core.Interfaces;
using Nexus.Models;

namespace Nexus.Core.Services;

public class KnowledgeGraphService : IKnowledgeGraphService
{
    private readonly Dictionary<string, KnowledgeNode> _nodes = new();
    private readonly Dictionary<string, KnowledgeEdge> _edges = new();
    private readonly ILogger<KnowledgeGraphService> _logger;
    private readonly object _lockObj = new();

    public KnowledgeGraphService(ILogger<KnowledgeGraphService> logger)
    {
        _logger = logger;
    }

    public Task<KnowledgeNode> UpsertNodeAsync(KnowledgeNode node, CancellationToken ct = default)
    {
        node.UpdatedAt = DateTimeOffset.UtcNow;
        lock (_lockObj) { _nodes[node.Id] = node; }
        _logger.LogDebug("Upserted node {NodeId} type={Type}", node.Id, node.Type);
        return Task.FromResult(node);
    }

    public Task<KnowledgeEdge> UpsertEdgeAsync(KnowledgeEdge edge, CancellationToken ct = default)
    {
        lock (_lockObj) { _edges[edge.Id] = edge; }
        _logger.LogDebug("Upserted edge {EdgeId} {Relation}", edge.Id, edge.RelationType);
        return Task.FromResult(edge);
    }

    public Task<KnowledgeNode?> GetNodeAsync(string nodeId, string tenantId, CancellationToken ct = default)
    {
        lock (_lockObj)
        {
            _nodes.TryGetValue(nodeId, out var node);
            if (node != null && node.TenantId != tenantId) return Task.FromResult<KnowledgeNode?>(null);
            return Task.FromResult(node);
        }
    }

    public Task<KnowledgeQueryResult> QueryAsync(KnowledgeQuery query, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        List<KnowledgeNode> nodes;
        lock (_lockObj)
        {
            nodes = _nodes.Values
                .Where(n => n.TenantId == query.TenantId)
                .Where(n => !query.NodeTypes.Any() || query.NodeTypes.Contains(n.Type))
                .Take(query.TopK)
                .ToList();
        }

        if (query.EmbeddingVector is { Length: > 0 })
        {
            var scored = nodes
                .Select(n => (node: n, score: CosineSimilarity(query.EmbeddingVector, n.Embedding)))
                .Where(x => x.score >= query.MinSimilarity)
                .OrderByDescending(x => x.score)
                .ToList();
            nodes = scored.Select(x => x.node).ToList();
        }

        var edgeIds = nodes.SelectMany(n => n.OutgoingEdges.Select(e => e.Id)).ToHashSet();
        List<KnowledgeEdge> edges;
        lock (_lockObj) { edges = _edges.Values.Where(e => edgeIds.Contains(e.Id)).ToList(); }

        sw.Stop();
        return Task.FromResult(new KnowledgeQueryResult
        {
            Nodes = nodes,
            Edges = edges,
            ExecutionTimeMs = sw.ElapsedMilliseconds
        });
    }

    public Task<bool> DeleteNodeAsync(string nodeId, string tenantId, CancellationToken ct = default)
    {
        lock (_lockObj)
        {
            if (_nodes.TryGetValue(nodeId, out var node) && node.TenantId == tenantId)
                return Task.FromResult(_nodes.Remove(nodeId));
            return Task.FromResult(false);
        }
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0) return 0;
        var len = Math.Min(a.Length, b.Length);
        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < len; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        return normA == 0 || normB == 0 ? 0 : dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}

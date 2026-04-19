using Nexus.Models;

namespace Nexus.Core.Interfaces;

public interface IConnector
{
    string ConnectorId { get; }
    string ConnectorName { get; }
    ConnectorType Type { get; }
    Task<bool> TestConnectionAsync(CancellationToken ct = default);
    Task<ConnectorSyncResult> SyncAsync(CancellationToken ct = default);
    Task<List<ConnectorDataRecord>> FetchRecordsAsync(string recordType, int maxRecords = 100, CancellationToken ct = default);
    Task<ConnectorHealthInfo> GetHealthAsync(CancellationToken ct = default);
}

public interface IConnectorRegistry
{
    Task RegisterAsync(Connector connector);
    Task UnregisterAsync(string connectorId);
    Task<Connector?> GetAsync(string connectorId);
    Task<List<Connector>> GetAllAsync(string tenantId);
    Task<List<Connector>> GetByTypeAsync(ConnectorType type, string tenantId);
}

public interface ITenantService
{
    Task<Tenant?> GetTenantAsync(string tenantId, CancellationToken ct = default);
    Task<Tenant> CreateTenantAsync(TenantCreateRequest request, CancellationToken ct = default);
    Task<Tenant> UpdateTenantAsync(string tenantId, Action<Tenant> updater, CancellationToken ct = default);
    Task<bool> DeleteTenantAsync(string tenantId, CancellationToken ct = default);
    Task<bool> ValidateTenantAsync(string tenantId, CancellationToken ct = default);
    Task<List<Tenant>> ListTenantsAsync(CancellationToken ct = default);
}

public interface ISemanticKernelService
{
    Task<string> InvokePromptAsync(string prompt, string tenantId, CancellationToken ct = default);
    Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct = default);
    Task<string> SummarizeAsync(string text, int maxTokens = 200, CancellationToken ct = default);
    Task<List<string>> ExtractEntitiesAsync(string text, CancellationToken ct = default);
}

public interface IKnowledgeGraphService
{
    Task<KnowledgeNode> UpsertNodeAsync(KnowledgeNode node, CancellationToken ct = default);
    Task<KnowledgeEdge> UpsertEdgeAsync(KnowledgeEdge edge, CancellationToken ct = default);
    Task<KnowledgeNode?> GetNodeAsync(string nodeId, string tenantId, CancellationToken ct = default);
    Task<KnowledgeQueryResult> QueryAsync(KnowledgeQuery query, CancellationToken ct = default);
    Task<bool> DeleteNodeAsync(string nodeId, string tenantId, CancellationToken ct = default);
}

public interface IEventBusService
{
    Task PublishAsync<T>(string topic, T message, CancellationToken ct = default) where T : class;
    Task SubscribeAsync<T>(string topic, Func<T, Task> handler, CancellationToken ct = default) where T : class;
}

using Nexus.Core.Interfaces;
using Nexus.Models;

namespace Nexus.Connectors.Base;

/// <summary>Abstract base implementing the standard IConnector contract.</summary>
public abstract class BaseConnector : IConnector
{
    protected readonly Connector _config;
    private bool _initialized;

    protected BaseConnector(Connector config) => _config = config;

    public string Id => _config.Id;
    public string Name => _config.Name;
    public ConnectorType Type => _config.Type;
    public ConnectorStatus Status => _config.Status;

    public virtual async Task InitializeAsync(CancellationToken ct = default)
    {
        _config.Status = ConnectorStatus.Initializing;
        await OnInitializeAsync(ct);
        _config.Status = ConnectorStatus.Active;
        _initialized = true;
    }

    protected abstract Task OnInitializeAsync(CancellationToken ct);

    public abstract Task<ConnectorHealthInfo> GetHealthAsync(CancellationToken ct = default);

    public abstract Task<IEnumerable<ConnectorDataRecord>> FetchRecordsAsync(
        string entity, IDictionary<string, string>? filters = null, CancellationToken ct = default);

    public abstract Task<ConnectorSyncResult> SyncAsync(
        string entity, IEnumerable<ConnectorDataRecord> records, CancellationToken ct = default);

    public virtual Task DisconnectAsync(CancellationToken ct = default)
    {
        _config.Status = ConnectorStatus.Inactive;
        return Task.CompletedTask;
    }

    protected void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException($"Connector '{Name}' has not been initialized. Call InitializeAsync() first.");
    }

    protected static ConnectorDataRecord MakeRecord(string id, string entity, IDictionary<string, object?> fields) =>
        new() { Id = id, EntityType = entity, Fields = fields, FetchedAt = DateTimeOffset.UtcNow };
}

using Nexus.Connectors.Base;
using Nexus.Models;

namespace Nexus.Connectors.SAP;

/// <summary>SAP connector using a local mock client — no live SAP system required.</summary>
public sealed class SapConnector : BaseConnector
{
    private readonly SapMockClient _client;

    public SapConnector(Connector config) : base(config)
        => _client = new SapMockClient(config.BaseUrl ?? "https://sap-mock.local");

    protected override Task OnInitializeAsync(CancellationToken ct)
        => _client.AuthenticateAsync(ct);

    public override async Task<ConnectorHealthInfo> GetHealthAsync(CancellationToken ct = default)
    {
        var ok = await _client.PingAsync(ct);
        return new ConnectorHealthInfo
        {
            ConnectorId = Id,
            IsHealthy = ok,
            LastChecked = DateTimeOffset.UtcNow,
            LatencyMs = ok ? 12 : -1,
            Details = new() { ["system"] = "SAP S/4HANA Mock", ["version"] = "2023.1" }
        };
    }

    public override async Task<IEnumerable<ConnectorDataRecord>> FetchRecordsAsync(
        string entity, IDictionary<string, string>? filters = null, CancellationToken ct = default)
    {
        EnsureInitialized();
        return await _client.FetchAsync(entity, filters, ct);
    }

    public override async Task<ConnectorSyncResult> SyncAsync(
        string entity, IEnumerable<ConnectorDataRecord> records, CancellationToken ct = default)
    {
        EnsureInitialized();
        return await _client.UpsertAsync(entity, records, ct);
    }
}

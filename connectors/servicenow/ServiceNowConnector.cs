using Nexus.Connectors.Base;
using Nexus.Models;

namespace Nexus.Connectors.ServiceNow;

public sealed class ServiceNowConnector : BaseConnector
{
    private readonly ServiceNowMockClient _client;

    public ServiceNowConnector(Connector config) : base(config)
        => _client = new ServiceNowMockClient(config.BaseUrl ?? "https://dev-mock.service-now.com");

    protected override Task OnInitializeAsync(CancellationToken ct)
        => _client.AuthenticateAsync(ct);

    public override async Task<ConnectorHealthInfo> GetHealthAsync(CancellationToken ct = default) =>
        new()
        {
            ConnectorId = Id,
            IsHealthy = await _client.PingAsync(ct),
            LastChecked = DateTimeOffset.UtcNow,
            LatencyMs = 22,
            Details = new() { ["instance"] = "nexus-dev.service-now.com", ["apiVersion"] = "v2" }
        };

    public override async Task<IEnumerable<ConnectorDataRecord>> FetchRecordsAsync(
        string entity, IDictionary<string, string>? filters = null, CancellationToken ct = default)
    {
        EnsureInitialized();
        return await _client.FetchTableAsync(entity, filters, ct);
    }

    public override async Task<ConnectorSyncResult> SyncAsync(
        string entity, IEnumerable<ConnectorDataRecord> records, CancellationToken ct = default)
    {
        EnsureInitialized();
        return await _client.UpsertAsync(entity, records, ct);
    }
}

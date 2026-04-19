using Nexus.Connectors.Base;
using Nexus.Models;

namespace Nexus.Connectors.Salesforce;

public sealed class SalesforceConnector : BaseConnector
{
    private readonly SalesforceMockClient _client;

    public SalesforceConnector(Connector config) : base(config)
        => _client = new SalesforceMockClient(config.BaseUrl ?? "https://sf-mock.salesforce.com");

    protected override Task OnInitializeAsync(CancellationToken ct)
        => _client.OAuth2LoginAsync(ct);

    public override async Task<ConnectorHealthInfo> GetHealthAsync(CancellationToken ct = default) =>
        new()
        {
            ConnectorId = Id,
            IsHealthy = await _client.PingAsync(ct),
            LastChecked = DateTimeOffset.UtcNow,
            LatencyMs = 18,
            Details = new() { ["org"] = "nexus-dev.salesforce.com", ["apiVersion"] = "v60.0" }
        };

    public override async Task<IEnumerable<ConnectorDataRecord>> FetchRecordsAsync(
        string entity, IDictionary<string, string>? filters = null, CancellationToken ct = default)
    {
        EnsureInitialized();
        return await _client.QueryAsync(entity, filters, ct);
    }

    public override async Task<ConnectorSyncResult> SyncAsync(
        string entity, IEnumerable<ConnectorDataRecord> records, CancellationToken ct = default)
    {
        EnsureInitialized();
        return await _client.UpsertAsync(entity, records, ct);
    }
}

using Nexus.Models;

namespace Nexus.Connectors.SAP;

/// <summary>Simulates SAP OData/BAPI calls without a live SAP backend.</summary>
public sealed class SapMockClient
{
    private readonly string _baseUrl;
    private bool _authenticated;
    private static readonly Random _rng = new();

    // SAP entity definitions: entity name → list of field templates
    private static readonly Dictionary<string, Func<int, Dictionary<string, object?>>> _entityGenerators = new()
    {
        ["PurchaseOrder"] = i => new()
        {
            ["PurchaseOrderId"] = $"PO-{i:D8}",
            ["Vendor"] = $"VENDOR-{_rng.Next(1, 50):D4}",
            ["Amount"] = Math.Round(_rng.NextDouble() * 50000 + 100, 2),
            ["Currency"] = "USD",
            ["Status"] = new[] { "OPEN", "IN_PROCESS", "COMPLETE" }[_rng.Next(3)],
            ["CreatedDate"] = DateTimeOffset.UtcNow.AddDays(-_rng.Next(365)).ToString("O")
        },
        ["SalesOrder"] = i => new()
        {
            ["SalesOrderId"] = $"SO-{i:D8}",
            ["Customer"] = $"CUST-{_rng.Next(1, 200):D4}",
            ["NetValue"] = Math.Round(_rng.NextDouble() * 100000, 2),
            ["Currency"] = "USD",
            ["DeliveryDate"] = DateTimeOffset.UtcNow.AddDays(_rng.Next(1, 90)).ToString("O"),
            ["Status"] = new[] { "NEW", "PROCESSING", "SHIPPED", "DELIVERED" }[_rng.Next(4)]
        },
        ["Material"] = i => new()
        {
            ["MaterialNumber"] = $"MAT-{i:D8}",
            ["Description"] = $"Material {i} Description",
            ["MaterialGroup"] = $"MG-{_rng.Next(1, 20):D2}",
            ["BaseUnit"] = new[] { "EA", "KG", "L", "M" }[_rng.Next(4)],
            ["Plant"] = $"PLANT-{_rng.Next(1, 5):D4}"
        }
    };

    public SapMockClient(string baseUrl) => _baseUrl = baseUrl;

    public Task AuthenticateAsync(CancellationToken ct = default)
    {
        _authenticated = true;
        return Task.CompletedTask;
    }

    public Task<bool> PingAsync(CancellationToken ct = default)
        => Task.FromResult(_authenticated);

    public Task<IEnumerable<ConnectorDataRecord>> FetchAsync(
        string entity, IDictionary<string, string>? filters, CancellationToken ct = default)
    {
        var generator = _entityGenerators.TryGetValue(entity, out var gen)
            ? gen
            : i => new Dictionary<string, object?> { ["Id"] = $"{entity}-{i}", ["Data"] = $"Generic {entity} record {i}" };

        var count = filters?.ContainsKey("top") == true ? int.Parse(filters["top"]) : 20;
        var records = Enumerable.Range(1, count)
            .Select(i => new ConnectorDataRecord
            {
                Id = $"sap-{entity.ToLower()}-{i:D6}",
                EntityType = entity,
                Fields = generator(i),
                FetchedAt = DateTimeOffset.UtcNow,
                Source = $"{_baseUrl}/odata/v4/{entity}({i})"
            });

        return Task.FromResult(records);
    }

    public Task<ConnectorSyncResult> UpsertAsync(
        string entity, IEnumerable<ConnectorDataRecord> records, CancellationToken ct = default)
    {
        var list = records.ToList();
        return Task.FromResult(new ConnectorSyncResult
        {
            ConnectorId = "sap",
            Entity = entity,
            RecordsProcessed = list.Count,
            RecordsCreated = list.Count / 2,
            RecordsUpdated = list.Count - list.Count / 2,
            RecordsFailed = 0,
            SyncedAt = DateTimeOffset.UtcNow
        });
    }
}

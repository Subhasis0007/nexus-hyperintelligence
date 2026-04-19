using Nexus.Models;

namespace Nexus.Connectors.Salesforce;

public sealed class SalesforceMockClient
{
    private readonly string _instanceUrl;
    private bool _authenticated;
    private static readonly Random _rng = new();

    public SalesforceMockClient(string instanceUrl) => _instanceUrl = instanceUrl;

    public Task OAuth2LoginAsync(CancellationToken ct = default) { _authenticated = true; return Task.CompletedTask; }
    public Task<bool> PingAsync(CancellationToken ct = default) => Task.FromResult(_authenticated);

    public Task<IEnumerable<ConnectorDataRecord>> QueryAsync(
        string entity, IDictionary<string, string>? filters, CancellationToken ct = default)
    {
        var count = filters?.ContainsKey("limit") == true ? int.Parse(filters["limit"]) : 25;
        IEnumerable<ConnectorDataRecord> records = entity switch
        {
            "Account" => Enumerable.Range(1, count).Select(i => MakeAccount(i)),
            "Opportunity" => Enumerable.Range(1, count).Select(i => MakeOpportunity(i)),
            "Contact" => Enumerable.Range(1, count).Select(i => MakeContact(i)),
            "Lead" => Enumerable.Range(1, count).Select(i => MakeLead(i)),
            _ => Enumerable.Range(1, count).Select(i => new ConnectorDataRecord
            {
                Id = $"sf-{entity.ToLower()}-{i:D6}",
                EntityType = entity,
                Fields = new Dictionary<string, object?> { ["Id"] = $"SF{i:D15}", ["Name"] = $"{entity} {i}" },
                FetchedAt = DateTimeOffset.UtcNow
            })
        };
        return Task.FromResult(records);
    }

    public Task<ConnectorSyncResult> UpsertAsync(string entity, IEnumerable<ConnectorDataRecord> records, CancellationToken ct = default)
    {
        var list = records.ToList();
        return Task.FromResult(new ConnectorSyncResult
        {
            ConnectorId = "salesforce", Entity = entity,
            RecordsProcessed = list.Count, RecordsCreated = list.Count / 3,
            RecordsUpdated = list.Count * 2 / 3, RecordsFailed = 0, SyncedAt = DateTimeOffset.UtcNow
        });
    }

    private ConnectorDataRecord MakeAccount(int i) => new()
    {
        Id = $"sf-account-{i:D6}", EntityType = "Account",
        Fields = new()
        {
            ["Id"] = $"001{i:D15}", ["Name"] = $"Company {i} Inc.",
            ["Industry"] = new[] { "Technology", "Finance", "Healthcare", "Retail" }[_rng.Next(4)],
            ["AnnualRevenue"] = (object?)(_rng.NextDouble() * 1e8),
            ["NumberOfEmployees"] = _rng.Next(10, 50000),
            ["BillingCity"] = new[] { "San Francisco", "New York", "Chicago", "Austin" }[_rng.Next(4)]
        },
        FetchedAt = DateTimeOffset.UtcNow
    };

    private ConnectorDataRecord MakeOpportunity(int i) => new()
    {
        Id = $"sf-opp-{i:D6}", EntityType = "Opportunity",
        Fields = new()
        {
            ["Id"] = $"006{i:D15}", ["Name"] = $"Opportunity {i}",
            ["Amount"] = (object?)Math.Round(_rng.NextDouble() * 500000, 2),
            ["StageName"] = new[] { "Prospecting", "Qualification", "Proposal", "Closed Won", "Closed Lost" }[_rng.Next(5)],
            ["CloseDate"] = DateTimeOffset.UtcNow.AddDays(_rng.Next(1, 180)).ToString("yyyy-MM-dd"),
            ["Probability"] = _rng.Next(10, 100)
        },
        FetchedAt = DateTimeOffset.UtcNow
    };

    private ConnectorDataRecord MakeContact(int i) => new()
    {
        Id = $"sf-contact-{i:D6}", EntityType = "Contact",
        Fields = new()
        {
            ["Id"] = $"003{i:D15}", ["FirstName"] = $"First{i}", ["LastName"] = $"Last{i}",
            ["Email"] = $"contact{i}@example.com", ["Phone"] = $"+1-555-{_rng.Next(1000, 9999)}-{_rng.Next(1000, 9999)}",
            ["Title"] = new[] { "CEO", "CTO", "VP Engineering", "Director" }[_rng.Next(4)]
        },
        FetchedAt = DateTimeOffset.UtcNow
    };

    private ConnectorDataRecord MakeLead(int i) => new()
    {
        Id = $"sf-lead-{i:D6}", EntityType = "Lead",
        Fields = new()
        {
            ["Id"] = $"00Q{i:D15}", ["FirstName"] = $"Lead{i}", ["LastName"] = $"Surname{i}",
            ["Company"] = $"LeadCo {i}", ["Status"] = new[] { "New", "Working", "Converted" }[_rng.Next(3)],
            ["LeadSource"] = new[] { "Web", "Phone", "Email", "Partner" }[_rng.Next(4)]
        },
        FetchedAt = DateTimeOffset.UtcNow
    };
}

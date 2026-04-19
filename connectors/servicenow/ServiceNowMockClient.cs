using Nexus.Models;

namespace Nexus.Connectors.ServiceNow;

public sealed class ServiceNowMockClient
{
    private readonly string _instanceUrl;
    private bool _authenticated;
    private static readonly Random _rng = new();

    public ServiceNowMockClient(string instanceUrl) => _instanceUrl = instanceUrl;

    public Task AuthenticateAsync(CancellationToken ct = default) { _authenticated = true; return Task.CompletedTask; }
    public Task<bool> PingAsync(CancellationToken ct = default) => Task.FromResult(_authenticated);

    public Task<IEnumerable<ConnectorDataRecord>> FetchTableAsync(
        string table, IDictionary<string, string>? filters, CancellationToken ct = default)
    {
        var count = filters?.ContainsKey("sysparm_limit") == true ? int.Parse(filters["sysparm_limit"]) : 20;
        IEnumerable<ConnectorDataRecord> records = table switch
        {
            "incident" => Enumerable.Range(1, count).Select(i => MakeIncident(i)),
            "change_request" => Enumerable.Range(1, count).Select(i => MakeChangeRequest(i)),
            "problem" => Enumerable.Range(1, count).Select(i => MakeProblem(i)),
            _ => Enumerable.Range(1, count).Select(i => new ConnectorDataRecord
            {
                Id = $"snow-{table}-{i:D6}", EntityType = table,
                Fields = new() { ["sys_id"] = Guid.NewGuid().ToString("N"), ["number"] = $"{table.ToUpper()[0..4]}{i:D7}" },
                FetchedAt = DateTimeOffset.UtcNow
            })
        };
        return Task.FromResult(records);
    }

    public Task<ConnectorSyncResult> UpsertAsync(string table, IEnumerable<ConnectorDataRecord> records, CancellationToken ct = default)
    {
        var list = records.ToList();
        return Task.FromResult(new ConnectorSyncResult
        {
            ConnectorId = "servicenow", Entity = table,
            RecordsProcessed = list.Count, RecordsCreated = list.Count / 4,
            RecordsUpdated = list.Count * 3 / 4, RecordsFailed = 0, SyncedAt = DateTimeOffset.UtcNow
        });
    }

    private ConnectorDataRecord MakeIncident(int i) => new()
    {
        Id = $"snow-incident-{i:D6}", EntityType = "incident",
        Fields = new()
        {
            ["sys_id"] = Guid.NewGuid().ToString("N"),
            ["number"] = $"INC{i:D7}",
            ["short_description"] = $"Incident {i}: Service degradation detected",
            ["priority"] = new[] { "1 - Critical", "2 - High", "3 - Moderate", "4 - Low" }[_rng.Next(4)],
            ["state"] = new[] { "New", "In Progress", "Resolved", "Closed" }[_rng.Next(4)],
            ["assigned_to"] = $"user{_rng.Next(1, 50)}",
            ["category"] = new[] { "Software", "Hardware", "Network", "Database" }[_rng.Next(4)],
            ["opened_at"] = DateTimeOffset.UtcNow.AddHours(-_rng.Next(1, 720)).ToString("O")
        },
        FetchedAt = DateTimeOffset.UtcNow
    };

    private ConnectorDataRecord MakeChangeRequest(int i) => new()
    {
        Id = $"snow-change-{i:D6}", EntityType = "change_request",
        Fields = new()
        {
            ["sys_id"] = Guid.NewGuid().ToString("N"),
            ["number"] = $"CHG{i:D7}",
            ["short_description"] = $"Change Request {i}",
            ["type"] = new[] { "Standard", "Normal", "Emergency" }[_rng.Next(3)],
            ["state"] = new[] { "Draft", "Assess", "Authorize", "Scheduled", "Implement", "Closed" }[_rng.Next(6)],
            ["risk"] = new[] { "Low", "Medium", "High", "Very High" }[_rng.Next(4)],
            ["planned_start_date"] = DateTimeOffset.UtcNow.AddDays(_rng.Next(1, 30)).ToString("O")
        },
        FetchedAt = DateTimeOffset.UtcNow
    };

    private ConnectorDataRecord MakeProblem(int i) => new()
    {
        Id = $"snow-problem-{i:D6}", EntityType = "problem",
        Fields = new()
        {
            ["sys_id"] = Guid.NewGuid().ToString("N"),
            ["number"] = $"PRB{i:D7}",
            ["short_description"] = $"Problem {i}: Root cause investigation",
            ["state"] = new[] { "Open", "Root Cause Analysis", "Fix in Progress", "Resolved", "Closed" }[_rng.Next(5)],
            ["priority"] = _rng.Next(1, 5),
            ["known_error"] = _rng.Next(2) == 1
        },
        FetchedAt = DateTimeOffset.UtcNow
    };
}

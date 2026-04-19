namespace Nexus.Models;

public enum ConnectorType { SAP, Salesforce, ServiceNow, Custom, REST, GraphQL, MQTT, Kafka }
public enum ConnectorStatus { Disconnected, Connecting, Connected, Error, Active }
public enum ConnectorAuthType { ApiKey, OAuth2, BasicAuth, Certificate }

public class Connector
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public ConnectorType Type { get; set; }
    public ConnectorStatus Status { get; set; } = ConnectorStatus.Disconnected;
    public ConnectorAuthType AuthType { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public Dictionary<string, string> Config { get; set; } = new();
    public ConnectorHealthInfo Health { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastSyncAt { get; set; }
}

public class ConnectorHealthInfo
{
    public bool IsHealthy { get; set; }
    public string? LastError { get; set; }
    public double ResponseTimeMs { get; set; }
    public int SuccessfulCallsLast24h { get; set; }
    public int FailedCallsLast24h { get; set; }
    public DateTimeOffset LastChecked { get; set; } = DateTimeOffset.UtcNow;
}

public class ConnectorCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public ConnectorType Type { get; set; }
    public ConnectorAuthType AuthType { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public Dictionary<string, string>? Config { get; set; }
}

public class ConnectorDataRecord
{
    public string ConnectorId { get; set; } = string.Empty;
    public string RecordType { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public Dictionary<string, object> Fields { get; set; } = new();
    public DateTimeOffset ExtractedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ConnectorSyncResult
{
    public string ConnectorId { get; set; } = string.Empty;
    public int RecordsProcessed { get; set; }
    public int RecordsFailed { get; set; }
    public TimeSpan Duration { get; set; }
    public List<string> Errors { get; set; } = new();
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
}

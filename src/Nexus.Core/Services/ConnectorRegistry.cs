using Microsoft.Extensions.Logging;
using Nexus.Core.Interfaces;
using Nexus.Models;

namespace Nexus.Core.Services;

public class ConnectorRegistry : IConnectorRegistry
{
    private readonly Dictionary<string, Connector> _connectors = new();
    private readonly ILogger<ConnectorRegistry> _logger;
    private readonly object _lockObj = new();

    public ConnectorRegistry(ILogger<ConnectorRegistry> logger)
    {
        _logger = logger;
    }

    public Task RegisterAsync(Connector connector)
    {
        lock (_lockObj)
        {
            _connectors[connector.Id] = connector;
            _logger.LogInformation("Registered connector {ConnectorId} ({Name})", connector.Id, connector.Name);
            return Task.CompletedTask;
        }
    }

    public Task UnregisterAsync(string connectorId)
    {
        lock (_lockObj)
        {
            if (_connectors.Remove(connectorId))
                _logger.LogInformation("Unregistered connector {ConnectorId}", connectorId);
            return Task.CompletedTask;
        }
    }

    public Task<Connector?> GetAsync(string connectorId)
    {
        lock (_lockObj)
        {
            _connectors.TryGetValue(connectorId, out var connector);
            return Task.FromResult(connector);
        }
    }

    public Task<List<Connector>> GetAllAsync(string tenantId)
    {
        lock (_lockObj) { return Task.FromResult(_connectors.Values.Where(c => c.TenantId == tenantId).ToList()); }
    }

    public Task<List<Connector>> GetByTypeAsync(ConnectorType type, string tenantId)
    {
        lock (_lockObj) { return Task.FromResult(_connectors.Values.Where(c => c.Type == type && c.TenantId == tenantId).ToList()); }
    }

}

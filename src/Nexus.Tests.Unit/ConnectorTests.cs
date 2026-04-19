using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Core.Services;
using Nexus.Models;
using Xunit;

namespace Nexus.Tests.Unit;

public class ConnectorRegistryTests
{
    private static ConnectorRegistry MakeRegistry() => new(NullLogger<ConnectorRegistry>.Instance);

    private static Connector MakeConnector(string id, ConnectorType type = ConnectorType.SAP) => new()
    {
        Id = id,
        Name = $"Connector_{id}",
        Type = type,
        TenantId = "tenant-test",
        BaseUrl = "https://example.com",
        Status = ConnectorStatus.Active
    };

    [Fact]
    public async Task Register_CanRetrieve()
    {
        var reg = MakeRegistry();
        var conn = MakeConnector("c1");
        await reg.RegisterAsync(conn);

        (await reg.GetAsync("c1")).Should().NotBeNull();
    }

    [Fact]
    public async Task Register_DuplicateId_Overwrites()
    {
        var reg = MakeRegistry();
        await reg.RegisterAsync(MakeConnector("dup", ConnectorType.SAP));
        await reg.RegisterAsync(MakeConnector("dup", ConnectorType.ServiceNow));

        var got = await reg.GetAsync("dup");
        got!.Type.Should().Be(ConnectorType.ServiceNow);
    }

    [Fact]
    public async Task Unregister_RemovesConnector()
    {
        var reg = MakeRegistry();
        await reg.RegisterAsync(MakeConnector("rem"));

        await reg.UnregisterAsync("rem");

        (await reg.GetAsync("rem")).Should().BeNull();
    }

    [Fact]
    public async Task GetAll_FiltersByTenant()
    {
        var reg = MakeRegistry();
        var c1 = MakeConnector("a1"); c1.TenantId = "tenant-A";
        var c2 = MakeConnector("b1"); c2.TenantId = "tenant-B";
        await reg.RegisterAsync(c1);
        await reg.RegisterAsync(c2);

        (await reg.GetAllAsync("tenant-A")).Should().HaveCount(1);
    }

    [Theory]
    [InlineData(ConnectorType.SAP)]
    [InlineData(ConnectorType.Salesforce)]
    [InlineData(ConnectorType.ServiceNow)]
    [InlineData(ConnectorType.REST)]
    [InlineData(ConnectorType.GraphQL)]
    [InlineData(ConnectorType.MQTT)]
    [InlineData(ConnectorType.Kafka)]
    public async Task Register_AllConnectorTypes(ConnectorType type)
    {
        var reg = MakeRegistry();
        await reg.RegisterAsync(MakeConnector($"ct-{type}", type));

        var got = await reg.GetAsync($"ct-{type}");
        got!.Type.Should().Be(type);
    }
}

public class EventBusServiceTests
{
    private static EventBusService MakeService() => new(NullLogger<EventBusService>.Instance);

    [Fact]
    public async Task SubscribeAsync_ReceivesPublishedMessage()
    {
        var svc = MakeService();
        AgentEvent? received = null;

        await svc.SubscribeAsync<AgentEvent>("nexus.test", e =>
        {
            received = e;
            return Task.CompletedTask;
        });

        var evt = AgentEvent.Create("TestEvent", "a1", "tenant-test");
        await svc.PublishAsync("nexus.test", evt);

        received.Should().NotBeNull();
        received!.AgentId.Should().Be("a1");
    }

    [Fact]
    public async Task Publish_NoSubscriber_DoesNotThrow()
    {
        var svc = MakeService();
        var evt = AgentEvent.Create("NoOp", "a1", "tenant-test");

        await svc.Invoking(s => s.PublishAsync("no.subscribers", evt)).Should().NotThrowAsync();
    }

    [Fact]
    public async Task Publish_MultipleSubscribers_AllReceive()
    {
        var svc = MakeService();
        var count = 0;

        await svc.SubscribeAsync<AgentEvent>("t", _ => { count++; return Task.CompletedTask; });
        await svc.SubscribeAsync<AgentEvent>("t", _ => { count++; return Task.CompletedTask; });

        await svc.PublishAsync("t", AgentEvent.Create("Multi", "a1", "tenant-test"));

        count.Should().Be(2);
    }
}

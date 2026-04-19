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

    // ── Registration ─────────────────────────────────────────────────────
    [Fact] public async Task Register_CanRetrieve()
    {
        var reg = MakeRegistry();
        var conn = MakeConnector("c1");
        await reg.RegisterAsync(conn);
        (await reg.GetAsync("c1")).Should().NotBeNull();
    }

    [Fact] public async Task Register_SetsStatus()
    {
        var reg = MakeRegistry();
        await reg.RegisterAsync(MakeConnector("c2"));
        var got = await reg.GetAsync("c2");
        got!.Status.Should().Be(ConnectorStatus.Active);
    }

    [Fact] public async Task GetAll_ReturnsRegistered()
    {
        var reg = MakeRegistry();
        await reg.RegisterAsync(MakeConnector("c3"));
        await reg.RegisterAsync(MakeConnector("c4", ConnectorType.Salesforce));
        var all = await reg.GetAllAsync("tenant-test");
        all.Should().HaveCount(2);
    }

    [Fact] public async Task Get_Unknown_ReturnsNull()
    {
        var reg = MakeRegistry();
        (await reg.GetAsync("no-such-connector")).Should().BeNull();
    }

    [Fact] public async Task Register_DuplicateId_Overwrites()
    {
        var reg = MakeRegistry();
        await reg.RegisterAsync(MakeConnector("dup"));
        var updated = MakeConnector("dup", ConnectorType.ServiceNow);
        await reg.RegisterAsync(updated);
        var got = await reg.GetAsync("dup");
        got!.Type.Should().Be(ConnectorType.ServiceNow);
    }

    [Fact] public async Task Unregister_RemovesConnector()
    {
        var reg = MakeRegistry();
        await reg.RegisterAsync(MakeConnector("rem"));
        await reg.UnregisterAsync("rem");
        (await reg.GetAsync("rem")).Should().BeNull();
    }

    [Fact] public async Task GetAll_FiltersByTenant()
    {
        var reg = MakeRegistry();
        var c1 = MakeConnector("t1c1"); c1.TenantId = "tenant-A";
        var c2 = MakeConnector("t2c2"); c2.TenantId = "tenant-B";
        await reg.RegisterAsync(c1);
        await reg.RegisterAsync(c2);
        (await reg.GetAllAsync("tenant-A")).Should().HaveCount(1);
    }

    // ── Connector types ───────────────────────────────────────────────────
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

    [Fact] public async Task Subscribe_ReceivesPublishedMessage()
    {
        var svc = MakeService();
        AgentEvent? received = null;
        svc.Subscribe<AgentEvent>("nexus.test", e => { received = e; return Task.CompletedTask; });
        var evt = new AgentEvent { AgentId = "a1", EventType = "TestEvent" };
        await svc.PublishAsync("nexus.test", evt);
        received.Should().NotBeNull();
        received!.AgentId.Should().Be("a1");
    }

    [Fact] public async Task Publish_NoSubscriber_DoesNotThrow()
    {
        var svc = MakeService();
        await svc.Invoking(s => s.PublishAsync("no.subscribers", new AgentEvent())).Should().NotThrowAsync();
    }

    [Fact] public async Task Subscribe_MultipleTopics_ReceivesCorrect()
    {
        var svc = MakeService();
        var received = new List<string>();
        svc.Subscribe<AgentEvent>("topic.a", e => { received.Add("a"); return Task.CompletedTask; });
        svc.Subscribe<AgentEvent>("topic.b", e => { received.Add("b"); return Task.CompletedTask; });
        await svc.PublishAsync("topic.a", new AgentEvent());
        received.Should().ContainSingle().Which.Should().Be("a");
    }

    [Fact] public async Task Publish_MultipleSubscribers_AllReceive()
    {
        var svc = MakeService();
        int count = 0;
        svc.Subscribe<AgentEvent>("t", _ => { count++; return Task.CompletedTask; });
        svc.Subscribe<AgentEvent>("t", _ => { count++; return Task.CompletedTask; });
        await svc.PublishAsync("t", new AgentEvent());
        count.Should().Be(2);
    }

    [Fact] public async Task PublishSwarmEvent_IsReceived()
    {
        var svc = MakeService();
        SwarmEvent? got = null;
        svc.Subscribe<SwarmEvent>("nexus.swarms", e => { got = e; return Task.CompletedTask; });
        await svc.PublishAsync("nexus.swarms", new SwarmEvent { SwarmId = "s1", EventType = "Consensus" });
        got!.SwarmId.Should().Be("s1");
    }
}

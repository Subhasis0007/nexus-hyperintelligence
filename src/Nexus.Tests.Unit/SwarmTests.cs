using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nexus.Agents;
using Nexus.Core.Interfaces;
using Nexus.Models;
using Xunit;

namespace Nexus.Tests.Unit;

public class SwarmTests
{
    private readonly Mock<IEventBusService> _mockBus = new();

    public SwarmTests()
    {
        _mockBus.Setup(e => e.PublishAsync(It.IsAny<string>(), It.IsAny<AgentEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockBus.Setup(e => e.PublishAsync(It.IsAny<string>(), It.IsAny<SwarmEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static Swarm MakeSwarmModel(SwarmType type, int n = 1) => new()
    {
        Id = $"swarm-{n:D3}",
        Name = $"{type}Swarm",
        TenantId = "tenant-test",
        Type = type,
        Status = SwarmStatus.Initializing
    };

    private AutoAgent MakeAgent(int n) => new(
        new Agent { Id = $"agent-{n}", Name = $"Agent{n}", TenantId = "tenant-test", Capability = AgentCapability.Analytics, Status = AgentStatus.Idle },
        NullLogger<AutoAgent>.Instance,
        _mockBus.Object);

    // ── 16 swarm types can be instantiated ───────────────────────────────
    [Theory]
    [InlineData(SwarmType.Analytics)]
    [InlineData(SwarmType.Security)]
    [InlineData(SwarmType.DataIngestion)]
    [InlineData(SwarmType.Intelligence)]
    [InlineData(SwarmType.Prediction)]
    [InlineData(SwarmType.Compliance)]
    [InlineData(SwarmType.Monitoring)]
    [InlineData(SwarmType.Orchestration)]
    [InlineData(SwarmType.Knowledge)]
    [InlineData(SwarmType.Communication)]
    [InlineData(SwarmType.Optimization)]
    [InlineData(SwarmType.Resilience)]
    [InlineData(SwarmType.Discovery)]
    [InlineData(SwarmType.Transformation)]
    [InlineData(SwarmType.Validation)]
    [InlineData(SwarmType.Evolution)]
    public void Swarm_CanBeInstantiated(SwarmType type)
    {
        var swarm = SwarmFactory.CreateSwarm(MakeSwarmModel(type), _mockBus.Object);
        swarm.Should().NotBeNull();
    }

    [Theory]
    [InlineData(SwarmType.Analytics)]
    [InlineData(SwarmType.Security)]
    [InlineData(SwarmType.DataIngestion)]
    [InlineData(SwarmType.Intelligence)]
    [InlineData(SwarmType.Prediction)]
    [InlineData(SwarmType.Compliance)]
    [InlineData(SwarmType.Monitoring)]
    [InlineData(SwarmType.Orchestration)]
    [InlineData(SwarmType.Knowledge)]
    [InlineData(SwarmType.Communication)]
    [InlineData(SwarmType.Optimization)]
    [InlineData(SwarmType.Resilience)]
    [InlineData(SwarmType.Discovery)]
    [InlineData(SwarmType.Transformation)]
    [InlineData(SwarmType.Validation)]
    [InlineData(SwarmType.Evolution)]
    public async Task Swarm_CanExecuteTask(SwarmType type)
    {
        var swarm = SwarmFactory.CreateSwarm(MakeSwarmModel(type), _mockBus.Object);
        swarm.AddAgent(MakeAgent(1));
        swarm.AddAgent(MakeAgent(2));
        var result = await swarm.ExecuteAsync(new SwarmTask { Type = "sample" });
        result.Success.Should().BeTrue();
    }

    // ── Agent management ──────────────────────────────────────────────────
    [Fact]
    public void Swarm_AgentCount_StartsAtZero()
    {
        var swarm = SwarmFactory.CreateSwarm(MakeSwarmModel(SwarmType.Analytics), _mockBus.Object);
        swarm.AgentCount.Should().Be(0);
    }

    [Fact]
    public void Swarm_AddAgent_IncrementsCount()
    {
        var swarm = SwarmFactory.CreateSwarm(MakeSwarmModel(SwarmType.Analytics), _mockBus.Object);
        swarm.AddAgent(MakeAgent(1));
        swarm.AgentCount.Should().Be(1);
    }

    [Fact]
    public void Swarm_Add10Agents_CountIs10()
    {
        var swarm = SwarmFactory.CreateSwarm(MakeSwarmModel(SwarmType.Security), _mockBus.Object);
        for (int i = 0; i < 10; i++) swarm.AddAgent(MakeAgent(i));
        swarm.AgentCount.Should().Be(10);
    }

    [Fact]
    public void Swarm_HasSwarmId()
    {
        var swarm = SwarmFactory.CreateSwarm(MakeSwarmModel(SwarmType.Analytics, 7), _mockBus.Object);
        swarm.Id.Should().Be("swarm-007");
    }

    [Fact]
    public void Swarm_HasSwarmName()
    {
        var swarm = SwarmFactory.CreateSwarm(MakeSwarmModel(SwarmType.Analytics), _mockBus.Object);
        swarm.Name.Should().Be("AnalyticsSwarm");
    }

    // ── Consensus ─────────────────────────────────────────────────────────
    [Fact]
    public async Task Swarm_Consensus_ReturnsResult()
    {
        var swarm = SwarmFactory.CreateSwarm(MakeSwarmModel(SwarmType.Analytics), _mockBus.Object);
        for (int i = 0; i < 5; i++) swarm.AddAgent(MakeAgent(i));
        var result = await swarm.RunConsensusAsync("proposal-1", "Should we deploy?");
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Swarm_ExecuteTask_HasOutput()
    {
        var swarm = SwarmFactory.CreateSwarm(MakeSwarmModel(SwarmType.Analytics), _mockBus.Object);
        swarm.AddAgent(MakeAgent(1));
        var result = await swarm.ExecuteAsync(new SwarmTask { Type = "analyse", Parameters = new() { ["data"] = "test" } });
        result.Output.Should().NotBeNull();
    }

    [Fact]
    public async Task Swarm_ExecuteTask_HasSwarmId()
    {
        var swarm = SwarmFactory.CreateSwarm(MakeSwarmModel(SwarmType.Analytics), _mockBus.Object);
        swarm.AddAgent(MakeAgent(1));
        var result = await swarm.ExecuteAsync(new SwarmTask { Type = "analyse" });
        result.SwarmId.Should().Be("swarm-001");
    }
}

// ── AgentFactory tests ────────────────────────────────────────────────────
public class AgentFactoryTests
{
    private readonly Mock<IEventBusService> _mockBus = new();

    public AgentFactoryTests()
    {
        _mockBus.Setup(e => e.PublishAsync(It.IsAny<string>(), It.IsAny<AgentEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public void Factory_Creates200Agents()
    {
        var factory = new AgentFactory(NullLogger<AgentFactory>.Instance, _mockBus.Object);
        var agents = factory.CreateAgentBatch("tenant-test", "swarm-001", 200);
        agents.Should().HaveCount(200);
    }

    [Fact]
    public void Factory_AgentsHaveUniqueIds()
    {
        var factory = new AgentFactory(NullLogger<AgentFactory>.Instance, _mockBus.Object);
        var agents = factory.CreateAgentBatch("tenant-test", "swarm-001", 200);
        agents.Select(a => a.Id).Distinct().Should().HaveCount(200);
    }

    [Fact]
    public void Factory_AgentsHaveTenantId()
    {
        var factory = new AgentFactory(NullLogger<AgentFactory>.Instance, _mockBus.Object);
        var agents = factory.CreateAgentBatch("my-tenant", "swarm-001", 10);
        agents.All(a => a.AgentModel.TenantId == "my-tenant").Should().BeTrue();
    }

    [Fact]
    public void Factory_Creates16Swarms()
    {
        var factory = new AgentFactory(NullLogger<AgentFactory>.Instance, _mockBus.Object);
        var swarms = factory.CreateAllSwarms("tenant-test");
        swarms.Should().HaveCount(16);
    }

    [Fact]
    public void Factory_SwarmTypesAreAllUnique()
    {
        var factory = new AgentFactory(NullLogger<AgentFactory>.Instance, _mockBus.Object);
        var swarms = factory.CreateAllSwarms("tenant-test");
        swarms.Select(s => s.SwarmModel.Type).Distinct().Should().HaveCount(16);
    }
}

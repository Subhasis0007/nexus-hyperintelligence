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
        _mockBus.Setup(e => e.PublishAsync(It.IsAny<string>(), It.IsAny<SwarmEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static Swarm MakeSwarmModel(SwarmType type, int n = 1) => new()
    {
        Id = $"swarm-{n:D3}",
        Name = $"{type}Swarm",
        TenantId = "tenant-test",
        Type = type,
        Status = SwarmStatus.Forming
    };

    private AutoAgent MakeAgent(int n) => new(
        new Agent { Id = $"agent-{n}", Name = $"Agent{n}", TenantId = "tenant-test", Capability = AgentCapability.Analytics, Status = AgentStatus.Idle },
        NullLogger<AutoAgent>.Instance,
        _mockBus.Object);

    private BaseSwarm MakeSwarm(SwarmType type)
    {
        return type switch
        {
            SwarmType.Analytics => new AnalyticsSwarm(MakeSwarmModel(type), NullLogger<AnalyticsSwarm>.Instance, _mockBus.Object),
            SwarmType.Security => new SecuritySwarm(MakeSwarmModel(type), NullLogger<SecuritySwarm>.Instance, _mockBus.Object),
            SwarmType.DataIngestion => new DataIngestionSwarm(MakeSwarmModel(type), NullLogger<DataIngestionSwarm>.Instance, _mockBus.Object),
            SwarmType.Intelligence => new IntelligenceSwarm(MakeSwarmModel(type), NullLogger<IntelligenceSwarm>.Instance, _mockBus.Object),
            SwarmType.Prediction => new PredictionSwarm(MakeSwarmModel(type), NullLogger<PredictionSwarm>.Instance, _mockBus.Object),
            SwarmType.Compliance => new ComplianceSwarm(MakeSwarmModel(type), NullLogger<ComplianceSwarm>.Instance, _mockBus.Object),
            SwarmType.Monitoring => new MonitoringSwarm(MakeSwarmModel(type), NullLogger<MonitoringSwarm>.Instance, _mockBus.Object),
            SwarmType.Orchestration => new OrchestrationSwarm(MakeSwarmModel(type), NullLogger<OrchestrationSwarm>.Instance, _mockBus.Object),
            SwarmType.Knowledge => new KnowledgeSwarm(MakeSwarmModel(type), NullLogger<KnowledgeSwarm>.Instance, _mockBus.Object),
            SwarmType.Communication => new CommunicationSwarm(MakeSwarmModel(type), NullLogger<CommunicationSwarm>.Instance, _mockBus.Object),
            SwarmType.Optimization => new OptimizationSwarm(MakeSwarmModel(type), NullLogger<OptimizationSwarm>.Instance, _mockBus.Object),
            SwarmType.Resilience => new ResilienceSwarm(MakeSwarmModel(type), NullLogger<ResilienceSwarm>.Instance, _mockBus.Object),
            SwarmType.Discovery => new DiscoverySwarm(MakeSwarmModel(type), NullLogger<DiscoverySwarm>.Instance, _mockBus.Object),
            SwarmType.Transformation => new TransformationSwarm(MakeSwarmModel(type), NullLogger<TransformationSwarm>.Instance, _mockBus.Object),
            SwarmType.Validation => new ValidationSwarm(MakeSwarmModel(type), NullLogger<ValidationSwarm>.Instance, _mockBus.Object),
            SwarmType.Evolution => new EvolutionSwarm(MakeSwarmModel(type), NullLogger<EvolutionSwarm>.Instance, _mockBus.Object),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
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
    public void Swarm_CanBeInstantiated(SwarmType type)
    {
        var swarm = MakeSwarm(type);
        swarm.Should().NotBeNull();
        swarm.Type.Should().Be(type);
    }

    [Fact]
    public async Task Swarm_CanExecuteTask_WithAgents()
    {
        var swarm = MakeSwarm(SwarmType.Analytics);
        swarm.AddAgent(MakeAgent(1));
        swarm.AddAgent(MakeAgent(2));

        var result = await swarm.ExecuteAsync(new SwarmTask { Type = "sample" });
        result.SwarmId.Should().Be(swarm.Id);
        result.SucceededCount.Should().Be(2);
    }

    [Fact]
    public void Swarm_AddAgent_TracksAgentIds()
    {
        var swarm = MakeSwarm(SwarmType.Security);
        var agent = MakeAgent(1);
        swarm.AddAgent(agent);

        swarm.SwarmModel.AgentIds.Should().Contain(agent.Id);
    }
}

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
        var factory = new AgentFactory(NullLogger<AgentFactory>.Instance, NullLoggerFactory.Instance, _mockBus.Object);
        var agents = factory.CreateAgentBatch("tenant-test", "swarm-001", 200);
        agents.Should().HaveCount(200);
    }

    [Fact]
    public void Factory_AgentsHaveUniqueIds()
    {
        var factory = new AgentFactory(NullLogger<AgentFactory>.Instance, NullLoggerFactory.Instance, _mockBus.Object);
        var agents = factory.CreateAgentBatch("tenant-test", "swarm-001", 200);
        agents.Select(a => a.Id).Distinct().Should().HaveCount(200);
    }

    [Fact]
    public void Factory_Creates16Swarms()
    {
        var factory = new AgentFactory(NullLogger<AgentFactory>.Instance, NullLoggerFactory.Instance, _mockBus.Object);
        var swarms = factory.CreateAllSwarms("tenant-test");
        swarms.Should().HaveCount(16);
    }
}

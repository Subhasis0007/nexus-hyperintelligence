using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nexus.Agents;
using Nexus.Core.Interfaces;
using Nexus.Models;
using Xunit;

namespace Nexus.Tests.Unit;

public class AgentTests
{
    private readonly Mock<IEventBusService> _mockEventBus = new();
    private readonly ILogger<AutoAgent> _nullLogger = NullLogger<AutoAgent>.Instance;

    public AgentTests()
    {
        _mockEventBus.Setup(e => e.PublishAsync(It.IsAny<string>(), It.IsAny<AgentEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private Agent MakeAgentModel(int n = 1, AgentCapability cap = AgentCapability.Analytics) => new()
    {
        Id = $"agent-{n:D3}",
        Name = $"TestAgent_{n}",
        TenantId = "tenant-test",
        Capability = cap,
        Status = AgentStatus.Idle
    };

    private AutoAgent MakeAgent(int n = 1) => new(MakeAgentModel(n), _nullLogger, _mockEventBus.Object);

    // ── Construction ─────────────────────────────────────────────────────
    [Fact] public void Agent_HasCorrectId()        => MakeAgent(1).Id.Should().Be("agent-001");
    [Fact] public void Agent_HasCorrectName()      => MakeAgent(2).Name.Should().Be("TestAgent_2");
    [Fact] public void Agent_InitiallyIdle()       => MakeAgent().Status.Should().Be(AgentStatus.Idle);
    [Fact] public void Agent_HasTenantId()         => MakeAgent().AgentModel.TenantId.Should().Be("tenant-test");
    [Fact] public void Agent_HasCapability()       => MakeAgent().AgentModel.Capability.Should().Be(AgentCapability.Analytics);

    // ── Start / Stop lifecycle ────────────────────────────────────────────
    [Fact] public async Task Agent_StartChangesStatusToRunning()
    {
        var agent = MakeAgent();
        await agent.StartAsync();
        agent.Status.Should().Be(AgentStatus.Running);
    }

    [Fact] public async Task Agent_StopChangesStatusToIdle()
    {
        var agent = MakeAgent();
        await agent.StartAsync();
        await agent.StopAsync();
        agent.Status.Should().Be(AgentStatus.Idle);
    }

    [Fact] public async Task Agent_StartSetsLastActiveAt()
    {
        var agent = MakeAgent();
        await agent.StartAsync();
        agent.AgentModel.LastActiveAt.Should().NotBeNull();
    }

    // ── Task execution ────────────────────────────────────────────────────
    [Fact] public async Task Agent_ExecuteTask_ReturnsSuccess()
    {
        var agent = MakeAgent();
        var task = new AgentTask { Type = "test", Parameters = new() { ["data"] = "hello" } };
        var result = await agent.ExecuteTaskAsync(task);
        result.Success.Should().BeTrue();
    }

    [Fact] public async Task Agent_ExecuteTask_HasTaskId()
    {
        var agent = MakeAgent();
        var task = new AgentTask { Id = "task-abc", Type = "compute" };
        var result = await agent.ExecuteTaskAsync(task);
        result.TaskId.Should().Be("task-abc");
    }

    [Fact] public async Task Agent_ExecuteTask_IncrementsMessageCount()
    {
        var agent = MakeAgent();
        await agent.ExecuteTaskAsync(new AgentTask { Type = "a" });
        await agent.ExecuteTaskAsync(new AgentTask { Type = "b" });
        agent.AgentModel.MessageCount.Should().Be(2);
    }

    [Fact] public async Task Agent_ExecuteTask_OutputContainsCapability()
    {
        var agent = MakeAgent();
        var result = await agent.ExecuteTaskAsync(new AgentTask { Type = "capability-check" });
        result.Output.Should().ContainKey("capability");
    }

    // ── Event emission ────────────────────────────────────────────────────
    [Fact] public async Task Agent_Start_PublishesEvent()
    {
        var agent = MakeAgent();
        await agent.StartAsync();
        _mockEventBus.Verify(e => e.PublishAsync("nexus.agents", It.IsAny<AgentEvent>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact] public async Task Agent_ExecuteTask_PublishesTwoEvents()
    {
        var agent = MakeAgent();
        await agent.ExecuteTaskAsync(new AgentTask { Type = "x" });
        _mockEventBus.Verify(e => e.PublishAsync("nexus.agents", It.IsAny<AgentEvent>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    // ── All 16 capabilities have agents ──────────────────────────────────
    [Theory]
    [InlineData(AgentCapability.Analytics)]
    [InlineData(AgentCapability.Security)]
    [InlineData(AgentCapability.DataIngestion)]
    [InlineData(AgentCapability.Intelligence)]
    [InlineData(AgentCapability.Prediction)]
    [InlineData(AgentCapability.Compliance)]
    [InlineData(AgentCapability.Monitoring)]
    [InlineData(AgentCapability.Orchestration)]
    [InlineData(AgentCapability.Knowledge)]
    [InlineData(AgentCapability.Communication)]
    [InlineData(AgentCapability.Optimization)]
    [InlineData(AgentCapability.Resilience)]
    [InlineData(AgentCapability.Discovery)]
    [InlineData(AgentCapability.Transformation)]
    [InlineData(AgentCapability.Validation)]
    [InlineData(AgentCapability.Evolution)]
    public async Task Agent_CanExecuteTask_ForCapability(AgentCapability capability)
    {
        var model = MakeAgentModel(1, capability);
        var agent = new AutoAgent(model, _nullLogger, _mockEventBus.Object);
        var result = await agent.ExecuteTaskAsync(new AgentTask { Type = capability.ToString() });
        result.Success.Should().BeTrue();
    }

    // ── Metadata ──────────────────────────────────────────────────────────
    [Fact] public void Agent_CanHaveMetadata()
    {
        var model = MakeAgentModel();
        model.Metadata["key"] = "value";
        model.Metadata["key"].Should().Be("value");
    }

    [Fact] public void Agent_CanHaveTags()
    {
        var model = MakeAgentModel();
        model.Tags.Add("prod");
        model.Tags.Should().Contain("prod");
    }

    [Fact] public void Agent_DefaultConfidenceIsOne() => MakeAgentModel().ConfidenceScore.Should().Be(1.0);

    [Fact] public void Agent_MessageCount_StartsAtZero() => MakeAgent().AgentModel.MessageCount.Should().Be(0);

    [Fact] public async Task Agent_ExecuteMultipleTasks_CountMatches()
    {
        var agent = MakeAgent();
        for (int i = 0; i < 10; i++)
            await agent.ExecuteTaskAsync(new AgentTask { Type = $"task-{i}" });
        agent.AgentModel.MessageCount.Should().Be(10);
    }
}

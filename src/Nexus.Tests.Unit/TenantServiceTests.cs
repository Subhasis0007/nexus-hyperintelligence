using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Core.Services;
using Nexus.Models;
using Xunit;

namespace Nexus.Tests.Unit;

public class TenantServiceTests
{
    private static TenantService MakeService() => new(NullLogger<TenantService>.Instance);

    // ── Default tenant ────────────────────────────────────────────────────
    [Fact] public async Task DefaultTenant_Exists()
    {
        var svc = MakeService();
        var t = await svc.GetTenantAsync("tenant-default");
        t.Should().NotBeNull();
    }

    [Fact] public async Task DefaultTenant_IsActive()
    {
        var svc = MakeService();
        var t = await svc.GetTenantAsync("tenant-default");
        t!.Status.Should().Be(TenantStatus.Active);
    }

    [Fact] public async Task DefaultTenant_IsEnterprise()
    {
        var svc = MakeService();
        var t = await svc.GetTenantAsync("tenant-default");
        t!.Tier.Should().Be(TenantTier.Enterprise);
    }

    [Fact] public async Task DefaultTenant_Allows200Agents()
    {
        var svc = MakeService();
        var t = await svc.GetTenantAsync("tenant-default");
        t!.Limits.MaxAgents.Should().Be(200);
    }

    [Fact] public async Task DefaultTenant_Allows16Swarms()
    {
        var svc = MakeService();
        var t = await svc.GetTenantAsync("tenant-default");
        t!.Limits.MaxSwarms.Should().Be(16);
    }

    // ── CRUD ──────────────────────────────────────────────────────────────
    [Fact] public async Task CreateTenant_CanBeRetrieved()
    {
        var svc = MakeService();
        var req = new TenantCreateRequest { Name = "TestCo", ContactEmail = "admin@test.co" };
        var created = await svc.CreateTenantAsync(req);
        var retrieved = await svc.GetTenantAsync(created.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("TestCo");
    }

    [Fact] public async Task CreateTenant_IsActiveByDefault()
    {
        var svc = MakeService();
        var t = await svc.CreateTenantAsync(new TenantCreateRequest { Name = "X", ContactEmail = "x@x.com" });
        t.Status.Should().Be(TenantStatus.Active);
    }

    [Fact] public async Task CreateTenant_HasCreatedAt()
    {
        var svc = MakeService();
        var t = await svc.CreateTenantAsync(new TenantCreateRequest { Name = "Y", ContactEmail = "y@y.com" });
        t.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact] public async Task GetAllTenants_IncludesDefault()
    {
        var svc = MakeService();
        var all = await svc.GetAllTenantsAsync();
        all.Should().Contain(t => t.Id == "tenant-default");
    }

    [Fact] public async Task GetAllTenants_AfterCreate_HasNewTenant()
    {
        var svc = MakeService();
        await svc.CreateTenantAsync(new TenantCreateRequest { Name = "NewCo", ContactEmail = "n@n.com" });
        var all = await svc.GetAllTenantsAsync();
        all.Should().Contain(t => t.Name == "NewCo");
    }

    // ── Validation ────────────────────────────────────────────────────────
    [Fact] public async Task ValidateTenant_DefaultTenant_ReturnsTrue()
    {
        var svc = MakeService();
        var valid = await svc.ValidateTenantAsync("tenant-default");
        valid.Should().BeTrue();
    }

    [Fact] public async Task ValidateTenant_Unknown_ReturnsFalse()
    {
        var svc = MakeService();
        var valid = await svc.ValidateTenantAsync("does-not-exist");
        valid.Should().BeFalse();
    }

    [Fact] public async Task ValidateTenant_NewlyCreated_ReturnsTrue()
    {
        var svc = MakeService();
        var t = await svc.CreateTenantAsync(new TenantCreateRequest { Name = "V", ContactEmail = "v@v.com" });
        (await svc.ValidateTenantAsync(t.Id)).Should().BeTrue();
    }

    // ── Usage tracking ────────────────────────────────────────────────────
    [Fact] public async Task IncrementAgentCount_IncreasesUsage()
    {
        var svc = MakeService();
        await svc.IncrementAgentCountAsync("tenant-default");
        var t = await svc.GetTenantAsync("tenant-default");
        t!.Usage.ActiveAgents.Should().Be(1);
    }

    [Fact] public async Task IncrementSwarmCount_IncreasesUsage()
    {
        var svc = MakeService();
        await svc.IncrementSwarmCountAsync("tenant-default");
        var t = await svc.GetTenantAsync("tenant-default");
        t!.Usage.ActiveSwarms.Should().Be(1);
    }

    [Fact] public async Task IncrementApiCalls_Accumulates()
    {
        var svc = MakeService();
        for (int i = 0; i < 5; i++)
            await svc.IncrementApiCallsAsync("tenant-default");
        var t = await svc.GetTenantAsync("tenant-default");
        t!.Usage.TotalApiCalls.Should().Be(5);
    }

    // ── GetTenant returns null for unknown ────────────────────────────────
    [Fact] public async Task GetTenant_Unknown_ReturnsNull()
    {
        var svc = MakeService();
        (await svc.GetTenantAsync("no-such-tenant")).Should().BeNull();
    }
}

public class KnowledgeGraphServiceTests
{
    private static KnowledgeGraphService MakeService() => new(NullLogger<KnowledgeGraphService>.Instance);

    [Fact] public async Task AddNode_CanBeQueried()
    {
        var svc = MakeService();
        var node = new KnowledgeNode { Id = "n1", Label = "Agent", Properties = new() { ["name"] = "test" } };
        await svc.AddNodeAsync(node);
        var results = await svc.QueryAsync(new KnowledgeQuery { Query = "Agent", TenantId = "t1" });
        results.Nodes.Should().Contain(n => n.Id == "n1");
    }

    [Fact] public async Task AddEdge_BetweenNodes()
    {
        var svc = MakeService();
        await svc.AddNodeAsync(new KnowledgeNode { Id = "a", Label = "A" });
        await svc.AddNodeAsync(new KnowledgeNode { Id = "b", Label = "B" });
        var edge = new KnowledgeEdge { FromNodeId = "a", ToNodeId = "b", RelationType = "CONNECTS" };
        await svc.AddEdgeAsync(edge);
        var results = await svc.QueryAsync(new KnowledgeQuery { Query = "A" });
        results.Edges.Should().Contain(e => e.FromNodeId == "a");
    }

    [Fact] public async Task SemanticSearch_ReturnsNodes()
    {
        var svc = MakeService();
        for (int i = 0; i < 5; i++)
            await svc.AddNodeAsync(new KnowledgeNode { Id = $"n{i}", Label = "Test", EmbeddingVector = Enumerable.Range(0, 8).Select(j => (float)(i + j)).ToArray() });
        var results = await svc.SemanticSearchAsync(Enumerable.Range(0, 8).Select(j => (float)j).ToArray(), topK: 3);
        results.Should().HaveCount(3);
    }

    [Fact] public async Task GetNodeCount_Returns0Initially()
    {
        var svc = MakeService();
        (await svc.GetNodeCountAsync()).Should().Be(0);
    }

    [Fact] public async Task GetNodeCount_AfterAdding3()
    {
        var svc = MakeService();
        for (int i = 0; i < 3; i++)
            await svc.AddNodeAsync(new KnowledgeNode { Id = $"node{i}", Label = "X" });
        (await svc.GetNodeCountAsync()).Should().Be(3);
    }
}

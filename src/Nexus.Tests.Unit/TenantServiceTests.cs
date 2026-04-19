using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Core.Services;
using Nexus.Models;
using Xunit;

namespace Nexus.Tests.Unit;

public class TenantServiceTests
{
    private static TenantService MakeService() => new(NullLogger<TenantService>.Instance);

    [Fact]
    public async Task DefaultTenant_Exists_AndIsActive()
    {
        var svc = MakeService();
        var t = await svc.GetTenantAsync("tenant-default");
        t.Should().NotBeNull();
        t!.Status.Should().Be(TenantStatus.Active);
        t.Tier.Should().Be(TenantTier.Enterprise);
        t.Limits.MaxAgents.Should().Be(200);
        t.Limits.MaxSwarms.Should().Be(16);
    }

    [Fact]
    public async Task CreateTenant_CanBeRetrieved_AndValidated()
    {
        var svc = MakeService();
        var req = new TenantCreateRequest { Name = "TestCo", AdminEmail = "admin@test.co" };
        var created = await svc.CreateTenantAsync(req);

        created.Name.Should().Be("TestCo");
        created.AdminEmail.Should().Be("admin@test.co");

        var retrieved = await svc.GetTenantAsync(created.Id);
        retrieved.Should().NotBeNull();

        var valid = await svc.ValidateTenantAsync(created.Id);
        valid.Should().BeTrue();
    }

    [Fact]
    public async Task ListTenants_IncludesDefault_AndCreated()
    {
        var svc = MakeService();
        await svc.CreateTenantAsync(new TenantCreateRequest { Name = "NewCo", AdminEmail = "n@n.com" });

        var all = await svc.ListTenantsAsync();
        all.Should().Contain(t => t.Id == "tenant-default");
        all.Should().Contain(t => t.Name == "NewCo");
    }

    [Fact]
    public async Task UpdateTenant_ChangesState()
    {
        var svc = MakeService();
        var created = await svc.CreateTenantAsync(new TenantCreateRequest { Name = "Updatable", AdminEmail = "u@u.com" });

        await svc.UpdateTenantAsync(created.Id, t =>
        {
            t.Status = TenantStatus.Suspended;
            t.Name = "Updated";
        });

        var updated = await svc.GetTenantAsync(created.Id);
        updated!.Status.Should().Be(TenantStatus.Suspended);
        updated.Name.Should().Be("Updated");
    }

    [Fact]
    public async Task DeleteTenant_RemovesTenant()
    {
        var svc = MakeService();
        var created = await svc.CreateTenantAsync(new TenantCreateRequest { Name = "DeleteMe", AdminEmail = "d@d.com" });

        var deleted = await svc.DeleteTenantAsync(created.Id);
        deleted.Should().BeTrue();

        var found = await svc.GetTenantAsync(created.Id);
        found.Should().BeNull();
    }
}

public class KnowledgeGraphServiceTests
{
    private static KnowledgeGraphService MakeService() => new(NullLogger<KnowledgeGraphService>.Instance);

    [Fact]
    public async Task UpsertNode_ThenQuery_FindsNode()
    {
        var svc = MakeService();
        var node = new KnowledgeNode
        {
            Id = "n1",
            TenantId = "tenant-a",
            Label = "Agent",
            Type = "Agent"
        };

        await svc.UpsertNodeAsync(node);
        var result = await svc.QueryAsync(new KnowledgeQuery { TenantId = "tenant-a", NodeTypes = new List<string> { "Agent" }, TopK = 10 });

        result.Nodes.Should().Contain(n => n.Id == "n1");
    }

    [Fact]
    public async Task UpsertEdge_ThenQuery_ReturnsEdge()
    {
        var svc = MakeService();
        await svc.UpsertNodeAsync(new KnowledgeNode { Id = "a", TenantId = "tenant-a", Label = "A", Type = "Node" });
        await svc.UpsertNodeAsync(new KnowledgeNode { Id = "b", TenantId = "tenant-a", Label = "B", Type = "Node" });

        await svc.UpsertEdgeAsync(new KnowledgeEdge
        {
            Id = "e1",
            SourceNodeId = "a",
            TargetNodeId = "b",
            RelationType = "CONNECTS"
        });

        var result = await svc.QueryAsync(new KnowledgeQuery { TenantId = "tenant-a", TopK = 10 });
        result.Edges.Should().Contain(e => e.Id == "e1");
    }

    [Fact]
    public async Task GetNode_RespectsTenantIsolation()
    {
        var svc = MakeService();
        await svc.UpsertNodeAsync(new KnowledgeNode { Id = "x", TenantId = "tenant-a", Label = "Node", Type = "Node" });

        var visible = await svc.GetNodeAsync("x", "tenant-a");
        var hidden = await svc.GetNodeAsync("x", "tenant-b");

        visible.Should().NotBeNull();
        hidden.Should().BeNull();
    }

    [Fact]
    public async Task DeleteNode_RemovesNode()
    {
        var svc = MakeService();
        await svc.UpsertNodeAsync(new KnowledgeNode { Id = "del", TenantId = "tenant-a", Label = "Node", Type = "Node" });

        var deleted = await svc.DeleteNodeAsync("del", "tenant-a");
        var found = await svc.GetNodeAsync("del", "tenant-a");

        deleted.Should().BeTrue();
        found.Should().BeNull();
    }
}

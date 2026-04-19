using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Nexus.Models;
using Xunit;

namespace Nexus.Tests.Integration;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Tenant-ID", "tenant-default");
    }

    // ── Health ────────────────────────────────────────────────────────────
    [Fact] public async Task Health_Endpoint_Returns200()
    {
        var resp = await _client.GetAsync("/health");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Agents ────────────────────────────────────────────────────────────
    [Fact] public async Task GetAgents_Returns200()
    {
        var resp = await _client.GetAsync("/api/agents");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact] public async Task GetAgents_ResponseIsPagedResult()
    {
        var resp = await _client.GetAsync("/api/agents");
        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<List<Agent>>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
    }

    [Fact] public async Task GetAgents_Has200Items()
    {
        var resp = await _client.GetAsync("/api/agents?page=1&pageSize=200");
        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<List<Agent>>>();
        body!.Data!.Count.Should().Be(200);
    }

    [Fact] public async Task GetAgent_Unknown_Returns404()
    {
        var resp = await _client.GetAsync("/api/agents/no-such-agent");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Swarms ────────────────────────────────────────────────────────────
    [Fact] public async Task GetSwarms_Returns200()
    {
        var resp = await _client.GetAsync("/api/swarms");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact] public async Task GetSwarms_Has16Items()
    {
        var resp = await _client.GetAsync("/api/swarms");
        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<List<Swarm>>>();
        body!.Data!.Count.Should().Be(16);
    }

    // ── Tenants ───────────────────────────────────────────────────────────
    [Fact] public async Task GetTenants_Returns200()
    {
        var resp = await _client.GetAsync("/api/tenants");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact] public async Task GetDefaultTenant_Returns200()
    {
        var resp = await _client.GetAsync("/api/tenants/tenant-default");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact] public async Task GetDefaultTenant_IsEnterprise()
    {
        var resp = await _client.GetAsync("/api/tenants/tenant-default");
        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<Tenant>>();
        body!.Data!.Tier.Should().Be(TenantTier.Enterprise);
    }

    [Fact] public async Task CreateTenant_Returns201()
    {
        var req = new TenantCreateRequest { Name = "IntTestCo", AdminEmail = "int@test.co" };
        var resp = await _client.PostAsJsonAsync("/api/tenants", req);
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ── Connectors ────────────────────────────────────────────────────────
    [Fact] public async Task GetConnectors_Returns200()
    {
        var resp = await _client.GetAsync("/api/connectors");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Crypto endpoints ──────────────────────────────────────────────────
    [Fact] public async Task Kyber_GenerateKeypair_Returns200()
    {
        var resp = await _client.PostAsync("/api/crypto/kyber/keypair", null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact] public async Task Dilithium_GenerateKeypair_Returns200()
    {
        var resp = await _client.PostAsync("/api/crypto/dilithium/keypair", null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact] public async Task Shamir_Split_Returns200()
    {
        var req = new
        {
            secretBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 }),
            threshold = 3,
            totalShares = 5
        };
        var resp = await _client.PostAsJsonAsync("/api/crypto/shamir/split", req);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Metrics endpoint ──────────────────────────────────────────────────
    [Fact] public async Task Metrics_Returns200()
    {
        var resp = await _client.GetAsync("/metrics");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Missing tenant header uses default ────────────────────────────────
    [Fact] public async Task NoTenantHeader_UsesDefault_Returns200()
    {
        using var client = new WebApplicationFactory<Program>().CreateClient();
        var resp = await client.GetAsync("/health");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Invalid tenant returns 401 ────────────────────────────────────────
    [Fact] public async Task InvalidTenant_Returns401()
    {
        using var client = new WebApplicationFactory<Program>().CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-ID", "invalid-tenant-xyz");
        var resp = await client.GetAsync("/api/agents");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

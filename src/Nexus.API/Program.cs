using Nexus.Agents;
using Nexus.API.GraphQL;
using Nexus.API.Hubs;
using Nexus.API.Middleware;
using Nexus.Core.AI;
using Nexus.Core.Interfaces;
using Nexus.Core.Services;
using Nexus.Crypto.Services;
using Nexus.Models;

var builder = WebApplication.CreateBuilder(args);

// ── Core Services ─────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Nexus HyperIntelligence API", Version = "v1" });
    c.AddSecurityDefinition("TenantId", new()
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Name = "X-Tenant-ID",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey
    });
});

// ── HTTP client factory (required by AIProviderFactory) ──────────────────
builder.Services.AddHttpClient();

// ── AI Provider (hybrid: offline / online / auto via NEXUS_AI_MODE) ───────
builder.Services.AddSingleton<AIProviderFactory>();
builder.Services.AddSingleton<IAIProviderFactory>(sp => sp.GetRequiredService<AIProviderFactory>());
builder.Services.AddSingleton<AIService>();

// ── Domain Services ───────────────────────────────────────────────────────
builder.Services.AddSingleton<ISemanticKernelService, SemanticKernelService>();
builder.Services.AddSingleton<IConnectorRegistry, ConnectorRegistry>();
builder.Services.AddSingleton<ITenantService, TenantService>();
builder.Services.AddSingleton<IKnowledgeGraphService, KnowledgeGraphService>();
builder.Services.AddSingleton<IEventBusService, EventBusService>();
builder.Services.AddSingleton<ICryptoService, CryptoService>();
builder.Services.AddSingleton<AgentFactory>();
builder.Services.AddSingleton<SwarmOrchestrator>();

// ── SignalR ───────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── GraphQL (HotChocolate) ────────────────────────────────────────────────
builder.Services
    .AddGraphQLServer()
    .AddQueryType<NexusQuery>()
    .AddMutationType<NexusMutation>()
    .AddSubscriptionType<NexusSubscription>()
    .AddInMemorySubscriptions();

// ── Health Checks ─────────────────────────────────────────────────────────
builder.Services.AddHealthChecks();

// ── OpenTelemetry & Metrics ───────────────────────────────────────────────
// TODO: Configure OpenTelemetry tracing + Prometheus metrics  in production
// For now, focus on core API functionality

// ── CORS ──────────────────────────────────────────────────────────────────
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// ── Middleware Pipeline ───────────────────────────────────────────────────
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Nexus API v1"));

app.UseRouting();
app.UseMiddleware<TenantMiddleware>();

app.MapControllers();
app.MapHub<AgentHub>("/hubs/agents");
app.MapHub<AgentHub>("/ws/telemetry");
app.MapGraphQL("/graphql");
app.MapHealthChecks("/health");
// Metrics endpoint (requires Prometheus setup)
app.Map("/metrics", () => Results.Ok("Metrics endpoint"));

// ── Bootstrap orchestrator and connectors with default tenant ─────────────
var orchestrator = app.Services.GetRequiredService<SwarmOrchestrator>();
orchestrator.InitializeForTenant("tenant-default");

var connectorRegistry = app.Services.GetRequiredService<IConnectorRegistry>();
await SeedConnectorsAsync(connectorRegistry, "tenant-default");

app.Logger.LogInformation("Nexus HyperIntelligence API starting on {Urls}", app.Configuration["ASPNETCORE_URLS"] ?? "http://+:5000");

app.Run();

static async Task SeedConnectorsAsync(IConnectorRegistry registry, string tenantId)
{
    var existing = await registry.GetAllAsync(tenantId);
    if (existing.Count >= 42)
    {
        return;
    }

    var connectorTypes = Enum.GetValues<ConnectorType>();
    for (var i = 1; i <= 42; i++)
    {
        var id = $"connector-{i:D3}";
        if (await registry.GetAsync(id) != null)
        {
            continue;
        }

        var connector = new Connector
        {
            Id = id,
            Name = $"Connector {i:D3}",
            TenantId = tenantId,
            Type = connectorTypes[(i - 1) % connectorTypes.Length],
            AuthType = ConnectorAuthType.ApiKey,
            Status = ConnectorStatus.Active,
            BaseUrl = $"https://connector-{i:D3}.internal.nexus",
            Config = new Dictionary<string, string>
            {
                ["region"] = "global",
                ["mode"] = "readonly"
            },
            Health = new ConnectorHealthInfo
            {
                IsHealthy = true,
                ResponseTimeMs = 25 + (i % 10),
                SuccessfulCallsLast24h = 1000 + i,
                FailedCallsLast24h = 0,
                LastChecked = DateTimeOffset.UtcNow
            }
        };

        await registry.RegisterAsync(connector);
    }
}

public partial class Program { } // for WebApplicationFactory in integration tests

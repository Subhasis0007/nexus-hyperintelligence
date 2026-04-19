using Nexus.Agents;
using Nexus.API.GraphQL;
using Nexus.API.Hubs;
using Nexus.API.Middleware;
using Nexus.Core.Interfaces;
using Nexus.Core.Services;
using Nexus.Crypto.Services;

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
app.MapGraphQL("/graphql");
app.MapHealthChecks("/health");
// Metrics endpoint (requires Prometheus setup)
app.Map("/metrics", () => Results.Ok("Metrics endpoint"));

// ── Bootstrap orchestrator with default tenant ────────────────────────────
var orchestrator = app.Services.GetRequiredService<SwarmOrchestrator>();
orchestrator.InitializeForTenant("tenant-default");

app.Logger.LogInformation("Nexus HyperIntelligence API starting on {Urls}", app.Configuration["ASPNETCORE_URLS"] ?? "http://+:5000");

app.Run();

public partial class Program { } // for WebApplicationFactory in integration tests

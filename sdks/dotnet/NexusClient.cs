using System.Net.Http.Json;
using System.Text.Json;

namespace Nexus.SDK;

public class NexusError : Exception
{
    public int StatusCode { get; }
    public NexusError(int status, string message) : base($"HTTP {status}: {message}") => StatusCode = status;
}

public class NexusAuthError : NexusError
{
    public NexusAuthError(string message) : base(401, message) { }
}

public class NexusClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _tenantId;
    private bool _disposed;

    public NexusClient(string baseUrl = "http://localhost:5000", string tenantId = "tenant-default")
    {
        _tenantId = tenantId;
        _http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + '/') };
        _http.DefaultRequestHeaders.Add("X-Tenant-ID", tenantId);
        _http.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public NexusClient(HttpClient httpClient, string tenantId = "tenant-default")
    {
        _http = httpClient;
        _tenantId = tenantId;
        _http.DefaultRequestHeaders.TryAddWithoutValidation("X-Tenant-ID", tenantId);
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync(path, ct);
        await EnsureSuccess(resp, ct);
        return await resp.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
    }

    private async Task<T?> PostAsync<T>(string path, object? body = null, CancellationToken ct = default)
    {
        HttpResponseMessage resp;
        if (body is null)
            resp = await _http.PostAsync(path, null, ct);
        else
            resp = await _http.PostAsJsonAsync(path, body, ct);
        await EnsureSuccess(resp, ct);
        return await resp.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
    }

    private static async Task EnsureSuccess(HttpResponseMessage resp, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode) return;
        var body = await resp.Content.ReadAsStringAsync(ct);
        if ((int)resp.StatusCode == 401) throw new NexusAuthError(body);
        throw new NexusError((int)resp.StatusCode, body);
    }

    // ── Agents ──────────────────────────────────────────────────────────────
    public Task<JsonElement?> ListAgentsAsync(int page = 1, int pageSize = 50, CancellationToken ct = default)
        => GetAsync<JsonElement>($"api/agents?page={page}&pageSize={pageSize}", ct);

    public Task<JsonElement?> GetAgentAsync(string agentId, CancellationToken ct = default)
        => GetAsync<JsonElement>($"api/agents/{Uri.EscapeDataString(agentId)}", ct);

    public Task<JsonElement?> CreateAgentAsync(string name, string capability, string? swarmId = null, CancellationToken ct = default)
        => PostAsync<JsonElement>("api/agents", new { name, capability, tenantId = _tenantId, swarmId }, ct);

    public Task<JsonElement?> ExecuteAgentTaskAsync(string agentId, string taskType, object? parameters = null, CancellationToken ct = default)
        => PostAsync<JsonElement>($"api/agents/{Uri.EscapeDataString(agentId)}/execute", new { taskType, parameters }, ct);

    public Task<JsonElement?> GetAgentStatsAsync(CancellationToken ct = default)
        => GetAsync<JsonElement>("api/agents/stats", ct);

    // ── Swarms ───────────────────────────────────────────────────────────────
    public Task<JsonElement?> ListSwarmsAsync(CancellationToken ct = default)
        => GetAsync<JsonElement>("api/swarms", ct);

    public Task<JsonElement?> GetSwarmAsync(string swarmId, CancellationToken ct = default)
        => GetAsync<JsonElement>($"api/swarms/{Uri.EscapeDataString(swarmId)}", ct);

    public Task<JsonElement?> RunConsensusAsync(string swarmId, string proposalId, string proposal, CancellationToken ct = default)
        => PostAsync<JsonElement>($"api/swarms/{Uri.EscapeDataString(swarmId)}/consensus", new { proposalId, proposal }, ct);

    // ── Crypto ───────────────────────────────────────────────────────────────
    public Task<JsonElement?> KyberKeypairAsync(string level = "Kyber768", CancellationToken ct = default)
        => PostAsync<JsonElement>($"api/crypto/kyber/keypair?level={level}", null, ct);

    public Task<JsonElement?> DilithiumKeypairAsync(string level = "Dilithium3", CancellationToken ct = default)
        => PostAsync<JsonElement>($"api/crypto/dilithium/keypair?level={level}", null, ct);

    public Task<JsonElement?> ShamirSplitAsync(string secretB64, int threshold, int total, CancellationToken ct = default)
        => PostAsync<JsonElement>("api/crypto/shamir/split", new { secret = secretB64, threshold, total }, ct);

    // ── Health ───────────────────────────────────────────────────────────────
    public Task<JsonElement?> HealthAsync(CancellationToken ct = default)
        => GetAsync<JsonElement>("health", ct);

    public void Dispose()
    {
        if (!_disposed) { _http.Dispose(); _disposed = true; }
    }
}

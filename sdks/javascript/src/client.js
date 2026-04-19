/**
 * Nexus HyperIntelligence JavaScript SDK
 * Requires Node.js >= 18 (uses native fetch)
 */

export class NexusError extends Error {
  constructor(status, message) {
    super(`HTTP ${status}: ${message}`);
    this.status = status;
  }
}

export class NexusAuthError extends NexusError {}

export class NexusClient {
  #baseUrl;
  #tenantId;
  #timeout;

  constructor({ baseUrl = 'http://localhost:5000', tenantId = 'tenant-default', timeoutMs = 30_000 } = {}) {
    this.#baseUrl = baseUrl.replace(/\/$/, '');
    this.#tenantId = tenantId;
    this.#timeout = timeoutMs;
  }

  #headers() {
    return {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
      'X-Tenant-ID': this.#tenantId,
    };
  }

  async #request(method, path, body) {
    const url = `${this.#baseUrl}${path}`;
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), this.#timeout);
    try {
      const resp = await fetch(url, {
        method,
        headers: this.#headers(),
        body: body != null ? JSON.stringify(body) : undefined,
        signal: controller.signal,
      });
      const text = await resp.text();
      const data = text ? JSON.parse(text) : null;
      if (!resp.ok) {
        if (resp.status === 401) throw new NexusAuthError(resp.status, text);
        throw new NexusError(resp.status, text);
      }
      return data;
    } finally {
      clearTimeout(timer);
    }
  }

  // ── Agents ──────────────────────────────────────────────────────────────
  listAgents(page = 1, pageSize = 50) {
    return this.#request('GET', `/api/agents?page=${page}&pageSize=${pageSize}`);
  }
  getAgent(agentId) {
    return this.#request('GET', `/api/agents/${encodeURIComponent(agentId)}`);
  }
  createAgent({ name, capability, swarmId, tags, metadata } = {}) {
    return this.#request('POST', '/api/agents', { name, capability, tenantId: this.#tenantId, swarmId, tags, metadata });
  }
  executeAgentTask(agentId, taskType, parameters = {}) {
    return this.#request('POST', `/api/agents/${encodeURIComponent(agentId)}/execute`, { taskType, parameters });
  }
  agentStats() {
    return this.#request('GET', '/api/agents/stats');
  }

  // ── Swarms ───────────────────────────────────────────────────────────────
  listSwarms() { return this.#request('GET', '/api/swarms'); }
  getSwarm(swarmId) { return this.#request('GET', `/api/swarms/${encodeURIComponent(swarmId)}`); }
  executeSwarmTask(swarmId, taskType, parameters = {}) {
    return this.#request('POST', `/api/swarms/${encodeURIComponent(swarmId)}/execute`, { taskType, parameters });
  }
  runConsensus(swarmId, proposalId, proposal) {
    return this.#request('POST', `/api/swarms/${encodeURIComponent(swarmId)}/consensus`, { proposalId, proposal });
  }

  // ── Tenants ──────────────────────────────────────────────────────────────
  listTenants() { return this.#request('GET', '/api/tenants'); }
  getTenant(tenantId) { return this.#request('GET', `/api/tenants/${encodeURIComponent(tenantId)}`); }

  // ── Crypto ───────────────────────────────────────────────────────────────
  kyberKeypair(level = 'Kyber768') { return this.#request('POST', `/api/crypto/kyber/keypair?level=${level}`); }
  dilithiumKeypair(level = 'Dilithium3') { return this.#request('POST', `/api/crypto/dilithium/keypair?level=${level}`); }
  shamirSplit(secretB64, threshold, total) {
    return this.#request('POST', '/api/crypto/shamir/split', { secret: secretB64, threshold, total });
  }
  zkpProve(circuit, publicInputsB64, witnessB64) {
    return this.#request('POST', '/api/crypto/zkp/prove', { circuit, publicInputs: publicInputsB64, witness: witnessB64 });
  }

  // ── Health ───────────────────────────────────────────────────────────────
  health() { return this.#request('GET', '/health'); }
}

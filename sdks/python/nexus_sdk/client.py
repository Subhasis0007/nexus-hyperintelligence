"""
Nexus HyperIntelligence Python SDK Client
"""
from __future__ import annotations
import json
import urllib.error
import urllib.parse
import urllib.request
from typing import Any, Optional


class NexusError(Exception):
    """Raised for non-2xx HTTP responses."""
    def __init__(self, status: int, message: str):
        super().__init__(f"HTTP {status}: {message}")
        self.status = status


class NexusAuthError(NexusError):
    """Raised when the tenant header is missing or invalid."""


class NexusClient:
    """Synchronous Nexus API client."""

    def __init__(
        self,
        base_url: str = "http://localhost:5000",
        tenant_id: str = "tenant-default",
        timeout: float = 30.0,
    ):
        self.base_url = base_url.rstrip("/")
        self.tenant_id = tenant_id
        self.timeout = timeout

    # ── Internal helpers ────────────────────────────────────────────────────

    def _headers(self) -> dict[str, str]:
        return {
            "Content-Type": "application/json",
            "Accept": "application/json",
            "X-Tenant-ID": self.tenant_id,
        }

    def _request(self, method: str, path: str, body: Any = None) -> Any:
        url = f"{self.base_url}{path}"
        data = json.dumps(body).encode() if body is not None else None
        req = urllib.request.Request(url, data=data, headers=self._headers(), method=method)
        try:
            with urllib.request.urlopen(req, timeout=self.timeout) as resp:
                raw = resp.read()
                return json.loads(raw) if raw else None
        except urllib.error.HTTPError as exc:
            msg = exc.read().decode("utf-8", errors="replace")
            if exc.code == 401:
                raise NexusAuthError(exc.code, msg) from exc
            raise NexusError(exc.code, msg) from exc

    # ── Agents ──────────────────────────────────────────────────────────────

    def list_agents(self, page: int = 1, page_size: int = 50) -> dict:
        return self._request("GET", f"/api/agents?page={page}&pageSize={page_size}")

    def get_agent(self, agent_id: str) -> dict:
        return self._request("GET", f"/api/agents/{urllib.parse.quote(agent_id)}")

    def create_agent(self, name: str, capability: str, swarm_id: Optional[str] = None,
                     tags: Optional[list[str]] = None, metadata: Optional[dict] = None) -> dict:
        payload: dict = {"name": name, "capability": capability, "tenantId": self.tenant_id}
        if swarm_id:
            payload["swarmId"] = swarm_id
        if tags:
            payload["tags"] = tags
        if metadata:
            payload["metadata"] = metadata
        return self._request("POST", "/api/agents", payload)

    def execute_agent_task(self, agent_id: str, task_type: str, parameters: Optional[dict] = None) -> dict:
        return self._request("POST", f"/api/agents/{urllib.parse.quote(agent_id)}/execute",
                              {"taskType": task_type, "parameters": parameters or {}})

    def agent_stats(self) -> dict:
        return self._request("GET", "/api/agents/stats")

    # ── Swarms ───────────────────────────────────────────────────────────────

    def list_swarms(self) -> dict:
        return self._request("GET", "/api/swarms")

    def get_swarm(self, swarm_id: str) -> dict:
        return self._request("GET", f"/api/swarms/{urllib.parse.quote(swarm_id)}")

    def execute_swarm_task(self, swarm_id: str, task_type: str, parameters: Optional[dict] = None) -> dict:
        return self._request("POST", f"/api/swarms/{urllib.parse.quote(swarm_id)}/execute",
                              {"taskType": task_type, "parameters": parameters or {}})

    def run_consensus(self, swarm_id: str, proposal_id: str, proposal: str) -> dict:
        return self._request("POST", f"/api/swarms/{urllib.parse.quote(swarm_id)}/consensus",
                              {"proposalId": proposal_id, "proposal": proposal})

    # ── Tenants ──────────────────────────────────────────────────────────────

    def list_tenants(self) -> dict:
        return self._request("GET", "/api/tenants")

    def get_tenant(self, tenant_id: str) -> dict:
        return self._request("GET", f"/api/tenants/{urllib.parse.quote(tenant_id)}")

    # ── Crypto ───────────────────────────────────────────────────────────────

    def kyber_keypair(self, level: str = "Kyber768") -> dict:
        return self._request("POST", f"/api/crypto/kyber/keypair?level={level}")

    def dilithium_keypair(self, level: str = "Dilithium3") -> dict:
        return self._request("POST", f"/api/crypto/dilithium/keypair?level={level}")

    def shamir_split(self, secret_b64: str, threshold: int, total: int) -> dict:
        return self._request("POST", "/api/crypto/shamir/split",
                              {"secret": secret_b64, "threshold": threshold, "total": total})

    def zkp_prove(self, circuit: str, public_inputs_b64: str, witness_b64: str) -> dict:
        return self._request("POST", "/api/crypto/zkp/prove",
                              {"circuit": circuit, "publicInputs": public_inputs_b64, "witness": witness_b64})

    # ── Health ───────────────────────────────────────────────────────────────

    def health(self) -> dict:
        return self._request("GET", "/health")

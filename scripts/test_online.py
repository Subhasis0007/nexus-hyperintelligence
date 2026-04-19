#!/usr/bin/env python3
import asyncio
import json
import os
from dataclasses import dataclass
from typing import Any
from urllib.parse import urlparse

import pytest
import requests

BASE_URL = os.getenv("BASE_URL", "").rstrip("/")
TENANT_ID = os.getenv("TENANT_ID", "tenant-default")
TIMEOUT = float(os.getenv("HTTP_TIMEOUT", "20"))

if not BASE_URL:
    raise RuntimeError("BASE_URL environment variable is required, e.g. https://nexus-api.onrender.com")


@dataclass
class ResultRow:
    test_name: str
    status: str
    detail: str


RESULTS: list[ResultRow] = []


def _record(name: str, status: str, detail: str) -> None:
    RESULTS.append(ResultRow(name, status, detail))


def _headers() -> dict[str, str]:
    return {
        "X-Tenant-ID": TENANT_ID,
        "Content-Type": "application/json",
    }


def _url(path: str) -> str:
    return f"{BASE_URL}{path}"


def _parse_api_data(payload: Any) -> Any:
    if isinstance(payload, dict) and "data" in payload:
        return payload["data"]
    return payload


def _try_get(paths: list[str]) -> tuple[str, requests.Response]:
    last: requests.Response | None = None
    for path in paths:
        resp = requests.get(_url(path), headers=_headers(), timeout=TIMEOUT)
        if resp.status_code == 200:
            return path, resp
        last = resp
    assert last is not None
    return paths[-1], last


def _try_post(paths: list[str], body: Any) -> tuple[str, requests.Response]:
    last: requests.Response | None = None
    for path in paths:
        resp = requests.post(_url(path), headers=_headers(), timeout=TIMEOUT, json=body)
        if resp.status_code == 200:
            return path, resp
        last = resp
    assert last is not None
    return paths[-1], last


@pytest.hookimpl(hookwrapper=True)
def pytest_runtest_makereport(item, call):
    outcome = yield
    report = outcome.get_result()
    if report.when == "call":
        if report.passed:
            _record(item.name, "PASS", "")
        elif report.failed:
            _record(item.name, "FAIL", str(report.longrepr).splitlines()[-1][:240])


@pytest.hookimpl
def pytest_sessionfinish(session, exitstatus):
    print("\nOnline Test Summary")
    print("=" * 100)
    print(f"{'Test':40} | {'Status':6} | Detail")
    print("-" * 100)
    for row in RESULTS:
        print(f"{row.test_name:40} | {row.status:6} | {row.detail}")
    print("=" * 100)


# Required checks

def test_health() -> None:
    path, resp = _try_get(["/health"])
    assert resp.status_code == 200, f"{path} returned {resp.status_code}"


def test_swagger_json() -> None:
    path, resp = _try_get(["/swagger/v1/swagger.json"])
    assert resp.status_code == 200, f"{path} returned {resp.status_code}"


def test_agents_count_200() -> None:
    path, resp = _try_get(["/api/v1/agents/count", "/api/agents", "/api/agents/stats"])
    assert resp.status_code == 200, f"{path} returned {resp.status_code}"

    payload = resp.json()
    data = _parse_api_data(payload)

    if path.endswith("/count"):
        count = data.get("count") if isinstance(data, dict) else None
    elif path.endswith("/stats"):
        count = data.get("Total") if isinstance(data, dict) else None
    else:
        count = len(data) if isinstance(data, list) else None

    assert count == 200, f"Expected 200 agents, got {count} via {path}"


def test_swarms_count_16() -> None:
    path, resp = _try_get(["/api/v1/swarms", "/api/swarms"])
    assert resp.status_code == 200, f"{path} returned {resp.status_code}"
    data = _parse_api_data(resp.json())
    count = len(data) if isinstance(data, list) else data.get("count") if isinstance(data, dict) else None
    assert count == 16, f"Expected 16 swarms, got {count} via {path}"


def test_connectors_count_42() -> None:
    path, resp = _try_get(["/api/v1/connectors", "/api/connectors"])
    assert resp.status_code == 200, f"{path} returned {resp.status_code}"
    data = _parse_api_data(resp.json())
    count = len(data) if isinstance(data, list) else data.get("count") if isinstance(data, dict) else None
    assert count == 42, f"Expected 42 connectors, got {count} via {path}"


def test_crypto_kyber_keypair() -> None:
    path, resp = _try_post(["/api/v1/crypto/kyber/keypair", "/api/crypto/kyber/keypair"], body={})
    assert resp.status_code == 200, f"{path} returned {resp.status_code}"


def test_crypto_dilithium_sign_or_keypair() -> None:
    path, resp = _try_post(["/api/v1/crypto/dilithium/sign", "/api/crypto/dilithium/sign", "/api/crypto/dilithium/keypair"], body={})
    assert resp.status_code == 200, f"{path} returned {resp.status_code}"


def test_crypto_zkp_prove() -> None:
    body = {
        "circuit": "nexus-range-proof",
        "publicInputsBase64": "AQID",
        "witnessBase64": "BAUG",
    }
    path, resp = _try_post(["/api/v1/crypto/zkp/prove", "/api/crypto/zkp/prove"], body=body)
    assert resp.status_code == 200, f"{path} returned {resp.status_code}"


def test_crypto_he_demo_or_shamir() -> None:
    body = {
        "secretBase64": "AQIDBA==",
        "threshold": 3,
        "totalShares": 5,
    }
    path, resp = _try_post(["/api/v1/crypto/he/demo", "/api/crypto/he/demo", "/api/crypto/shamir/split"], body=body)
    assert resp.status_code == 200, f"{path} returned {resp.status_code}"


def test_crypto_mpc_demo_or_shamir_combine() -> None:
    share = {
        "index": 1,
        "share": "AQIDBA==",
        "threshold": 1,
        "totalShares": 1,
    }
    path, resp = _try_post(["/api/v1/crypto/mpc/demo", "/api/crypto/mpc/demo", "/api/crypto/shamir/combine"], body=[share])
    assert resp.status_code == 200, f"{path} returned {resp.status_code}"


def test_graphql_introspection() -> None:
    query = {
        "query": "query IntrospectionQuery { __schema { queryType { name } mutationType { name } subscriptionType { name } } }"
    }
    path, resp = _try_post(["/graphql"], body=query)
    assert resp.status_code == 200, f"{path} returned {resp.status_code}"
    payload = resp.json()
    assert "data" in payload, "GraphQL response missing data"


async def _test_plain_ws(ws_url: str) -> str | None:
    websockets = pytest.importorskip("websockets")
    try:
        async with websockets.connect(ws_url, open_timeout=10, close_timeout=5) as ws:
            msg = await asyncio.wait_for(ws.recv(), timeout=10)
            return str(msg)
    except Exception:
        return None


async def _test_signalr_ws(base_url: str) -> str | None:
    websockets = pytest.importorskip("websockets")
    negotiate_url = _url("/hubs/agents/negotiate?negotiateVersion=1")
    negotiate = requests.post(negotiate_url, headers={"X-Tenant-ID": TENANT_ID}, timeout=TIMEOUT)
    if negotiate.status_code != 200:
        return None

    payload = negotiate.json()
    conn_id = payload.get("connectionId")
    if not conn_id:
        return None

    parsed = urlparse(base_url)
    scheme = "wss" if parsed.scheme == "https" else "ws"
    ws_url = f"{scheme}://{parsed.netloc}/hubs/agents?id={conn_id}"

    async with websockets.connect(ws_url, open_timeout=10, close_timeout=5) as ws:
        await ws.send('{"protocol":"json","version":1}\x1e')
        raw = await asyncio.wait_for(ws.recv(), timeout=10)
        text = str(raw)
        if "Connected" in text or '"type":1' in text:
            return text
        return None


@pytest.mark.asyncio
async def test_websocket_connect_and_receive() -> None:
    parsed = urlparse(BASE_URL)
    ws_scheme = "wss" if parsed.scheme == "https" else "ws"
    telemetry_url = f"{ws_scheme}://{parsed.netloc}/ws/telemetry"

    msg = await _test_plain_ws(telemetry_url)
    if msg:
        assert len(msg) > 0
        return

    signalr_msg = await _test_signalr_ws(BASE_URL)
    assert signalr_msg is not None, "Could not receive websocket payload from /ws/telemetry or /hubs/agents"


if __name__ == "__main__":
    raise SystemExit(pytest.main([__file__, "-q"]))

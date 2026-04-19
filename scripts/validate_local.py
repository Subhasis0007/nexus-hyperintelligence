#!/usr/bin/env python3
"""
validate_local.py — Validates that all Nexus local services are healthy.

Usage:
    python scripts/validate_local.py

Exit codes:
    0  — All services healthy
    1  — One or more services failed or unreachable
"""
from __future__ import annotations

import json
import socket
import sys
import time
import urllib.error
import urllib.request
from dataclasses import dataclass, field
from typing import Callable

# ── Configuration ─────────────────────────────────────────────────────────────

TIMEOUT = 5          # seconds per HTTP check
RETRY_DELAY = 2      # seconds between retries
MAX_RETRIES = 3      # per check


@dataclass
class CheckResult:
    name: str
    ok: bool
    message: str


@dataclass
class ServiceCheck:
    name: str
    check_fn: Callable[[], tuple[bool, str]]
    critical: bool = True


# ── Check helpers ─────────────────────────────────────────────────────────────

def http_get(url: str, expect_status: int = 200, headers: dict | None = None) -> tuple[bool, str]:
    req = urllib.request.Request(url, headers=headers or {})
    try:
        with urllib.request.urlopen(req, timeout=TIMEOUT) as resp:
            body = resp.read().decode("utf-8", errors="replace")
            if resp.status == expect_status:
                return True, f"HTTP {resp.status}"
            return False, f"Expected {expect_status}, got {resp.status}: {body[:120]}"
    except urllib.error.HTTPError as exc:
        return False, f"HTTP {exc.code}: {exc.reason}"
    except Exception as exc:
        return False, str(exc)


def tcp_connect(host: str, port: int) -> tuple[bool, str]:
    try:
        with socket.create_connection((host, port), timeout=TIMEOUT):
            return True, f"TCP {host}:{port} open"
    except Exception as exc:
        return False, str(exc)


def nexus_api_health() -> tuple[bool, str]:
    ok, msg = http_get("http://localhost:5000/health")
    if not ok:
        return False, msg
    try:
        with urllib.request.urlopen("http://localhost:5000/health", timeout=TIMEOUT) as r:
            data = json.loads(r.read())
            status = data.get("status", "unknown")
            return status.lower() == "healthy", f"status={status}"
    except Exception as exc:
        return False, str(exc)


def nexus_agents_200() -> tuple[bool, str]:
    req = urllib.request.Request(
        "http://localhost:5000/api/agents?page=1&pageSize=1",
        headers={"X-Tenant-ID": "tenant-default"},
    )
    try:
        with urllib.request.urlopen(req, timeout=TIMEOUT) as r:
            data = json.loads(r.read())
            count = data.get("totalCount", 0)
            return count >= 200, f"totalCount={count}"
    except Exception as exc:
        return False, str(exc)


def nexus_swarms_16() -> tuple[bool, str]:
    req = urllib.request.Request(
        "http://localhost:5000/api/swarms",
        headers={"X-Tenant-ID": "tenant-default"},
    )
    try:
        with urllib.request.urlopen(req, timeout=TIMEOUT) as r:
            data = json.loads(r.read())
            count = data.get("totalCount", 0)
            return count >= 16, f"totalCount={count}"
    except Exception as exc:
        return False, str(exc)


def nexus_metrics() -> tuple[bool, str]:
    ok, msg = http_get("http://localhost:5000/metrics")
    return ok, msg


def kafka_tcp() -> tuple[bool, str]:
    return tcp_connect("localhost", 9092)


def neo4j_http() -> tuple[bool, str]:
    return http_get("http://localhost:7474")


def redis_tcp() -> tuple[bool, str]:
    return tcp_connect("localhost", 6379)


def nats_tcp() -> tuple[bool, str]:
    return tcp_connect("localhost", 4222)


def prometheus_http() -> tuple[bool, str]:
    return http_get("http://localhost:9090/-/healthy")


def grafana_http() -> tuple[bool, str]:
    return http_get("http://localhost:3000/api/health")


def jaeger_http() -> tuple[bool, str]:
    return http_get("http://localhost:16686")


def minio_http() -> tuple[bool, str]:
    return http_get("http://localhost:9000/minio/health/live")


def elasticsearch_http() -> tuple[bool, str]:
    return http_get("http://localhost:9200/_cluster/health")


def mosquitto_tcp() -> tuple[bool, str]:
    return tcp_connect("localhost", 1883)


# ── Service registry ──────────────────────────────────────────────────────────

SERVICES: list[ServiceCheck] = [
    ServiceCheck("nexus-api /health",      nexus_api_health,   critical=True),
    ServiceCheck("nexus-api 200 agents",   nexus_agents_200,   critical=True),
    ServiceCheck("nexus-api 16 swarms",    nexus_swarms_16,    critical=True),
    ServiceCheck("nexus-api /metrics",     nexus_metrics,      critical=False),
    ServiceCheck("kafka:9092",             kafka_tcp,          critical=False),
    ServiceCheck("neo4j:7474",             neo4j_http,         critical=False),
    ServiceCheck("redis:6379",             redis_tcp,          critical=False),
    ServiceCheck("nats:4222",              nats_tcp,           critical=False),
    ServiceCheck("prometheus:9090",        prometheus_http,    critical=False),
    ServiceCheck("grafana:3000",           grafana_http,       critical=False),
    ServiceCheck("jaeger:16686",           jaeger_http,        critical=False),
    ServiceCheck("minio:9000",             minio_http,         critical=False),
    ServiceCheck("elasticsearch:9200",     elasticsearch_http, critical=False),
    ServiceCheck("mosquitto:1883",         mosquitto_tcp,      critical=False),
]

# ── Runner ────────────────────────────────────────────────────────────────────

RESET  = "\033[0m"
GREEN  = "\033[32m"
RED    = "\033[31m"
YELLOW = "\033[33m"
BOLD   = "\033[1m"


def run_check_with_retry(svc: ServiceCheck) -> CheckResult:
    last_msg = "no attempt"
    for attempt in range(1, MAX_RETRIES + 1):
        ok, msg = svc.check_fn()
        if ok:
            return CheckResult(svc.name, True, msg)
        last_msg = msg
        if attempt < MAX_RETRIES:
            time.sleep(RETRY_DELAY)
    return CheckResult(svc.name, False, last_msg)


def main() -> int:
    print(f"\n{BOLD}Nexus HyperIntelligence — Local Validation{RESET}")
    print("=" * 56)

    results: list[CheckResult] = []
    for svc in SERVICES:
        result = run_check_with_retry(svc)
        results.append(result)
        icon = f"{GREEN}✓{RESET}" if result.ok else (f"{RED}✗{RESET}" if svc.critical else f"{YELLOW}⚠{RESET}")
        label = f"{svc.name:<35}"
        print(f"  {icon}  {label} {result.message}")

    failed_critical = [r for r, s in zip(results, SERVICES) if not r.ok and s.critical]
    failed_optional = [r for r, s in zip(results, SERVICES) if not r.ok and not s.critical]
    passed = [r for r in results if r.ok]

    print("\n" + "=" * 56)
    print(f"  {GREEN}Passed : {len(passed)}{RESET}")
    if failed_optional:
        print(f"  {YELLOW}Warning: {len(failed_optional)} optional service(s) unreachable{RESET}")
    if failed_critical:
        print(f"  {RED}FAILED : {len(failed_critical)} critical check(s){RESET}")
        for r in failed_critical:
            print(f"    - {r.name}: {r.message}")
        print()
        return 1

    print(f"\n{GREEN}{BOLD}All critical checks passed.{RESET}\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())

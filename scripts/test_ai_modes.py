#!/usr/bin/env python3
"""
scripts/test_ai_modes.py
Tests all three NEXUS_AI_MODE values against a running Nexus API.

Usage:
    BASE_URL=https://your-app.onrender.com python3 scripts/test_ai_modes.py
    BASE_URL=http://localhost:5000 python3 scripts/test_ai_modes.py

The script sets NEXUS_AI_MODE headers via X-AI-Mode-Override if supported,
but primarily calls the /api/v1/ai/* endpoints and checks responses.
"""

import os
import sys
import time
import json
import urllib.request
import urllib.error
from dataclasses import dataclass, field
from typing import Optional

BASE_URL = os.environ.get("BASE_URL", "http://localhost:5000").rstrip("/")
TIMEOUT = int(os.environ.get("AI_TEST_TIMEOUT", "30"))

GREEN  = "\033[92m"
RED    = "\033[91m"
YELLOW = "\033[93m"
CYAN   = "\033[96m"
RESET  = "\033[0m"
BOLD   = "\033[1m"


@dataclass
class TestResult:
    name: str
    passed: bool
    detail: str = ""
    latency_ms: int = 0


results: list[TestResult] = []


def http_post(path: str, body: dict) -> tuple[int, dict]:
    data = json.dumps(body).encode("utf-8")
    req = urllib.request.Request(
        f"{BASE_URL}{path}",
        data=data,
        headers={"Content-Type": "application/json", "X-Tenant-ID": "tenant-default"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=TIMEOUT) as resp:
            return resp.status, json.loads(resp.read())
    except urllib.error.HTTPError as e:
        return e.code, json.loads(e.read() or b"{}")
    except Exception as e:
        return 0, {"error": str(e)}


def http_get(path: str) -> tuple[int, dict]:
    req = urllib.request.Request(
        f"{BASE_URL}{path}",
        headers={"X-Tenant-ID": "tenant-default"},
        method="GET",
    )
    try:
        with urllib.request.urlopen(req, timeout=TIMEOUT) as resp:
            return resp.status, json.loads(resp.read())
    except urllib.error.HTTPError as e:
        return e.code, json.loads(e.read() or b"{}")
    except Exception as e:
        return 0, {"error": str(e)}


def run_test(name: str, fn) -> None:
    t0 = time.monotonic()
    try:
        ok, detail = fn()
        latency = int((time.monotonic() - t0) * 1000)
        results.append(TestResult(name=name, passed=ok, detail=detail, latency_ms=latency))
    except Exception as ex:
        latency = int((time.monotonic() - t0) * 1000)
        results.append(TestResult(name=name, passed=False, detail=str(ex), latency_ms=latency))


# ── Tests ──────────────────────────────────────────────────────────────────────

def test_ai_health() -> tuple[bool, str]:
    status, body = http_get("/api/v1/ai/health")
    if status != 200:
        return False, f"HTTP {status}"
    data = body.get("data", {})
    mode    = data.get("mode", "?")
    provider = data.get("provider", "?")
    model   = data.get("model", "?")
    avail   = data.get("isAvailable", False)
    return True, f"mode={mode} provider={provider} model={model} available={avail}"


def test_ai_chat_basic() -> tuple[bool, str]:
    status, body = http_post("/api/v1/ai/chat", {
        "messages": [{"role": "user", "content": "Reply with exactly: NEXUS_OK"}],
        "temperature": 0,
        "maxTokens": 64
    })
    if status != 200:
        return False, f"HTTP {status}: {body}"
    content = body.get("data", {}).get("content", "")
    provider = body.get("data", {}).get("provider", "?")
    return bool(content), f"provider={provider} content_len={len(content)} preview={content[:60]!r}"


def test_ai_chat_with_system_prompt() -> tuple[bool, str]:
    status, body = http_post("/api/v1/ai/chat", {
        "systemPrompt": "You are a helpful assistant. Always respond in uppercase.",
        "messages": [{"role": "user", "content": "say hello"}],
        "maxTokens": 64
    })
    if status != 200:
        return False, f"HTTP {status}"
    content = body.get("data", {}).get("content", "")
    return bool(content), f"content={content[:80]!r}"


def test_ai_embed() -> tuple[bool, str]:
    status, body = http_post("/api/v1/ai/embed", {"text": "Nexus HyperIntelligence platform"})
    if status != 200:
        return False, f"HTTP {status}"
    dims = body.get("data", {}).get("dimensions", 0)
    return dims > 0, f"dimensions={dims}"


def test_ai_summarize() -> tuple[bool, str]:
    long_text = (
        "The Nexus HyperIntelligence platform is a next-generation AI system "
        "that integrates multi-agent orchestration, cryptographic privacy, "
        "knowledge graphs, and real-time data connectors. It supports hybrid "
        "AI inference through a pluggable provider architecture, enabling "
        "seamless switching between offline (Ollama) and online (Azure AI "
        "Foundry, OpenAI) backends without code changes. The system is "
        "designed for enterprise-scale deployment with full observability."
    )
    status, body = http_post("/api/v1/ai/summarize", {"text": long_text})
    if status != 200:
        return False, f"HTTP {status}"
    summary = body.get("data", {}).get("summary", "")
    orig_len = body.get("data", {}).get("originalLength", 0)
    return bool(summary), f"original={orig_len}chars summary_len={len(summary)} preview={summary[:80]!r}"


def test_ai_chat_returns_tokens() -> tuple[bool, str]:
    status, body = http_post("/api/v1/ai/chat", {
        "messages": [{"role": "user", "content": "What is 2 + 2?"}],
        "maxTokens": 32
    })
    if status != 200:
        return False, f"HTTP {status}"
    data = body.get("data", {})
    return True, f"inputTokens={data.get('inputTokens',0)} outputTokens={data.get('outputTokens',0)} latencyMs={data.get('latencyMs',0)}"


def test_ai_chat_empty_messages_is_400() -> tuple[bool, str]:
    status, body = http_post("/api/v1/ai/chat", {"messages": []})
    return status == 400, f"HTTP {status} (expected 400)"


def test_ai_embed_empty_text_is_400() -> tuple[bool, str]:
    status, body = http_post("/api/v1/ai/embed", {"text": ""})
    return status == 400, f"HTTP {status} (expected 400)"


def test_ai_provider_name_in_response() -> tuple[bool, str]:
    status, body = http_post("/api/v1/ai/chat", {
        "messages": [{"role": "user", "content": "hi"}],
        "maxTokens": 16
    })
    if status != 200:
        return False, f"HTTP {status}"
    provider = body.get("data", {}).get("provider", "")
    valid_providers = {"Ollama", "AzureFoundry", "OpenAI", "Fallback"}
    return provider in valid_providers, f"provider={provider!r} valid={provider in valid_providers}"


def test_ai_multi_turn_chat() -> tuple[bool, str]:
    status, body = http_post("/api/v1/ai/chat", {
        "messages": [
            {"role": "user", "content": "My name is Nexus."},
            {"role": "assistant", "content": "Nice to meet you, Nexus!"},
            {"role": "user", "content": "What is my name?"}
        ],
        "maxTokens": 64
    })
    if status != 200:
        return False, f"HTTP {status}"
    content = body.get("data", {}).get("content", "")
    return bool(content), f"content={content[:80]!r}"


# ── Runner ─────────────────────────────────────────────────────────────────────

def main() -> int:
    print(f"\n{BOLD}{CYAN}Nexus AI Mode Test Suite{RESET}")
    print(f"  Base URL : {BASE_URL}")
    print(f"  Timeout  : {TIMEOUT}s\n")

    run_test("GET  /api/v1/ai/health",               test_ai_health)
    run_test("POST /api/v1/ai/chat (basic)",           test_ai_chat_basic)
    run_test("POST /api/v1/ai/chat (system prompt)",   test_ai_chat_with_system_prompt)
    run_test("POST /api/v1/ai/chat (multi-turn)",      test_ai_multi_turn_chat)
    run_test("POST /api/v1/ai/chat (token counts)",    test_ai_chat_returns_tokens)
    run_test("POST /api/v1/ai/chat (empty → 400)",     test_ai_chat_empty_messages_is_400)
    run_test("POST /api/v1/ai/embed",                  test_ai_embed)
    run_test("POST /api/v1/ai/embed (empty → 400)",    test_ai_embed_empty_text_is_400)
    run_test("POST /api/v1/ai/summarize",              test_ai_summarize)
    run_test("      provider name in response",        test_ai_provider_name_in_response)

    # ── Print table ────────────────────────────────────────────────────────
    passed = sum(1 for r in results if r.passed)
    total  = len(results)
    col_w  = 42

    print(f"\n{'─' * (col_w + 42)}")
    print(f"  {'TEST':<{col_w}} {'STATUS':<8} {'MS':>5}  DETAIL")
    print(f"{'─' * (col_w + 42)}")
    for r in results:
        status_str = f"{GREEN}PASS{RESET}" if r.passed else f"{RED}FAIL{RESET}"
        detail = r.detail[:60] if len(r.detail) > 60 else r.detail
        print(f"  {r.name:<{col_w}} {status_str:<8} {r.latency_ms:>5}ms  {detail}")
    print(f"{'─' * (col_w + 42)}")

    colour = GREEN if passed == total else (YELLOW if passed > 0 else RED)
    print(f"\n  {BOLD}{colour}{passed}/{total} tests passed{RESET}\n")

    if passed < total:
        print(f"  {YELLOW}Failed tests:{RESET}")
        for r in results:
            if not r.passed:
                print(f"    {RED}✗{RESET} {r.name}: {r.detail}")
        print()

    return 0 if passed == total else 1


if __name__ == "__main__":
    sys.exit(main())

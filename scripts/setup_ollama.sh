#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# scripts/setup_ollama.sh
# Installs Ollama and pulls required models on Ubuntu 22.04.
# Usage:
#   chmod +x scripts/setup_ollama.sh
#   ./scripts/setup_ollama.sh
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail

OLLAMA_MODEL="${OLLAMA_MODEL:-qwen3}"
OLLAMA_EMBED_MODEL="${OLLAMA_EMBED_MODEL:-nomic-embed-text}"
OLLAMA_BASE_URL="${OLLAMA_BASE_URL:-http://localhost:11434}"
MAX_WAIT_SECONDS=120

log()  { printf '\033[0;34m[nexus]\033[0m %s\n' "$*"; }
ok()   { printf '\033[0;32m[  ok]\033[0m %s\n' "$*"; }
warn() { printf '\033[0;33m[warn]\033[0m %s\n' "$*"; }
die()  { printf '\033[0;31m[fail]\033[0m %s\n' "$*" >&2; exit 1; }

# ── 1. Install Ollama ────────────────────────────────────────────────────────
if command -v ollama &>/dev/null; then
    ok "Ollama already installed: $(ollama --version 2>/dev/null || echo 'version unknown')"
else
    log "Installing Ollama..."
    if [[ "$(uname -s)" != "Linux" ]]; then
        die "Automatic installation is only supported on Linux. Visit https://ollama.com/download for other platforms."
    fi
    curl -fsSL https://ollama.com/install.sh | sh
    ok "Ollama installed successfully"
fi

# ── 2. Start Ollama service if not running ───────────────────────────────────
if ! curl -sf "${OLLAMA_BASE_URL}/api/tags" &>/dev/null; then
    log "Starting Ollama service in background..."
    nohup ollama serve &>/tmp/ollama.log &
    OLLAMA_PID=$!
    log "Ollama PID: ${OLLAMA_PID}"

    log "Waiting for Ollama to be ready (up to ${MAX_WAIT_SECONDS}s)..."
    waited=0
    until curl -sf "${OLLAMA_BASE_URL}/api/tags" &>/dev/null; do
        sleep 2
        waited=$((waited + 2))
        if [[ $waited -ge $MAX_WAIT_SECONDS ]]; then
            die "Ollama did not start within ${MAX_WAIT_SECONDS} seconds. Check /tmp/ollama.log"
        fi
        log "  Waiting... (${waited}s)"
    done
    ok "Ollama is ready"
else
    ok "Ollama is already running at ${OLLAMA_BASE_URL}"
fi

# ── 3. Pull required models ──────────────────────────────────────────────────
pull_model() {
    local model="$1"
    log "Checking model: ${model}..."
    if ollama list 2>/dev/null | grep -q "^${model}"; then
        ok "Model '${model}' already present"
    else
        log "Pulling model '${model}' (this may take several minutes)..."
        ollama pull "${model}" || die "Failed to pull model '${model}'"
        ok "Model '${model}' pulled successfully"
    fi
}

pull_model "${OLLAMA_MODEL}"
pull_model "${OLLAMA_EMBED_MODEL}"

# ── 4. Verify models are functional ─────────────────────────────────────────
log "Verifying chat model '${OLLAMA_MODEL}'..."
test_response=$(curl -sf "${OLLAMA_BASE_URL}/api/generate" \
    -d "{\"model\":\"${OLLAMA_MODEL}\",\"prompt\":\"Reply with: OK\",\"stream\":false}" \
    | python3 -c "import sys,json; print(json.load(sys.stdin).get('response','').strip())" 2>/dev/null || echo "")
if [[ -n "$test_response" ]]; then
    ok "Chat model response: ${test_response}"
else
    warn "Could not verify model response — it may still be loading"
fi

log "Verifying embed model '${OLLAMA_EMBED_MODEL}'..."
embed_dims=$(curl -sf "${OLLAMA_BASE_URL}/api/embed" \
    -d "{\"model\":\"${OLLAMA_EMBED_MODEL}\",\"input\":\"test\"}" \
    | python3 -c "import sys,json; d=json.load(sys.stdin); print(len((d.get('embeddings') or [[]])[0]))" 2>/dev/null || echo "0")
if [[ "$embed_dims" -gt 0 ]]; then
    ok "Embed model returns ${embed_dims}-dimensional vectors"
else
    warn "Could not verify embed model — check Ollama logs"
fi

# ── 5. Print summary ─────────────────────────────────────────────────────────
echo ""
ok "Setup complete! Set these environment variables in your .env:"
echo "  NEXUS_AI_MODE=offline"
echo "  OLLAMA_BASE_URL=${OLLAMA_BASE_URL}"
echo "  OLLAMA_MODEL=${OLLAMA_MODEL}"
echo "  OLLAMA_EMBED_MODEL=${OLLAMA_EMBED_MODEL}"
echo ""
ok "Installed models:"
ollama list 2>/dev/null || true

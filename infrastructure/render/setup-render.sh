#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT_DIR"

if [[ -z "${RENDER_API_KEY:-}" ]]; then
  echo "RENDER_API_KEY is required."
  echo "Create one at Render Dashboard -> Account Settings -> API Keys."
  exit 1
fi

if ! command -v curl >/dev/null 2>&1; then
  echo "curl is required."
  exit 1
fi

if ! command -v python3 >/dev/null 2>&1; then
  echo "python3 is required."
  exit 1
fi

echo "[1/3] Ensuring root render.yaml is synchronized from infrastructure/render/render.yaml"
cp infrastructure/render/render.yaml render.yaml

echo "[2/3] Running Render API deployment orchestration script"
bash scripts/deploy_render.sh

echo "[3/3] Setup complete"
echo "Next:"
echo "  1) Set remaining secrets in Render dashboard using scripts/get_render_secrets.sh"
echo "  2) Trigger deploy if needed from Render dashboard"
echo "  3) Validate online behavior with: BASE_URL=https://<nexus-api>.onrender.com python3 scripts/test_online.py"

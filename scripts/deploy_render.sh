#!/usr/bin/env bash
set -euo pipefail

RENDER_API_BASE="https://api.render.com/v1"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [[ -z "${RENDER_API_KEY:-}" ]]; then
  echo "ERROR: RENDER_API_KEY is required"
  exit 1
fi

if ! command -v curl >/dev/null 2>&1; then
  echo "ERROR: curl is required"
  exit 1
fi

if ! command -v jq >/dev/null 2>&1; then
  echo "ERROR: jq is required"
  exit 1
fi

api_call() {
  local method="$1"
  local endpoint="$2"
  local data="${3:-}"

  if [[ -n "$data" ]]; then
    curl -sS -X "$method" "$RENDER_API_BASE$endpoint" \
      -H "Authorization: Bearer $RENDER_API_KEY" \
      -H "Accept: application/json" \
      -H "Content-Type: application/json" \
      --data "$data"
  else
    curl -sS -X "$method" "$RENDER_API_BASE$endpoint" \
      -H "Authorization: Bearer $RENDER_API_KEY" \
      -H "Accept: application/json"
  fi
}

find_service_id() {
  local service_name="$1"
  api_call GET "/services" | jq -r --arg NAME "$service_name" '.[] | select(.service.name == $NAME) | .service.id' | head -n1
}

upsert_env_var() {
  local service_id="$1"
  local key="$2"
  local value="$3"

  local payload
  payload=$(jq -cn --arg k "$key" --arg v "$value" '{key: $k, value: $v}')
  api_call POST "/services/${service_id}/env-vars" "$payload" >/dev/null
}

create_postgres() {
  echo "Creating/ensuring Postgres: nexus-postgres"
  local payload
  payload=$(jq -cn '{name:"nexus-postgres", plan:"starter", databaseName:"nexus", user:"nexus"}')
  api_call POST "/postgres" "$payload" >/dev/null || true
}

create_redis() {
  echo "Creating/ensuring Redis: nexus-redis"
  local payload
  payload=$(jq -cn '{name:"nexus-redis", plan:"starter", maxmemoryPolicy:"allkeys-lru"}')
  api_call POST "/redis" "$payload" >/dev/null || true
}

create_service() {
  local name="$1"
  local type="$2"
  local plan="$3"
  local image_url="$4"
  local docker_command="${5:-}"

  echo "Creating/ensuring service: $name"

  local payload
  payload=$(jq -cn \
    --arg n "$name" \
    --arg t "$type" \
    --arg p "$plan" \
    --arg i "$image_url" \
    --arg c "$docker_command" \
    'if $c == "" then
      {name:$n, type:$t, plan:$p, runtime:"docker", image:{url:$i}}
     else
      {name:$n, type:$t, plan:$p, runtime:"docker", image:{url:$i}, dockerCommand:$c}
     end')

  api_call POST "/services" "$payload" >/dev/null || true
}

create_nexus_api() {
  echo "Creating/ensuring Web service: nexus-api"

  local payload
  payload=$(jq -cn \
    --arg root "$ROOT_DIR" \
    '{
      name:"nexus-api",
      type:"web_service",
      plan:"free",
      runtime:"docker",
      autoDeploy:"yes",
      dockerContext:".",
      dockerfilePath:"Dockerfile.render",
      healthCheckPath:"/health",
      envVars:[
        {key:"ASPNETCORE_ENVIRONMENT", value:"Production"},
        {key:"ASPNETCORE_FORWARDEDHEADERS_ENABLED", value:"true"}
      ]
    }')

  api_call POST "/services" "$payload" >/dev/null || true
}

trigger_deploy() {
  local service_id="$1"
  echo "Triggering deploy for service id: $service_id"
  api_call POST "/services/${service_id}/deploys" "{}" >/dev/null
}

random_hex_32() {
  if command -v openssl >/dev/null 2>&1; then
    openssl rand -hex 32
  else
    python3 - <<'PY'
import secrets
print(secrets.token_hex(32))
PY
  fi
}

random_base64_48() {
  if command -v openssl >/dev/null 2>&1; then
    openssl rand -base64 48 | tr -d '\n'
  else
    python3 - <<'PY'
import base64
import secrets
print(base64.b64encode(secrets.token_bytes(48)).decode())
PY
  fi
}

main() {
  echo "Starting Render deployment orchestration"

  if [[ -n "${RENDER_BLUEPRINT_ID:-}" ]]; then
    echo "Blueprint mode enabled. Triggering blueprint sync/deploy"
    local blueprint_payload
    blueprint_payload=$(jq -cn --arg y "$(cat "$ROOT_DIR/render.yaml")" '{renderYaml: $y}')
    api_call POST "/blueprints/${RENDER_BLUEPRINT_ID}/deploys" "$blueprint_payload" >/dev/null
    echo "Blueprint deploy triggered for id: $RENDER_BLUEPRINT_ID"
    exit 0
  fi

  create_postgres
  create_redis

  create_service "nexus-neo4j" "private_service" "free" "docker.io/library/neo4j:5.18"
  create_service "nexus-kafka" "private_service" "free" "docker.io/bitnami/kafka:3.6"
  create_service "nexus-weaviate" "private_service" "free" "docker.io/semitechnologies/weaviate:1.25.3"
  create_service "nexus-qdrant" "private_service" "free" "docker.io/qdrant/qdrant:v1.9.1"
  create_service "nexus-minio" "private_service" "free" "docker.io/minio/minio:RELEASE.2024-04-18T19-09-19Z" "server /data --console-address :9001"

  create_nexus_api

  local api_id
  api_id="$(find_service_id "nexus-api")"
  if [[ -z "$api_id" ]]; then
    echo "ERROR: Could not resolve nexus-api service id after creation"
    exit 1
  fi

  upsert_env_var "$api_id" "NEXUS_API_KEY" "${NEXUS_API_KEY:-$(random_hex_32)}"
  upsert_env_var "$api_id" "JWT_SECRET" "${JWT_SECRET:-$(random_base64_48)}"
  upsert_env_var "$api_id" "NEO4J_URI" "bolt://nexus-neo4j:7687"
  upsert_env_var "$api_id" "KAFKA_URL" "nexus-kafka:9092"
  upsert_env_var "$api_id" "WEAVIATE_URL" "http://nexus-weaviate:8080"
  upsert_env_var "$api_id" "QDRANT_URL" "http://nexus-qdrant:6333"
  upsert_env_var "$api_id" "MINIO_ENDPOINT" "http://nexus-minio:9000"

  trigger_deploy "$api_id"

  echo "Render deployment orchestration complete"
  echo "Set managed-service injected vars in dashboard if API endpoints were created before add-ons: REDIS_URL, DATABASE_URL"
}

main "$@"

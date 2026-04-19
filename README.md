# Nexus HyperIntelligence Platform

A showcase-grade, enterprise-ready autonomous intelligence platform featuring 200 AI agents across 16 swarms, post-quantum cryptography, multi-protocol connectors, knowledge graph, vector search, WebAssembly marketplace, eBPF observability, and multi-language SDKs.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                        Nexus HyperIntelligence                       │
│                                                                       │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────────┐  │
│  │  Nexus.API   │  │ Nexus.Agents │  │    Nexus.Crypto          │  │
│  │  REST+GQL    │  │  16 Swarms   │  │  Kyber·Dilithium·ZKP     │  │
│  │  WebSocket   │  │  200 Agents  │  │  Shamir·Homomorphic      │  │
│  └──────┬───────┘  └──────┬───────┘  └──────────────────────────┘  │
│         └─────────────────┤                                          │
│                    ┌──────▼───────┐                                  │
│                    │  Nexus.Core  │                                  │
│                    │  SK·Plugins  │                                  │
│                    │  Connectors  │                                  │
│                    └──────┬───────┘                                  │
│         ┌─────────────────┼──────────────────────┐                 │
│         ▼                 ▼                        ▼                 │
│  ┌─────────────┐  ┌─────────────┐  ┌────────────────────┐          │
│  │    Kafka    │  │    Neo4j    │  │  Weaviate / Qdrant │          │
│  │    Redis    │  │    NATS     │  │    ClickHouse      │          │
│  └─────────────┘  └─────────────┘  └────────────────────┘          │
└─────────────────────────────────────────────────────────────────────┘
```

## Prerequisites

| Tool | Version |
|------|---------|
| Docker Desktop | 24+ |
| .NET SDK | 8.0+ |
| Python | 3.11+ |
| Node.js | 20+ |
| Go | 1.22+ |
| Rust | 1.77+ |
| Java JDK | 17+ |

## Quick Start

```bash
# 1. Clone the repository
git clone https://github.com/nexus-ai/nexus-hyperintelligence.git
cd nexus-hyperintelligence

# 2. Start all infrastructure services
docker compose up -d

# 3. Wait for services to become healthy (~90 seconds)
docker compose ps

# 4. Run the full .NET test suite
dotnet test

# 5. Validate local environment
python scripts/validate_local.py

# 6. Open API Explorer
# REST:    http://localhost:5000/swagger
# GraphQL: http://localhost:5000/graphql
# Metrics: http://localhost:5000/metrics
```

## Services & Ports

| Service | Port | URL |
|---------|------|-----|
| Nexus API | 5000 | http://localhost:5000 |
| Kafka | 9092 | kafka:9092 |
| Neo4j Browser | 7474 | http://localhost:7474 |
| Redis | 6379 | redis-cli -p 6379 |
| NATS | 4222/8222 | http://localhost:8222 |
| Mosquitto MQTT | 1883 | mqtt://localhost:1883 |
| Weaviate | 8080 | http://localhost:8080 |
| Qdrant | 6333 | http://localhost:6333 |
| ClickHouse | 8123 | http://localhost:8123 |
| Grafana | 3000 | http://localhost:3000 (admin/nexus_admin) |
| Prometheus | 9090 | http://localhost:9090 |
| Jaeger | 16686 | http://localhost:16686 |
| MinIO | 9099/9100 | http://localhost:9100 (nexus_admin/nexus_password) |
| Temporal UI | 8088 | http://localhost:8088 |
| Kong Gateway | 8000/8001 | http://localhost:8001 |

## Project Structure

```
nexus-hyperintelligence/
├── src/
│   ├── Nexus.Models/          # Domain models, DTOs, events
│   ├── Nexus.Core/            # Business logic, SK, connectors
│   ├── Nexus.API/             # REST + GraphQL + WebSocket
│   ├── Nexus.Agents/          # 16 swarms, 200 agents, consensus
│   ├── Nexus.Crypto/          # Post-quantum, ZKP, MPC, homomorphic
│   ├── Nexus.Tests.Unit/      # 200+ unit tests
│   └── Nexus.Tests.Integration/  # Integration tests
├── connectors/
│   ├── sap/                   # SAP S/4HANA connector
│   ├── salesforce/            # Salesforce connector
│   ├── servicenow/            # ServiceNow connector
│   └── templates/             # Connector code templates
├── cryptographic-privacy/
│   ├── zkp/                   # Groth16 ZKP prover/verifier (Python)
│   ├── mpc/                   # Shamir MPC 3-of-5 (Python)
│   └── homomorphic/           # Homomorphic aggregation (Python)
├── knowledge-graph/
│   ├── schema/                # GraphQL schema + OWL ontology
│   └── seed/                  # Cypher seed data
├── formal-verification/
│   ├── tla/                   # TLA+ consensus spec
│   ├── coq/                   # Coq proof
│   └── alloy/                 # Alloy model
├── wasm-agents/               # 10 WASM agents (Rust) + Go loader
├── ebpf/                      # eBPF programs (C) + Go loader
├── sdks/                      # Python, JS, .NET, Go, Rust, Java SDKs
├── ci/github-actions/         # 12 GitHub Actions workflows
├── infrastructure/
│   ├── docker/                # Dockerfiles + configs
│   └── k8s/                   # Kubernetes manifests
├── vscode-extension/          # VS Code extension
└── scripts/                   # validate_local.py + helpers
```

## Running Tests

```bash
# All tests
dotnet test

# Unit tests only
dotnet test src/Nexus.Tests.Unit/

# Integration tests only
dotnet test src/Nexus.Tests.Integration/

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Crypto Privacy Demo

```bash
cd cryptographic-privacy/zkp
pip install -r requirements.txt
python groth16.py

cd ../mpc
python shamir.py

cd ../homomorphic
python aggregator.py
```

## WASM Agent Marketplace

```bash
cd wasm-agents
# Build all Rust WASM agents
cargo build --release --target wasm32-unknown-unknown

# Run loader
go run loader/main.go
```

## eBPF Observability

```bash
cd ebpf
# Requires Linux with BPF support
go run loader/main.go
```

## SDK Examples

```bash
# Python SDK
cd sdks/python && pip install -e . && python examples/basic_usage.py

# JavaScript SDK
cd sdks/javascript && npm install && node examples/basic_usage.js

# Go SDK
cd sdks/go && go run examples/main.go

# Rust SDK
cd sdks/rust && cargo run --example main

# Java SDK
cd sdks/java && mvn package && mvn exec:java
```

## Validation Checklist

- [ ] `docker compose ps` — all services healthy
- [ ] `curl http://localhost:5000/health` — 200 OK
- [ ] `curl http://localhost:5000/swagger` — Swagger UI
- [ ] `curl http://localhost:5000/graphql` — GraphQL playground
- [ ] `dotnet test` — 200+ tests pass
- [ ] `python scripts/validate_local.py` — all checks green

## License

MIT © 2026 Nexus HyperIntelligence Contributors

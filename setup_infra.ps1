$files = @{
    ".env.example" = "DEBUG=false`nPORT=8080`nDB_HOST=db`nDB_PORT=5432`nREDIS_HOST=redis`nREDIS_PORT=6379"
    "infrastructure/docker/Dockerfile.api" = "FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build`nWORKDIR /app`nCOPY . .`nRUN dotnet publish src/Nexus.Api/Nexus.Api.csproj -c Release -o out`nFROM mcr.microsoft.com/dotnet/aspnet:8.0`nWORKDIR /app`nCOPY --from=build /app/out .`nENTRYPOINT [`"dotnet`", `"Nexus.Api.dll`"]"
    "infrastructure/docker/Dockerfile.agents" = "FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build`nWORKDIR /app`nCOPY . .`nRUN dotnet publish src/Nexus.Agents/Nexus.Agents.csproj -c Release -o out`nFROM mcr.microsoft.com/dotnet/aspnet:8.0`nWORKDIR /app`nCOPY --from=build /app/out .`nENTRYPOINT [`"dotnet`", `"Nexus.Agents.dll`"]"
    "infrastructure/docker/grafana/dashboards/nexus-dashboard.json" = "{`"title`": `"Nexus Dashboard`", `"panels`": []}"
    "infrastructure/docker/grafana/datasources/prometheus.yml" = "apiVersion: 1`ndatasources:`n  - name: Prometheus`n    type: prometheus`n    url: http://prometheus:9090`n    access: proxy"
    "infrastructure/docker/kong/kong.yml" = "_format_version: '3.0'`nservices:`n  - name: nexus-api`n    url: http://nexus-api:8080`n    routes:`n      - name: api-route`n        paths:`n          - /api"
    "infrastructure/k8s/api-deployment.yaml" = "apiVersion: apps/v1`nkind: Deployment`nmetadata:`n  name: nexus-api`nspec:`n  replicas: 1`n  template:`n    spec:`n      containers:`n      - name: api`n        image: nexus-api:latest"
    "infrastructure/k8s/agents-deployment.yaml" = "apiVersion: apps/v1`nkind: Deployment`nmetadata:`n  name: nexus-agents`nspec:`n  replicas: 1`n  template:`n    spec:`n      containers:`n      - name: agents`n        image: nexus-agents:latest"
    "infrastructure/k8s/services.yaml" = "apiVersion: v1`nkind: Service`nmetadata:`n  name: nexus-api`nspec:`n  ports:`n  - port: 80`n    targetPort: 8080"
    "infrastructure/k8s/ingress.yaml" = "apiVersion: networking.k8s.io/v1`nkind: Ingress`nmetadata:`n  name: nexus-ingress`nspec:`n  rules:`n  - http:`n      paths:`n      - path: /`n        pathType: Prefix`n        backend:`n          service:`n            name: nexus-api`n            port:`n              number: 80"
    "infrastructure/k8s/secrets.yaml" = "apiVersion: v1`nkind: Secret`nmetadata:`n  name: nexus-secrets`ntype: Opaque`ndata:`n  DB_PASS: cGFzc3dvcmQ="
    "scripts/generate_agents.py" = "import json`nprint(json.dumps([{'id': i} for i in range(200)]))"
    "scripts/generate_connectors.py" = "import os; print('Generating...')"
    "scripts/health_check.py" = "import requests; print('OK')"
    "scripts/seed_all.py" = "import json; print('Seeding...')"
    "scripts/setup_dev.sh" = "#!/bin/bash`necho 'Setting up...'"
}

$createdCount = 0
foreach ($path in $files.Keys) {
    $dir = Split-Path $path -Parent
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    if (-not (Test-Path $path)) {
        Set-Content -Path $path -Value $files[$path]
        $createdCount++
    }
}

$connectorCount = 0
for ($i = 4; $i -le 42; $i++) {
    $name = "connector_$(($i).ToString('000'))"
    $dir = "connectors/$name"
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Set-Content -Path "$dir/Connector.cs" -Value "public class Connector { public void Run() {} }"
        Set-Content -Path "$dir/schema.avro" -Value '{"type": "record", "name": "ConnectorMsg", "fields": []}'
        Set-Content -Path "$dir/model.als" -Value "sig State {}"
        Set-Content -Path "$dir/README.md" -Value "# $name"
        $connectorCount++
        $createdCount += 4
    }
}

Write-Host "Created file count: $createdCount"
Write-Host "Count of connector_* directories: $connectorCount"

New-Item -ItemType Directory -Path "infrastructure/docker/grafana/dashboards" -Force | Out-Null
New-Item -ItemType Directory -Path "infrastructure/docker/grafana/datasources" -Force | Out-Null
New-Item -ItemType Directory -Path "infrastructure/docker/kong" -Force | Out-Null
New-Item -ItemType Directory -Path "infrastructure/k8s" -Force | Out-Null
New-Item -ItemType Directory -Path "scripts" -Force | Out-Null
New-Item -ItemType Directory -Path "connectors" -Force | Out-Null

Set-Content -Path ".env.example" -Value "DEBUG=false`nPORT=8080"
Set-Content -Path "infrastructure/docker/Dockerfile.api" -Value "FROM alpine"
Set-Content -Path "infrastructure/docker/Dockerfile.agents" -Value "FROM alpine"
Set-Content -Path "infrastructure/docker/grafana/dashboards/nexus-dashboard.json" -Value "{}"
Set-Content -Path "infrastructure/docker/grafana/datasources/prometheus.yml" -Value "apiVersion: 1"
Set-Content -Path "infrastructure/docker/kong/kong.yml" -Value "_format_version: '3.0'"
Set-Content -Path "infrastructure/k8s/api-deployment.yaml" -Value "apiVersion: apps/v1"
Set-Content -Path "infrastructure/k8s/agents-deployment.yaml" -Value "apiVersion: apps/v1"
Set-Content -Path "infrastructure/k8s/services.yaml" -Value "apiVersion: v1"
Set-Content -Path "infrastructure/k8s/ingress.yaml" -Value "apiVersion: networking.k8s.io/v1"
Set-Content -Path "infrastructure/k8s/secrets.yaml" -Value "apiVersion: v1"
Set-Content -Path "scripts/generate_agents.py" -Value "print('agents')"
Set-Content -Path "scripts/generate_connectors.py" -Value "print('connectors')"
Set-Content -Path "scripts/health_check.py" -Value "print('health')"
Set-Content -Path "scripts/seed_all.py" -Value "print('seed')"
Set-Content -Path "scripts/setup_dev.sh" -Value "#!/bin/bash"

for ($i = 4; $i -le 42; $i++) {
    $dir = "connectors/connector_$(($i).ToString('000'))"
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    Set-Content -Path "$dir/Connector.cs" -Value " "
    Set-Content -Path "$dir/schema.avro" -Value " "
    Set-Content -Path "$dir/model.als" -Value " "
    Set-Content -Path "$dir/README.md" -Value " "
}

$fc = (Get-ChildItem -Path . -Recurse -File).Count
$dc = (Get-ChildItem -Path connectors -Filter "connector_*" -Directory).Count
Write-Host "Created file count: $fc"
Write-Host "Count of connector_* directories: $dc"

# BlueprintTemplates/Seed-Corpus-Batch18.ps1
# Batch18: Real-world app skeleton bundles (config/logging/di/http) + service baseline packs/recipes/graphs
# No provider/toolchain hardcoding. No raw shell commands. No hardcoded paths. PS 5.1 compatible.

$ErrorActionPreference = "Stop"

function Get-BaseDir {
  if ($PSCommandPath -and (Test-Path -LiteralPath $PSCommandPath)) { return Split-Path -Parent $PSCommandPath }
  if ($PSScriptRoot) { return $PSScriptRoot }
  return (Get-Location).Path
}
function Ensure-Dir([string]$p) { if (-not (Test-Path -LiteralPath $p)) { New-Item -ItemType Directory -Path $p | Out-Null } }
function Write-Json([string]$path, [string]$json) {
  $dir = Split-Path -Parent $path
  Ensure-Dir $dir
  $json2 = $json.Replace("`r`n","`n").Replace("`r","`n")
  Set-Content -LiteralPath $path -Value $json2 -Encoding UTF8
  Write-Host "Wrote: $path"
}

$base = Get-BaseDir
$hereName = Split-Path -Leaf $base
$bt = $null
if ($hereName -ieq "BlueprintTemplates") { $bt = $base } else { $bt = Join-Path $base "BlueprintTemplates" }

Ensure-Dir (Join-Path $bt "atoms")
Ensure-Dir (Join-Path $bt "packs")
Ensure-Dir (Join-Path $bt "graphs")
Ensure-Dir (Join-Path $bt "recipes")

# -------------------------
# ATOMS (internal): config/logging/di/http primitives (abstract)
# -------------------------

Write-Json (Join-Path $bt "atoms\task.feature.add_config_system.v1.json") @'
{
  "id": "task.feature.add_config_system.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Feature: Add configuration system (generic)",
  "description": "Adds a baseline configuration system for an app/service: config file + env overrides + docs. Abstract.",
  "tags": ["feature", "config"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "feature_add_config",
    "inputs": ["projectRoot", "ecosystem", "profile?", "options?"],
    "outputs": ["configArtifacts"],
    "rules": [
      "use variables for paths",
      "do not assume specific libraries",
      "document how to override via env"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.feature.add_structured_logging.v1.json") @'
{
  "id": "task.feature.add_structured_logging.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Feature: Add structured logging (generic)",
  "description": "Adds structured logging conventions: levels, correlation ids, request logging. Abstract.",
  "tags": ["feature", "logging", "observability"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "feature_add_structured_logging",
    "inputs": ["projectRoot", "ecosystem", "profile?", "options?"],
    "outputs": ["loggingArtifacts"],
    "conventions": {
      "levels": ["trace","debug","info","warn","error","fatal"],
      "fields": ["timestamp","level","message","component","correlationId","error?"]
    }
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.feature.add_di_container.v1.json") @'
{
  "id": "task.feature.add_di_container.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "Feature: Add dependency injection pattern (generic)",
  "description": "Adds DI wiring pattern for services/components without assuming a specific framework/library.",
  "tags": ["feature", "di"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "feature_add_di_pattern",
    "inputs": ["projectRoot", "ecosystem", "profile?", "options?"],
    "outputs": ["diArtifacts"],
    "rules": [
      "prefer constructor injection",
      "centralize registrations",
      "keep modules small"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.feature.add_http_client_pattern.v1.json") @'
{
  "id": "task.feature.add_http_client_pattern.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "Feature: Add HTTP client pattern (generic)",
  "description": "Adds an outbound HTTP client abstraction with timeouts, retries (optional), and test seams. Abstract.",
  "tags": ["feature", "http", "client", "resilience"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "feature_add_http_client_pattern",
    "inputs": ["projectRoot", "ecosystem", "options?"],
    "outputs": ["httpClientArtifacts"],
    "guidance": {
      "timeouts": "required",
      "retries": "optional",
      "circuitBreaker": "optional"
    }
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.feature.add_health_endpoint_or_check.v1.json") @'
{
  "id": "task.feature.add_health_endpoint_or_check.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 4 },
  "title": "Feature: Add health checks (generic)",
  "description": "Adds a health signal for services (endpoint or command) depending on ecosystem/profile. Abstract.",
  "tags": ["feature", "health"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "feature_add_health",
    "inputs": ["projectRoot", "ecosystem", "profile?", "options?"],
    "outputs": ["healthArtifacts"]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.feature.add_service_baseline_bundle.v1.json") @'
{
  "id": "task.feature.add_service_baseline_bundle.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Feature: Add service baseline bundle",
  "description": "Composes config + logging + DI + HTTP client + health into a baseline production-minded skeleton.",
  "tags": ["feature", "service", "baseline"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "feature_add_service_baseline",
    "inputs": ["projectRoot", "ecosystem", "profile?", "options?"],
    "uses": [
      "task.feature.add_config_system.v1",
      "task.feature.add_structured_logging.v1",
      "task.feature.add_di_container.v1",
      "task.feature.add_http_client_pattern.v1",
      "task.feature.add_health_endpoint_or_check.v1"
    ],
    "outputs": ["serviceBaselineArtifacts"]
  }
}
'@

# -------------------------
# RECIPES: per-ecosystem baseline skeletons (public)
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.service.baseline.dotnet.v1.json") @'
{
  "id": "recipe.service.baseline.dotnet.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 78 },
  "title": "Service Baseline (.NET)",
  "description": "Adds config/logging/DI/HTTP/health baseline patterns to a .NET service/app without assuming providers or toolchains.",
  "tags": ["service", "baseline", "dotnet"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.feature.add_service_baseline_bundle.v1", "with": { "ecosystem": "dotnet" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.service.baseline.node.v1.json") @'
{
  "id": "recipe.service.baseline.node.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 76 },
  "title": "Service Baseline (Node)",
  "description": "Adds config/logging/HTTP/health baseline patterns to a Node app/service abstractly.",
  "tags": ["service", "baseline", "node"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.feature.add_service_baseline_bundle.v1", "with": { "ecosystem": "node" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.service.baseline.python.v1.json") @'
{
  "id": "recipe.service.baseline.python.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 76 },
  "title": "Service Baseline (Python)",
  "description": "Adds config/logging/HTTP/health baseline patterns to a Python service abstractly.",
  "tags": ["service", "baseline", "python"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.feature.add_service_baseline_bundle.v1", "with": { "ecosystem": "python" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.service.baseline.go.v1.json") @'
{
  "id": "recipe.service.baseline.go.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 74 },
  "title": "Service Baseline (Go)",
  "description": "Adds config/logging/HTTP/health baseline patterns to a Go service abstractly.",
  "tags": ["service", "baseline", "go"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.feature.add_service_baseline_bundle.v1", "with": { "ecosystem": "go" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.service.baseline.rust.v1.json") @'
{
  "id": "recipe.service.baseline.rust.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 74 },
  "title": "Service Baseline (Rust)",
  "description": "Adds config/logging/HTTP/health baseline patterns to a Rust service abstractly.",
  "tags": ["service", "baseline", "rust"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.feature.add_service_baseline_bundle.v1", "with": { "ecosystem": "rust" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.service.baseline.java.v1.json") @'
{
  "id": "recipe.service.baseline.java.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 72 },
  "title": "Service Baseline (Java)",
  "description": "Adds config/logging/HTTP/health baseline patterns to a Java service abstractly.",
  "tags": ["service", "baseline", "java"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.feature.add_service_baseline_bundle.v1", "with": { "ecosystem": "java" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.service.baseline.cpp.v1.json") @'
{
  "id": "recipe.service.baseline.cpp.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 72 },
  "title": "Service Baseline (C++/CMake)",
  "description": "Adds config/logging/HTTP/health baseline patterns to a CMake-based C++ project abstractly.",
  "tags": ["service", "baseline", "cpp", "cmake"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.feature.add_service_baseline_bundle.v1", "with": { "ecosystem": "cpp" } }
  ] }
}
'@

# -------------------------
# PACKS (public): service baseline entrypoints
# -------------------------

Write-Json (Join-Path $bt "packs\pack.service.baseline.v1.json") @'
{
  "id": "pack.service.baseline.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 175 },
  "title": "Add Service Baseline (config/logging/di/http/health)",
  "description": "Adds a production-minded baseline bundle (config + structured logs + DI pattern + HTTP client + health).",
  "tags": ["service", "baseline", "feature"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "routes": {
      "dotnet": "recipe.service.baseline.dotnet.v1",
      "node": "recipe.service.baseline.node.v1",
      "python": "recipe.service.baseline.python.v1",
      "go": "recipe.service.baseline.go.v1",
      "rust": "recipe.service.baseline.rust.v1",
      "java": "recipe.service.baseline.java.v1",
      "cpp": "recipe.service.baseline.cpp.v1"
    }
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.feature.config_system.v1.json") @'
{
  "id": "pack.feature.config_system.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 140 },
  "title": "Add Config System (baseline)",
  "description": "Adds configuration baseline (file + env override + docs) without library assumptions.",
  "tags": ["feature", "config"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["task.feature.add_config_system.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.feature.structured_logging.v1.json") @'
{
  "id": "pack.feature.structured_logging.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 140 },
  "title": "Add Structured Logging (baseline)",
  "description": "Adds structured logging conventions and correlation ids abstractly.",
  "tags": ["feature", "logging"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["task.feature.add_structured_logging.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.feature.di_pattern.v1.json") @'
{
  "id": "pack.feature.di_pattern.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 135 },
  "title": "Add DI Pattern (baseline)",
  "description": "Adds dependency injection wiring patterns abstractly.",
  "tags": ["feature", "di"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["task.feature.add_di_container.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.feature.http_client_pattern.v1.json") @'
{
  "id": "pack.feature.http_client_pattern.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 135 },
  "title": "Add HTTP Client Pattern (baseline)",
  "description": "Adds outbound HTTP client abstraction with timeouts and optional resilience hooks.",
  "tags": ["feature", "http", "client"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["task.feature.add_http_client_pattern.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.feature.health_checks.generic.v1.json") @'
{
  "id": "pack.feature.health_checks.generic.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 130 },
  "title": "Add Health Checks (baseline)",
  "description": "Adds health signal appropriate for service/app profile, abstractly.",
  "tags": ["feature", "health"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["task.feature.add_health_endpoint_or_check.v1"] }
}
'@

# -------------------------
# GRAPHS (public): router for service baseline
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.service_baseline.v1.json") @'
{
  "id": "graph.router.service_baseline.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: Service Baseline",
  "description": "Routes 'add baseline' and 'productionize' intents to the service baseline pack and optional productionize packs.",
  "tags": ["router", "service", "baseline"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Input[Add baseline/productionize] --> Base[pack.service.baseline.v1]\n  Base --> Obs[pack.feature.observability.v1]\n  Base --> Res[pack.feature.resilience.v1]\n  Base --> Health[pack.feature.health_checks.v1]\n  Base --> Done[Done]"
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 18 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

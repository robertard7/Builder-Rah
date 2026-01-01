# BlueprintTemplates/Seed-Corpus-Batch20.ps1
# Batch20: Migration + Refactor + Hardening superpowers (atoms/recipes/packs/graphs)
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
# ATOMS (internal): refactor/migration primitives (abstract)
# -------------------------

Write-Json (Join-Path $bt "atoms\task.refactor.safe_rename_move.v1.json") @'
{
  "id": "task.refactor.safe_rename_move.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Refactor: Safe rename/move (project-wide)",
  "description": "Safely renames or moves files/symbols with reference updates and build validation. Abstract.",
  "tags": ["refactor", "rename", "move"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "refactor_safe_rename_move",
    "inputs": ["projectRoot", "changeset"],
    "outputs": ["changesApplied", "validation"],
    "rules": [
      "update references/imports/namespace/module paths",
      "keep public API stable unless explicitly requested",
      "run build+tests if available"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.refactor.extract_interface_or_trait.v1.json") @'
{
  "id": "task.refactor.extract_interface_or_trait.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Refactor: Extract interface/trait/protocol",
  "description": "Extracts an abstraction for a component to enable testing and modularity. Abstract across ecosystems.",
  "tags": ["refactor", "abstraction", "testing"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "refactor_extract_abstraction",
    "inputs": ["projectRoot", "targetSymbol", "ecosystem"],
    "outputs": ["abstractionAdded", "callSitesUpdated"],
    "notes": { "dotnet": "interface", "java": "interface", "go": "interface", "rust": "trait", "python": "protocol/abc", "node": "interface/type shape", "cpp": "pure virtual interface" }
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.refactor.introduce_adapter_layer.v1.json") @'
{
  "id": "task.refactor.introduce_adapter_layer.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "Refactor: Introduce adapter layer",
  "description": "Introduces an adapter/facade around an external dependency to isolate change and ease testing. Abstract.",
  "tags": ["refactor", "adapter", "facade"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "refactor_introduce_adapter",
    "inputs": ["projectRoot", "dependencySurface", "ecosystem"],
    "outputs": ["adapterAdded", "dependencyIsolated"],
    "rules": [
      "keep adapter surface minimal",
      "add tests for adapter boundary if possible",
      "do not hardcode credentials/endpoints"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.refactor.split_monolith_to_packages.v1.json") @'
{
  "id": "task.refactor.split_monolith_to_packages.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Refactor: Split monolith into packages/modules",
  "description": "Creates a plan and applies a minimal split into modules/packages with clear boundaries. Abstract.",
  "tags": ["refactor", "monolith", "modules", "packages"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "refactor_split_monolith",
    "inputs": ["projectRoot", "ecosystem", "targetLayout?", "constraints?"],
    "outputs": ["modulePlan", "modulesCreated", "importsUpdated"],
    "defaults": { "targetLayout": "by_domain" },
    "rules": [
      "start with smallest safe split",
      "prefer domain boundaries over technical layers",
      "keep build green after each step"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.migrate.dependency_upgrade_plan.v1.json") @'
{
  "id": "task.migrate.dependency_upgrade_plan.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "Migrate: Dependency upgrade plan (safe)",
  "description": "Plans a dependency upgrade with risk classification, incremental steps, and rollback notes. Abstract.",
  "tags": ["migrate", "dependencies", "upgrade"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "migrate_dependency_upgrade_plan",
    "inputs": ["projectRoot", "ecosystem", "targetPackages?", "constraints?"],
    "outputs": ["upgradePlan", "riskNotes"],
    "rules": [
      "prefer smallest diffs",
      "upgrade one dependency group at a time",
      "keep builds reproducible"
    ]
  }
}
'@

# -------------------------
# ATOMS (internal): hardening bundle (composes existing feature atoms from earlier batches)
# -------------------------

Write-Json (Join-Path $bt "atoms\task.harden.pass.baseline_bundle.v1.json") @'
{
  "id": "task.harden.pass.baseline_bundle.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Harden: Baseline pass (timeouts/retries/logging/health)",
  "description": "Applies a baseline hardening pass for services: structured logs, timeouts, optional retries hooks, health checks, and config docs. Abstract.",
  "tags": ["harden", "baseline", "service"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "harden_baseline_pass",
    "inputs": ["projectRoot", "ecosystem", "profile?", "options?"],
    "uses": [
      "task.feature.add_structured_logging.v1",
      "task.feature.add_http_client_pattern.v1",
      "task.feature.add_health_endpoint_or_check.v1",
      "task.feature.add_config_system.v1"
    ],
    "outputs": ["hardeningSummary"],
    "defaults": { "retries": "optional", "circuitBreaker": "optional" }
  }
}
'@

# -------------------------
# RECIPES (public): refactor and hardening flows
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.refactor.safe_rename_move.v1.json") @'
{
  "id": "recipe.refactor.safe_rename_move.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 70 },
  "title": "Refactor: Safe rename/move",
  "description": "Safely renames/moves with reference updates and validation.",
  "tags": ["refactor", "rename", "move"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.refactor.safe_rename_move.v1" }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.refactor.extract_abstraction.v1.json") @'
{
  "id": "recipe.refactor.extract_abstraction.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 72 },
  "title": "Refactor: Extract abstraction",
  "description": "Extracts an interface/trait/protocol around a component for modularity/testing.",
  "tags": ["refactor", "abstraction"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.refactor.extract_interface_or_trait.v1" }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.refactor.introduce_adapter.v1.json") @'
{
  "id": "recipe.refactor.introduce_adapter.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 68 },
  "title": "Refactor: Introduce adapter layer",
  "description": "Wraps an external dependency surface behind an adapter/facade for stability/testing.",
  "tags": ["refactor", "adapter", "facade"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.refactor.introduce_adapter_layer.v1" }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.refactor.split_monolith.v1.json") @'
{
  "id": "recipe.refactor.split_monolith.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 74 },
  "title": "Refactor: Split monolith into modules",
  "description": "Plans and applies a minimal safe split of a monolith into modules/packages.",
  "tags": ["refactor", "modules", "monolith"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.refactor.split_monolith_to_packages.v1" }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.harden.baseline_pass.v1.json") @'
{
  "id": "recipe.harden.baseline_pass.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 76 },
  "title": "Harden: Baseline pass (service)",
  "description": "Applies baseline hardening (logging/timeouts/health/config) to a service without toolchain assumptions.",
  "tags": ["harden", "baseline", "service"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.harden.pass.baseline_bundle.v1" }
  ] }
}
'@

# -------------------------
# PACKS (public): migration/refactor/hardening entrypoints
# -------------------------

Write-Json (Join-Path $bt "packs\pack.refactor.safe_rename_move.v1.json") @'
{
  "id": "pack.refactor.safe_rename_move.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 155 },
  "title": "Refactor: Safe rename/move (validated)",
  "description": "Safely renames/moves and validates by building/testing where applicable.",
  "tags": ["refactor", "rename", "move"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.refactor.safe_rename_move.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.refactor.extract_abstraction.v1.json") @'
{
  "id": "pack.refactor.extract_abstraction.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 160 },
  "title": "Refactor: Extract abstraction (interface/trait/protocol)",
  "description": "Extracts a clean abstraction for modularity and testing.",
  "tags": ["refactor", "abstraction"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.refactor.extract_abstraction.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.refactor.introduce_adapter.v1.json") @'
{
  "id": "pack.refactor.introduce_adapter.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 150 },
  "title": "Refactor: Introduce adapter/facade layer",
  "description": "Isolates external dependencies behind a thin adapter layer for stability and tests.",
  "tags": ["refactor", "adapter", "facade"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.refactor.introduce_adapter.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.refactor.split_monolith.v1.json") @'
{
  "id": "pack.refactor.split_monolith.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 165 },
  "title": "Refactor: Split monolith into modules/packages",
  "description": "Plans and applies a safe modular split with minimal diffs.",
  "tags": ["refactor", "modules", "monolith"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.refactor.split_monolith.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.harden.baseline_pass.v1.json") @'
{
  "id": "pack.harden.baseline_pass.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 170 },
  "title": "Harden: Baseline pass (logging/timeouts/health/config)",
  "description": "Applies baseline hardening pass to services/apps without hardcoding tools or providers.",
  "tags": ["harden", "baseline", "service"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.harden.baseline_pass.v1"] }
}
'@

# -------------------------
# GRAPHS (public): router for migrations/refactors
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.migration_refactor.v1.json") @'
{
  "id": "graph.router.migration_refactor.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: Migration + Refactor",
  "description": "Routes refactor/migration/hardening intents to the appropriate packs.",
  "tags": ["router", "refactor", "migration", "hardening"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Input[Refactor/Migrate request] --> Kind{which?}\n  Kind -->|rename/move| R[pack.refactor.safe_rename_move.v1]\n  Kind -->|extract abstraction| A[pack.refactor.extract_abstraction.v1]\n  Kind -->|adapter layer| AD[pack.refactor.introduce_adapter.v1]\n  Kind -->|split monolith| S[pack.refactor.split_monolith.v1]\n  Kind -->|hardening pass| H[pack.harden.baseline_pass.v1]\n  R --> Done[Done]\n  A --> Done\n  AD --> Done\n  S --> Done\n  H --> Done"
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 20 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

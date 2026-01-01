# BlueprintTemplates/Seed-Corpus-Batch23.ps1
# Batch23: API contract + client SDK neutral (intent-only) atoms/recipes/packs/graphs
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
# ATOMS (internal): API contract primitives (abstract)
# -------------------------

Write-Json (Join-Path $bt "atoms\task.api.define_contract.v1.json") @'
{
  "id": "task.api.define_contract.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "API: Define contract (endpoints, models, errors)",
  "description": "Defines an API contract (routes, request/response shapes, error model) without selecting a tooling format.",
  "tags": ["api", "contract", "models"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "api_define_contract",
    "inputs": ["projectRoot", "serviceType?", "useCases?"],
    "outputs": ["apiContract"],
    "rules": [
      "contract must be human-readable",
      "must define error model and status mapping conceptually",
      "avoid tool-specific schema syntax"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.api.versioning_strategy.v1.json") @'
{
  "id": "task.api.versioning_strategy.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "API: Versioning strategy",
  "description": "Defines how API versions are expressed and evolved (path/header/media-type) conceptually, with compatibility rules.",
  "tags": ["api", "versioning", "compat"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "api_define_versioning_strategy",
    "inputs": ["projectRoot", "constraints?"],
    "outputs": ["versioningStrategy"],
    "options": ["path_version", "header_version", "media_type_version"],
    "rules": [
      "default to additive changes within a major version",
      "breaking changes require a new major version",
      "deprecations must have timelines and migration notes"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.api.error_model_contract.v1.json") @'
{
  "id": "task.api.error_model_contract.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "API: Error model contract",
  "description": "Defines a standard error payload model (code, message, details, correlation id) and mapping rules.",
  "tags": ["api", "errors", "observability"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "api_define_error_model",
    "inputs": ["projectRoot", "needs?"],
    "outputs": ["errorModel"],
    "rules": [
      "errors must be stable for clients",
      "include correlation id concept for tracing",
      "do not leak sensitive data in errors"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.api.auth_contract.neutral.v1.json") @'
{
  "id": "task.api.auth_contract.neutral.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "API: Auth contract (neutral)",
  "description": "Defines authentication/authorization contract conceptually (tokens/sessions/keys) without committing to a product.",
  "tags": ["api", "auth", "security"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "api_define_auth_contract",
    "inputs": ["projectRoot", "serviceAudience?", "riskProfile?"],
    "outputs": ["authContract"],
    "defaults": { "riskProfile": "low|medium|high" },
    "rules": [
      "auth is optional; define when required",
      "least privilege, explicit scopes/roles if applicable",
      "never store secrets in repo"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.api.compatibility_gates.v1.json") @'
{
  "id": "task.api.compatibility_gates.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "API: Backward compatibility gates",
  "description": "Defines compatibility checks and gates to prevent breaking changes from shipping silently.",
  "tags": ["api", "compat", "gates", "ci"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "api_define_compatibility_gates",
    "inputs": ["projectRoot", "apiContract", "versioningStrategy"],
    "outputs": ["compatGates", "checklist"],
    "rules": [
      "breaking changes must fail gate unless new major version",
      "additive changes must be documented",
      "deprecations must include migration guidance"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.api.client_sdk_plan.v1.json") @'
{
  "id": "task.api.client_sdk_plan.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "API: Client SDK plan (neutral)",
  "description": "Defines how to produce and maintain client SDKs (manual/typed client/generated) without committing to a generator tool.",
  "tags": ["api", "client", "sdk", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "api_define_client_sdk_plan",
    "inputs": ["projectRoot", "ecosystem", "targetClients?"],
    "outputs": ["clientSdkPlan"],
    "defaults": { "targetClients": ["same_ecosystem", "http_generic"] },
    "rules": [
      "typed client should mirror contract types",
      "clients must handle retries/timeouts responsibly",
      "document versioning alignment with server"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.api.scaffold_contract_docs.v1.json") @'
{
  "id": "task.api.scaffold_contract_docs.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "API: Scaffold contract documentation",
  "description": "Creates human-readable API docs based on the contract (endpoints, examples, errors, auth, versioning).",
  "tags": ["api", "docs", "contract"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "api_scaffold_contract_docs",
    "inputs": ["projectRoot", "apiContract", "errorModel", "authContract?"],
    "outputs": ["docsArtifacts"],
    "rules": [
      "examples must avoid real secrets",
      "include curl-like examples only as pseudocode, not commands",
      "document pagination/filtering where applicable"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.api.deprecation_policy.v1.json") @'
{
  "id": "task.api.deprecation_policy.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "API: Deprecation policy",
  "description": "Defines deprecation process (headers/fields/endpoints), timelines, and migration guidance requirements.",
  "tags": ["api", "deprecation", "policy"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "api_define_deprecation_policy",
    "inputs": ["projectRoot", "versioningStrategy"],
    "outputs": ["deprecationPolicy"],
    "rules": [
      "must include minimum support window",
      "must include migration path",
      "deprecations must be visible in docs and release notes"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.api.contract_tests_baseline.v1.json") @'
{
  "id": "task.api.contract_tests_baseline.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "API: Contract tests baseline",
  "description": "Adds baseline contract tests and golden examples to catch breaking changes and regressions.",
  "tags": ["api", "tests", "contract", "compat"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "api_add_contract_tests_baseline",
    "inputs": ["projectRoot", "apiContract", "versioningStrategy"],
    "outputs": ["testsArtifacts", "goldenExamples"],
    "rules": [
      "tests must be deterministic",
      "include negative tests for error model stability",
      "include backwards-compat checks per gate policy"
    ]
  }
}
'@

# -------------------------
# RECIPES (public): API baseline flows
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.api.contract.baseline.neutral.v1.json") @'
{
  "id": "recipe.api.contract.baseline.neutral.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 82 },
  "title": "API contract baseline (neutral)",
  "description": "Defines API contract, error model, versioning, docs scaffolding, contract tests, deprecation policy, and compatibility gates.",
  "tags": ["api", "contract", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.api.define_contract.v1" },
      { "use": "task.api.error_model_contract.v1" },
      { "use": "task.api.versioning_strategy.v1" },
      { "use": "task.api.auth_contract.neutral.v1" },
      { "use": "task.api.deprecation_policy.v1" },
      { "use": "task.api.compatibility_gates.v1" },
      { "use": "task.api.scaffold_contract_docs.v1" },
      { "use": "task.api.contract_tests_baseline.v1" }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.api.client_sdk.neutral.v1.json") @'
{
  "id": "recipe.api.client_sdk.neutral.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 74 },
  "title": "Client SDK plan (neutral)",
  "description": "Defines a neutral plan for producing typed clients/SDKs and keeping them aligned with server contract/versioning.",
  "tags": ["api", "client", "sdk", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.api.client_sdk_plan.v1" }
  ] }
}
'@

# -------------------------
# PACKS (public): API entrypoints
# -------------------------

Write-Json (Join-Path $bt "packs\pack.api.contract.baseline.neutral.v1.json") @'
{
  "id": "pack.api.contract.baseline.neutral.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 190 },
  "title": "API contract baseline (neutral)",
  "description": "API contract + versioning + error model + docs + gates + contract tests. Tooling-agnostic and safe.",
  "tags": ["api", "contract", "neutral", "compat"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.api.contract.baseline.neutral.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.api.client_sdk.neutral.v1.json") @'
{
  "id": "pack.api.client_sdk.neutral.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 165 },
  "title": "Client SDK plan (neutral)",
  "description": "Defines how to create and maintain client SDKs without committing to a generator tool or vendor.",
  "tags": ["api", "client", "sdk", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.api.client_sdk.neutral.v1"] }
}
'@

# -------------------------
# GRAPHS (public): router for API intents
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.api_contract_and_clients.neutral.v1.json") @'
{
  "id": "graph.router.api_contract_and_clients.neutral.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: API contract + clients (neutral)",
  "description": "Routes API contract and client SDK intents to neutral packs.",
  "tags": ["router", "api", "contract", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Input[API request] --> Kind{need?}\n  Kind -->|contract baseline| A[pack.api.contract.baseline.neutral.v1]\n  Kind -->|client sdk plan| C[pack.api.client_sdk.neutral.v1]\n  A --> Done[Done]\n  C --> Done"
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 23 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

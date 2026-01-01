# BlueprintTemplates/Seed-Corpus-Batch21.ps1
# Batch21: Cloud deploy neutral (intent-only) atoms/recipes/packs/graphs
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
# ATOMS (internal): deploy intent primitives (abstract)
# -------------------------

Write-Json (Join-Path $bt "atoms\task.deploy.define_env_contract.v1.json") @'
{
  "id": "task.deploy.define_env_contract.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Deploy: Define environment variable contract",
  "description": "Defines a stable environment variable contract and documents it (names, types, defaults, required/optional). Abstract.",
  "tags": ["deploy", "env", "contract"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "deploy_define_env_contract",
    "inputs": ["projectRoot", "ecosystem", "profiles?"],
    "outputs": ["envContract", "docsArtifacts"],
    "rules": [
      "no secrets in docs values",
      "defaults must be safe for local dev",
      "prod must require explicit values for sensitive settings"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.deploy.secrets_policy.v1.json") @'
{
  "id": "task.deploy.secrets_policy.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Deploy: Secrets handling policy",
  "description": "Defines and scaffolds a secrets handling policy (never commit secrets; use env/secret store; rotation notes). Abstract.",
  "tags": ["deploy", "secrets", "security"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "deploy_secrets_policy",
    "inputs": ["projectRoot", "ecosystem"],
    "outputs": ["policyDocs", "repoGuards?"],
    "rules": [
      "never introduce real secrets",
      "add placeholders and instructions only",
      "ensure .gitignore patterns exist where appropriate"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.deploy.generate_artifact_manifest.v1.json") @'
{
  "id": "task.deploy.generate_artifact_manifest.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "Deploy: Generate deployable artifact manifest",
  "description": "Defines what gets built and deployed (artifacts, images, packages) and how versions are derived. Abstract.",
  "tags": ["deploy", "artifact", "manifest"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "deploy_generate_artifact_manifest",
    "inputs": ["projectRoot", "ecosystem", "publishProfile?"],
    "outputs": ["artifactManifest"],
    "rules": [
      "no toolchain hardcoding",
      "versions derived from repo/version strategy",
      "document outputs and locations relative to ${projectRoot}"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.deploy.preview_plan.v1.json") @'
{
  "id": "task.deploy.preview_plan.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Deploy: Preview environment plan",
  "description": "Creates a neutral preview deployment plan (staging/preview) including health checks and smoke tests. Abstract.",
  "tags": ["deploy", "preview", "plan"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "deploy_plan_preview",
    "inputs": ["projectRoot", "ecosystem", "serviceType?", "options?"],
    "outputs": ["previewPlan", "smokeChecklist"],
    "defaults": { "serviceType": "web|api|worker|cli" },
    "rules": [
      "no vendor-specific resources",
      "health check endpoint or check required for services",
      "plan must include rollback notes"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.deploy.prod_plan.v1.json") @'
{
  "id": "task.deploy.prod_plan.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Deploy: Production deployment plan",
  "description": "Creates a neutral production deployment plan with promotion gates and rollback strategy. Abstract.",
  "tags": ["deploy", "prod", "plan"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "deploy_plan_prod",
    "inputs": ["projectRoot", "ecosystem", "riskProfile?", "options?"],
    "outputs": ["prodPlan", "rollbackPlan", "gates"],
    "defaults": { "riskProfile": "low|medium|high" },
    "rules": [
      "explicit gate between preview and prod",
      "database migrations must be called out if applicable",
      "use canary/blue-green conceptually (no vendor impl)"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.deploy.db_migration_contract.v1.json") @'
{
  "id": "task.deploy.db_migration_contract.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "Deploy: DB migration contract (optional)",
  "description": "Defines how migrations are run, verified, and rolled back conceptually. Abstract.",
  "tags": ["deploy", "db", "migrations"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "deploy_db_migration_contract",
    "inputs": ["projectRoot", "ecosystem", "dbType?"],
    "outputs": ["migrationContract", "safetyNotes"],
    "rules": [
      "must be safe by default",
      "no destructive changes without explicit approval",
      "include backup/rollback guidance"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.deploy.smoke_test_contract.v1.json") @'
{
  "id": "task.deploy.smoke_test_contract.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "Deploy: Smoke test contract",
  "description": "Defines minimal smoke tests for post-deploy validation (health, critical path). Abstract.",
  "tags": ["deploy", "smoke", "validation"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "deploy_smoke_test_contract",
    "inputs": ["projectRoot", "serviceType?", "options?"],
    "outputs": ["smokeTests"],
    "defaults": { "serviceType": "web|api|worker|cli" },
    "rules": [
      "tests must be minimal and deterministic",
      "include how to capture evidence on failure"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.deploy.rollback_strategy.v1.json") @'
{
  "id": "task.deploy.rollback_strategy.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Deploy: Rollback strategy",
  "description": "Defines a vendor-neutral rollback strategy for deploy failures (code rollback, config rollback, migration notes). Abstract.",
  "tags": ["deploy", "rollback", "safety"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "deploy_rollback_strategy",
    "inputs": ["projectRoot", "ecosystem", "hasDb?"],
    "outputs": ["rollbackStrategy"],
    "rules": [
      "prefer reversible steps",
      "make rollback steps explicit",
      "never delete data by default"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.deploy.promote_preview_to_prod.v1.json") @'
{
  "id": "task.deploy.promote_preview_to_prod.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Deploy: Promote preview to production (gated)",
  "description": "Defines a gated promotion from preview to prod with checks and WAIT_USER if evidence is missing. Abstract.",
  "tags": ["deploy", "promotion", "gates", "wait_user"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "deploy_promote_preview_to_prod",
    "inputs": ["projectRoot", "evidenceBundle?", "gates"],
    "outputs": ["promotionPlan", "waitUser"],
    "rules": [
      "if smoke tests not green => waitUser true",
      "require explicit user confirmation for prod promotion",
      "avoid vendor details"
    ]
  }
}
'@

# -------------------------
# RECIPES (public): deploy flows
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.deploy.neutral.preview.v1.json") @'
{
  "id": "recipe.deploy.neutral.preview.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 78 },
  "title": "Deploy (neutral): Preview environment",
  "description": "Creates a vendor-neutral preview deployment plan: env contract, secrets policy, artifact manifest, smoke tests, rollback.",
  "tags": ["deploy", "preview", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.deploy.define_env_contract.v1" },
      { "use": "task.deploy.secrets_policy.v1" },
      { "use": "task.deploy.generate_artifact_manifest.v1" },
      { "use": "task.deploy.smoke_test_contract.v1" },
      { "use": "task.deploy.rollback_strategy.v1" },
      { "use": "task.deploy.preview_plan.v1" }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.deploy.neutral.prod.v1.json") @'
{
  "id": "recipe.deploy.neutral.prod.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 78 },
  "title": "Deploy (neutral): Production",
  "description": "Creates a vendor-neutral production deployment plan: env/secrets/artifacts, optional db migrations contract, smoke tests, rollback, gates.",
  "tags": ["deploy", "prod", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.deploy.define_env_contract.v1" },
      { "use": "task.deploy.secrets_policy.v1" },
      { "use": "task.deploy.generate_artifact_manifest.v1" },
      { "use": "task.deploy.db_migration_contract.v1" },
      { "use": "task.deploy.smoke_test_contract.v1" },
      { "use": "task.deploy.rollback_strategy.v1" },
      { "use": "task.deploy.prod_plan.v1" }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.deploy.neutral.promote_preview_to_prod.v1.json") @'
{
  "id": "recipe.deploy.neutral.promote_preview_to_prod.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 74 },
  "title": "Deploy (neutral): Promote preview to prod (gated)",
  "description": "Defines a gated promotion plan from preview to production with explicit user confirmation and required evidence.",
  "tags": ["deploy", "promotion", "neutral", "wait_user"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.deploy.promote_preview_to_prod.v1" }
    ]
  }
}
'@

# -------------------------
# PACKS (public): deploy entrypoints
# -------------------------

Write-Json (Join-Path $bt "packs\pack.deploy.neutral.preview.v1.json") @'
{
  "id": "pack.deploy.neutral.preview.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 175 },
  "title": "Deploy Preview (vendor-neutral)",
  "description": "Vendor-neutral preview deployment plan with contracts, smoke tests, rollback.",
  "tags": ["deploy", "preview", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.deploy.neutral.preview.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.deploy.neutral.prod.v1.json") @'
{
  "id": "pack.deploy.neutral.prod.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 180 },
  "title": "Deploy Production (vendor-neutral)",
  "description": "Vendor-neutral production deployment plan with contracts, optional DB migrations contract, smoke tests, rollback.",
  "tags": ["deploy", "prod", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.deploy.neutral.prod.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.deploy.neutral.promote_preview_to_prod.v1.json") @'
{
  "id": "pack.deploy.neutral.promote_preview_to_prod.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 165 },
  "title": "Promote Preview -> Prod (gated, neutral)",
  "description": "Gated promotion plan from preview to production. Requires explicit user confirmation and smoke evidence.",
  "tags": ["deploy", "promotion", "neutral", "wait_user"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.deploy.neutral.promote_preview_to_prod.v1"] }
}
'@

# -------------------------
# GRAPHS (public): router for deploy intents
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.deploy.neutral.v1.json") @'
{
  "id": "graph.router.deploy.neutral.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: Deploy (vendor-neutral)",
  "description": "Routes deploy intents to vendor-neutral packs (preview/prod/promotion).",
  "tags": ["router", "deploy", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Input[Deploy request] --> Env{target?}\n  Env -->|preview| P[pack.deploy.neutral.preview.v1]\n  Env -->|prod| PR[pack.deploy.neutral.prod.v1]\n  Env -->|promote| X[pack.deploy.neutral.promote_preview_to_prod.v1]\n  P --> Done[Done]\n  PR --> Done\n  X --> Done"
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 21 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

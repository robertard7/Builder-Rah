# BlueprintTemplates/Seed-Corpus-Batch22.ps1
# Batch22: Data + persistence neutral (intent-only) atoms/recipes/packs/graphs
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
# ATOMS (internal): data/persistence primitives (abstract)
# -------------------------

Write-Json (Join-Path $bt "atoms\task.data.define_domain_model.v1.json") @'
{
  "id": "task.data.define_domain_model.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Data: Define domain model (entities/value objects)",
  "description": "Defines domain entities/value objects and invariants. Abstract across ecosystems.",
  "tags": ["data", "domain", "model"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "data_define_domain_model",
    "inputs": ["projectRoot", "boundedContext?", "useCases?"],
    "outputs": ["domainModel", "invariants"],
    "rules": [
      "model should be storage-agnostic",
      "avoid leaking persistence concerns into domain types"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.data.define_storage_contract.v1.json") @'
{
  "id": "task.data.define_storage_contract.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Data: Define storage contract (ports/adapters)",
  "description": "Defines a storage contract (interfaces/traits) for persistence operations without committing to a DB or ORM.",
  "tags": ["data", "storage", "contract", "ports"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "data_define_storage_contract",
    "inputs": ["projectRoot", "domainModel", "ecosystem"],
    "outputs": ["storageContract"],
    "rules": [
      "CRUD is not a goal; define operations by use-case",
      "contract must support testing via in-memory implementation"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.data.schema_contract.v1.json") @'
{
  "id": "task.data.schema_contract.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "Data: Schema contract (logical)",
  "description": "Defines a logical schema contract (tables/collections/indexes) independent of any specific database engine.",
  "tags": ["data", "schema", "contract"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "data_define_schema_contract",
    "inputs": ["projectRoot", "domainModel", "queryNeeds?"],
    "outputs": ["schemaContract", "indexNotes"],
    "rules": [
      "avoid DB-specific syntax",
      "include notes for indexes/constraints",
      "privacy: avoid storing secrets or PII unless explicitly required"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.data.migrations_contract.v1.json") @'
{
  "id": "task.data.migrations_contract.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Data: Migrations contract (safe)",
  "description": "Defines how migrations are created, applied, verified, and rolled back conceptually.",
  "tags": ["data", "migrations", "safety"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "data_define_migrations_contract",
    "inputs": ["projectRoot", "schemaContract", "ecosystem"],
    "outputs": ["migrationsContract", "safetyChecklist"],
    "rules": [
      "must be reversible where possible",
      "destructive migrations require explicit user approval",
      "include backup strategy notes"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.data.repository_pattern_scaffold.v1.json") @'
{
  "id": "task.data.repository_pattern_scaffold.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Data: Repository pattern scaffold",
  "description": "Scaffolds repository/service boundaries for persistence with testability and clear transaction boundaries.",
  "tags": ["data", "repository", "transactions"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "data_scaffold_repository_pattern",
    "inputs": ["projectRoot", "storageContract", "ecosystem"],
    "outputs": ["repoScaffold", "txBoundaryNotes"],
    "rules": [
      "explicit transaction boundary per use-case",
      "avoid ambient globals",
      "prefer dependency injection where idiomatic"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.data.add_inmemory_store_for_tests.v1.json") @'
{
  "id": "task.data.add_inmemory_store_for_tests.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "Data: In-memory store for tests",
  "description": "Adds an in-memory persistence implementation for testing that conforms to the storage contract.",
  "tags": ["data", "testing", "inmemory"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "data_add_inmemory_store",
    "inputs": ["projectRoot", "storageContract", "ecosystem"],
    "outputs": ["inMemoryStore", "testsUpdated"],
    "rules": [
      "must be deterministic",
      "must mimic key constraints where possible",
      "do not depend on external services"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.data.cache_strategy_contract.v1.json") @'
{
  "id": "task.data.cache_strategy_contract.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "Data: Cache strategy contract",
  "description": "Defines caching strategy hooks (TTL, invalidation, stampede protection notes) without choosing a cache product.",
  "tags": ["data", "cache", "contract"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "data_define_cache_strategy",
    "inputs": ["projectRoot", "useCases?", "riskProfile?"],
    "outputs": ["cacheContract", "invalidationNotes"],
    "defaults": { "riskProfile": "low|medium|high" },
    "rules": [
      "cache is optional; app must work without it",
      "never cache secrets",
      "document invalidation rules clearly"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.data.choose_storage_option.v1.json") @'
{
  "id": "task.data.choose_storage_option.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Data: Choose storage option (WAIT_USER when unclear)",
  "description": "Selects a storage option (embedded DB, server DB, document store, file-based) based on constraints. If constraints missing, emits WAIT_USER questions.",
  "tags": ["data", "storage", "wait_user", "decision"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "data_choose_storage_option",
    "inputs": ["constraints?", "scale?", "offline?", "consistency?", "opsTolerance?"],
    "outputs": ["storageOption", "waitUser", "questions"],
    "options": ["embedded_sql", "server_sql", "document", "kv", "file_based"],
    "questionRules": [
      "ask for expected scale and durability needs",
      "ask whether offline/local-only is required",
      "ask ops tolerance (managed vs self-hosted)",
      "never ask for provider/toolchain/model ids"
    ]
  }
}
'@

# -------------------------
# RECIPES (public): data baseline flows
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.data.baseline.neutral.v1.json") @'
{
  "id": "recipe.data.baseline.neutral.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 80 },
  "title": "Data baseline (neutral): domain + contract + migrations + tests",
  "description": "Creates a storage-agnostic data layer baseline: domain model, storage contract, logical schema, migrations contract, repository scaffold, in-memory tests.",
  "tags": ["data", "persistence", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.data.define_domain_model.v1" },
      { "use": "task.data.define_storage_contract.v1" },
      { "use": "task.data.schema_contract.v1" },
      { "use": "task.data.migrations_contract.v1" },
      { "use": "task.data.repository_pattern_scaffold.v1" },
      { "use": "task.data.add_inmemory_store_for_tests.v1" }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.data.cache.strategy.neutral.v1.json") @'
{
  "id": "recipe.data.cache.strategy.neutral.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 72 },
  "title": "Cache strategy (neutral)",
  "description": "Defines cache strategy hooks and invalidation rules without selecting a cache product.",
  "tags": ["data", "cache", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.data.cache_strategy_contract.v1" }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.data.choose_storage.neutral.v1.json") @'
{
  "id": "recipe.data.choose_storage.neutral.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 70 },
  "title": "Choose storage option (neutral, may WAIT_USER)",
  "description": "Selects a storage option based on constraints; asks minimal questions if constraints are missing.",
  "tags": ["data", "storage", "neutral", "wait_user"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.data.choose_storage_option.v1" }
  ] }
}
'@

# -------------------------
# PACKS (public): data entrypoints
# -------------------------

Write-Json (Join-Path $bt "packs\pack.data.baseline.neutral.v1.json") @'
{
  "id": "pack.data.baseline.neutral.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 185 },
  "title": "Data baseline (neutral)",
  "description": "Storage-agnostic persistence baseline with domain model, contracts, migration safety, and testability.",
  "tags": ["data", "persistence", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.data.baseline.neutral.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.data.cache.strategy.neutral.v1.json") @'
{
  "id": "pack.data.cache.strategy.neutral.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 160 },
  "title": "Cache strategy (neutral)",
  "description": "Defines cache strategy hooks and invalidation rules, optional and product-agnostic.",
  "tags": ["data", "cache", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.data.cache.strategy.neutral.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.data.choose_storage.neutral.v1.json") @'
{
  "id": "pack.data.choose_storage.neutral.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 155 },
  "title": "Choose storage option (neutral)",
  "description": "Helps select a storage option based on constraints; asks only what’s missing.",
  "tags": ["data", "storage", "neutral", "wait_user"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.data.choose_storage.neutral.v1"] }
}
'@

# -------------------------
# GRAPHS (public): router for data intents
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.data_persistence.neutral.v1.json") @'
{
  "id": "graph.router.data_persistence.neutral.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: Data + Persistence (neutral)",
  "description": "Routes data/persistence intents to neutral packs.",
  "tags": ["router", "data", "persistence", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Input[Data/persistence request] --> Kind{which?}\n  Kind -->|baseline| B[pack.data.baseline.neutral.v1]\n  Kind -->|choose storage| C[pack.data.choose_storage.neutral.v1]\n  Kind -->|cache strategy| K[pack.data.cache.strategy.neutral.v1]\n  B --> Done[Done]\n  C --> Done\n  K --> Done"
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 22 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

# BlueprintTemplates/Seed-Corpus-Batch27.ps1
# Batch27: Testing pyramid + contract tests + golden files + property/fuzz style testing (neutral).
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
# ATOMS (internal): test strategy primitives
# -------------------------

Write-Json (Join-Path $bt "atoms\task.tests.define_pyramid_strategy.v1.json") @'
{
  "id": "task.tests.define_pyramid_strategy.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Tests: Define testing pyramid strategy",
  "description": "Defines a neutral testing pyramid strategy: unit/integration/e2e proportions, target coverage areas, and gating policy.",
  "tags": ["tests", "strategy", "pyramid"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "tests_define_pyramid_strategy",
    "inputs": ["projectRoot", "ecosystem", "appType?", "riskProfile?"],
    "outputs": ["testStrategy"],
    "defaults": { "riskProfile": "standard" },
    "rules": [
      "prefer unit tests for pure logic and deterministic behavior",
      "integration tests for IO boundaries and adapters",
      "e2e tests only for core user flows",
      "gates must be explicit: what blocks merge/release"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.tests.scaffold_unit_tests_if_missing.v1.json") @'
{
  "id": "task.tests.scaffold_unit_tests_if_missing.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Tests: Scaffold unit tests if missing",
  "description": "Ensures a unit test harness exists and adds a small set of starter tests for core modules without choosing a specific framework by name.",
  "tags": ["tests", "unit", "scaffold"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "tests_scaffold_unit_tests",
    "inputs": ["projectRoot", "ecosystem", "entryPoints?"],
    "outputs": ["testProjectOrFolder", "addedTests"],
    "rules": [
      "tests must be deterministic and not rely on network",
      "prefer table-driven tests where natural",
      "name tests after behavior, not implementation",
      "keep fixtures minimal"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.tests.define_contracts.v1.json") @'
{
  "id": "task.tests.define_contracts.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Tests: Define contract tests for boundaries",
  "description": "Defines contract tests for API boundaries, module boundaries, and IO adapters: inputs/outputs, error model, backwards compatibility.",
  "tags": ["tests", "contract", "api"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "tests_define_contracts",
    "inputs": ["projectRoot", "publicInterfaces", "errorModel?"],
    "outputs": ["contractSuitePlan"],
    "rules": [
      "contracts must include invalid/edge inputs",
      "contracts must assert stable error shapes where user-facing",
      "contracts must include backwards-compatible expectations where needed"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.tests.scaffold_contract_tests.v1.json") @'
{
  "id": "task.tests.scaffold_contract_tests.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Tests: Scaffold contract tests",
  "description": "Creates contract test scaffolding and a minimal contract suite based on the contract plan (no framework/toolchain selection).",
  "tags": ["tests", "contract", "scaffold"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "tests_scaffold_contract_tests",
    "inputs": ["projectRoot", "contractSuitePlan"],
    "outputs": ["addedContractTests"],
    "rules": [
      "contracts must be runnable locally and in CI",
      "avoid environment-specific assumptions",
      "keep test data small and committed"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.tests.define_golden_files.v1.json") @'
{
  "id": "task.tests.define_golden_files.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Tests: Define golden file strategy",
  "description": "Defines golden file testing for stable outputs: snapshots, approval workflow, normalization rules, and update policy.",
  "tags": ["tests", "golden", "snapshot"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "tests_define_golden_file_strategy",
    "inputs": ["projectRoot", "outputsToStabilize?"],
    "outputs": ["goldenStrategy"],
    "rules": [
      "normalize line endings, timestamps, and nondeterministic values",
      "golden updates must be intentional and reviewable",
      "store goldens under a clear folder with stable naming"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.tests.scaffold_golden_tests.v1.json") @'
{
  "id": "task.tests.scaffold_golden_tests.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "Tests: Scaffold golden tests",
  "description": "Adds a minimal golden test harness and one or two representative golden tests.",
  "tags": ["tests", "golden", "scaffold"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "tests_scaffold_golden_tests",
    "inputs": ["projectRoot", "goldenStrategy"],
    "outputs": ["addedGoldenTests", "goldenFiles"],
    "rules": [
      "golden tests must explain what output is being locked down and why",
      "avoid huge golden files; keep them focused",
      "normalize nondeterminism"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.tests.define_property_based_scope.v1.json") @'
{
  "id": "task.tests.define_property_based_scope.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "Tests: Define property-based testing scope",
  "description": "Defines properties/invariants for pure logic (parsers, serializers, transforms) and where randomized inputs are useful.",
  "tags": ["tests", "property", "invariants"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "tests_define_property_based_scope",
    "inputs": ["projectRoot", "modules?"],
    "outputs": ["propertyTestPlan"],
    "rules": [
      "only target deterministic functions",
      "define invariants in plain language first",
      "cap runtime and input sizes for CI sanity"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.tests.scaffold_property_tests.v1.json") @'
{
  "id": "task.tests.scaffold_property_tests.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "Tests: Scaffold property-based tests",
  "description": "Adds a small set of property tests based on the plan (no framework selection by name).",
  "tags": ["tests", "property", "scaffold"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "tests_scaffold_property_tests",
    "inputs": ["projectRoot", "propertyTestPlan"],
    "outputs": ["addedPropertyTests"],
    "rules": [
      "keep seed determinism support if available",
      "shrink/reduce failing inputs conceptually",
      "emit failing-case reproduction guidance"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.tests.define_fuzz_targets.v1.json") @'
{
  "id": "task.tests.define_fuzz_targets.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 4 },
  "title": "Tests: Define fuzz targets (safe)",
  "description": "Defines fuzz targets for parsers/decoders/routers with strict safety scope, resource caps, and crash triage workflow.",
  "tags": ["tests", "fuzz", "security", "robustness"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "tests_define_fuzz_targets",
    "inputs": ["projectRoot", "modules", "riskProfile?"],
    "outputs": ["fuzzPlan"],
    "defaults": { "riskProfile": "standard" },
    "rules": [
      "only fuzz pure parsing/decoding boundaries or isolated components",
      "set strict time/memory limits conceptually",
      "define crash reproduction and minimization expectations"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.tests.scaffold_fuzz_harness.v1.json") @'
{
  "id": "task.tests.scaffold_fuzz_harness.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 4 },
  "title": "Tests: Scaffold fuzz harness (safe)",
  "description": "Adds a minimal fuzz harness structure and documentation for running it, without selecting a specific engine by name.",
  "tags": ["tests", "fuzz", "harness"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "tests_scaffold_fuzz_harness",
    "inputs": ["projectRoot", "fuzzPlan"],
    "outputs": ["fuzzHarnessLayout", "docs"],
    "rules": [
      "must be opt-in (not default in quick test run)",
      "must document how to reproduce crashes",
      "must cap resources conceptually"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.tests.define_ci_gates_for_tests.v1.json") @'
{
  "id": "task.tests.define_ci_gates_for_tests.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Tests: Define CI gates for test layers",
  "description": "Defines which tests run on PR, nightly, and release. Establishes time budgets and required signals.",
  "tags": ["tests", "ci", "gates"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "tests_define_ci_gates",
    "inputs": ["projectRoot", "testStrategy", "capabilities?"],
    "outputs": ["ciTestGates"],
    "rules": [
      "PR gates must be fast and deterministic",
      "nightly can include heavier suites",
      "release gates must include contract tests and key integration coverage"
    ]
  }
}
'@

# -------------------------
# RECIPES (public): cohesive testing bundles
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.tests.pyramid.baseline.neutral.v1.json") @'
{
  "id": "recipe.tests.pyramid.baseline.neutral.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 88 },
  "title": "Testing pyramid baseline (neutral)",
  "description": "Defines a testing strategy and scaffolds unit tests with sensible defaults.",
  "tags": ["tests", "pyramid", "unit", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.tests.define_pyramid_strategy.v1" },
    { "use": "task.tests.scaffold_unit_tests_if_missing.v1" },
    { "use": "task.tests.define_ci_gates_for_tests.v1" }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.tests.contracts.neutral.v1.json") @'
{
  "id": "recipe.tests.contracts.neutral.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 86 },
  "title": "Contract tests baseline (neutral)",
  "description": "Defines and scaffolds contract tests for stable boundaries and compatibility.",
  "tags": ["tests", "contract", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.tests.define_contracts.v1" },
    { "use": "task.tests.scaffold_contract_tests.v1" }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.tests.golden_files.neutral.v1.json") @'
{
  "id": "recipe.tests.golden_files.neutral.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 82 },
  "title": "Golden file tests baseline (neutral)",
  "description": "Adds snapshot/golden file testing strategy and scaffolding for stable outputs.",
  "tags": ["tests", "golden", "snapshot", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.tests.define_golden_files.v1" },
    { "use": "task.tests.scaffold_golden_tests.v1" }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.tests.property_based.neutral.v1.json") @'
{
  "id": "recipe.tests.property_based.neutral.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 78 },
  "title": "Property-based tests baseline (neutral)",
  "description": "Defines invariants and scaffolds a small set of property-based tests for deterministic logic.",
  "tags": ["tests", "property", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.tests.define_property_based_scope.v1" },
    { "use": "task.tests.scaffold_property_tests.v1" }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.tests.fuzz.safe.neutral.v1.json") @'
{
  "id": "recipe.tests.fuzz.safe.neutral.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 72 },
  "title": "Fuzz testing harness (safe, neutral)",
  "description": "Defines safe fuzz targets and scaffolds an opt-in harness with strict caps and crash reproduction guidance.",
  "tags": ["tests", "fuzz", "robustness", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.tests.define_fuzz_targets.v1" },
    { "use": "task.tests.scaffold_fuzz_harness.v1" }
  ] }
}
'@

# -------------------------
# PACKS (public): entrypoints
# -------------------------

Write-Json (Join-Path $bt "packs\pack.tests.pyramid.baseline.neutral.v1.json") @'
{
  "id": "pack.tests.pyramid.baseline.neutral.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 175 },
  "title": "Testing pyramid baseline (neutral)",
  "description": "Adds a sane test strategy and unit test scaffolding with CI gating guidance.",
  "tags": ["tests", "pyramid", "unit", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.tests.pyramid.baseline.neutral.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.tests.contracts.neutral.v1.json") @'
{
  "id": "pack.tests.contracts.neutral.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 165 },
  "title": "Contract tests baseline (neutral)",
  "description": "Defines and scaffolds contract tests for stable boundaries and compatibility.",
  "tags": ["tests", "contract", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.tests.contracts.neutral.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.tests.golden_files.neutral.v1.json") @'
{
  "id": "pack.tests.golden_files.neutral.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 150 },
  "title": "Golden file tests (neutral)",
  "description": "Adds snapshot/golden tests to stabilize important outputs with intentional updates.",
  "tags": ["tests", "golden", "snapshot", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.tests.golden_files.neutral.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.tests.property_based.neutral.v1.json") @'
{
  "id": "pack.tests.property_based.neutral.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 145 },
  "title": "Property-based tests (neutral)",
  "description": "Adds invariants and a starter set of property-based tests for deterministic logic.",
  "tags": ["tests", "property", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.tests.property_based.neutral.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.tests.fuzz.safe.neutral.v1.json") @'
{
  "id": "pack.tests.fuzz.safe.neutral.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 125 },
  "title": "Fuzz testing harness (safe, neutral)",
  "description": "Opt-in fuzz harness for parsers/decoders/routers with caps and crash reproduction docs.",
  "tags": ["tests", "fuzz", "robustness", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.tests.fuzz.safe.neutral.v1"] }
}
'@

# -------------------------
# GRAPHS (public): router for testing intents
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.testing_quality.neutral.v1.json") @'
{
  "id": "graph.router.testing_quality.neutral.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: Testing + quality (neutral)",
  "description": "Routes testing and quality intents to appropriate packs: pyramid, contracts, goldens, property, fuzz (safe).",
  "tags": ["router", "tests", "quality", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Input[Testing request] --> K{Need?}\n  K -->|baseline pyramid| P[pack.tests.pyramid.baseline.neutral.v1]\n  K -->|contract tests| C[pack.tests.contracts.neutral.v1]\n  K -->|golden files| G[pack.tests.golden_files.neutral.v1]\n  K -->|property-based| R[pack.tests.property_based.neutral.v1]\n  K -->|fuzz safe| F[pack.tests.fuzz.safe.neutral.v1]\n  P --> Done[Done]\n  C --> Done\n  G --> Done\n  R --> Done\n  F --> Done"
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 27 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

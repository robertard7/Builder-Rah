# BlueprintTemplates/Seed-Corpus-Batch24.ps1
# Batch24: Performance + load + profiling (intent-only) atoms/recipes/packs/graphs
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
# ATOMS (internal): performance primitives (abstract)
# -------------------------

Write-Json (Join-Path $bt "atoms\task.perf.define_slos_and_budgets.v1.json") @'
{
  "id": "task.perf.define_slos_and_budgets.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Perf: Define SLOs and budgets",
  "description": "Defines service SLOs and performance budgets (latency percentiles, throughput, memory, startup) conceptually.",
  "tags": ["perf", "slo", "budgets"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "perf_define_slos_and_budgets",
    "inputs": ["projectRoot", "serviceType?", "userJourneys?"],
    "outputs": ["slos", "perfBudgets"],
    "rules": [
      "use percentiles not averages",
      "budgets must be measurable",
      "include startup time budget for CLI/desktop apps"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.perf.collect_baseline_metrics.v1.json") @'
{
  "id": "task.perf.collect_baseline_metrics.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Perf: Collect baseline metrics",
  "description": "Defines how to collect baseline metrics (timings, allocations, CPU, IO) without committing to a specific profiler/tool.",
  "tags": ["perf", "baseline", "metrics"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "perf_collect_baseline_metrics",
    "inputs": ["projectRoot", "slos", "scenarios?"],
    "outputs": ["baselineReport", "scenarioMatrix"],
    "rules": [
      "baseline must be reproducible",
      "record environment notes (hardware class, OS, runtime version) but not secrets",
      "store report under ${projectRoot}/.rah/perf/ (or equivalent variable path) if available"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.perf.define_load_profile.v1.json") @'
{
  "id": "task.perf.define_load_profile.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Perf: Define load profile",
  "description": "Defines load profile (arrival rate, concurrency, ramp, soak) and success criteria without selecting a load tool.",
  "tags": ["perf", "load", "testing"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "perf_define_load_profile",
    "inputs": ["projectRoot", "slos", "trafficAssumptions?"],
    "outputs": ["loadProfile", "successCriteria"],
    "defaults": {
      "phases": ["smoke", "baseline", "stress", "soak"],
      "durationMinutes": { "smoke": 5, "baseline": 10, "stress": 10, "soak": 30 }
    },
    "rules": [
      "include warmup period",
      "define error budget threshold and timeouts",
      "define data reset strategy for repeatability"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.perf.identify_bottlenecks.v1.json") @'
{
  "id": "task.perf.identify_bottlenecks.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Perf: Identify bottlenecks from evidence",
  "description": "Analyzes baseline evidence to identify bottlenecks: CPU hot paths, allocations, IO waits, lock contention, N+1 patterns.",
  "tags": ["perf", "triage", "bottlenecks"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "perf_identify_bottlenecks",
    "inputs": ["baselineReport", "logs?", "traces?"],
    "outputs": ["bottleneckList", "hypotheses"],
    "rules": [
      "rank by impact vs effort",
      "avoid premature optimization",
      "always propose measurement-first next step"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.perf.propose_optimizations.safe.v1.json") @'
{
  "id": "task.perf.propose_optimizations.safe.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Perf: Propose optimizations (safe)",
  "description": "Proposes safe optimizations that preserve correctness. Requires validation plan and rollback notes.",
  "tags": ["perf", "optimization", "safety"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "perf_propose_optimizations_safe",
    "inputs": ["bottleneckList", "ecosystem", "riskProfile?"],
    "outputs": ["optimizationPlan", "validationPlan", "rollbackPlan"],
    "defaults": { "riskProfile": "low|medium|high" },
    "rules": [
      "do not change observable behavior without tests",
      "include micro-benchmark plan if appropriate",
      "include correctness regression checks"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.perf.regression_gates.v1.json") @'
{
  "id": "task.perf.regression_gates.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Perf: Regression gates",
  "description": "Defines performance regression gates for CI (budgets, thresholds, comparisons) without tying to a specific CI system or tool.",
  "tags": ["perf", "ci", "gates", "budgets"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "perf_define_regression_gates",
    "inputs": ["projectRoot", "perfBudgets", "baselineReport?"],
    "outputs": ["perfGates", "reportingNotes"],
    "rules": [
      "prefer relative comparison + absolute budget caps",
      "allow noise bands, but require investigation when exceeded",
      "gate must be bypassable only with explicit user approval"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.perf.capacity_plan.neutral.v1.json") @'
{
  "id": "task.perf.capacity_plan.neutral.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "Perf: Capacity plan (neutral)",
  "description": "Creates a capacity plan template: scaling assumptions, resource limits, caching notes, and growth model.",
  "tags": ["perf", "capacity", "scaling"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "perf_create_capacity_plan",
    "inputs": ["slos", "loadProfile", "constraints?"],
    "outputs": ["capacityPlan"],
    "rules": [
      "state assumptions clearly",
      "include resource ceilings and alarm thresholds conceptually",
      "avoid vendor-specific autoscaling features"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.perf.ask_user_for_target_load.v1.json") @'
{
  "id": "task.perf.ask_user_for_target_load.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Perf: Ask user for target load (WAIT_USER)",
  "description": "When perf targets are unclear, asks minimal questions needed to define SLOs and load profile.",
  "tags": ["perf", "wait_user", "slo", "load"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "emit_wait_user_questions",
    "inputs": ["serviceType?", "knownConstraints?"],
    "outputs": ["questions", "waitUser"],
    "waitUser": true,
    "questionRules": [
      "ask expected concurrent users or rps range",
      "ask latency targets (p95/p99) per key journey",
      "ask payload sizes and data store involvement",
      "ask whether single-machine or distributed deployment",
      "never ask for provider/toolchain/model ids"
    ]
  }
}
'@

# -------------------------
# RECIPES (public): perf flows
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.perf.baseline_and_gates.neutral.v1.json") @'
{
  "id": "recipe.perf.baseline_and_gates.neutral.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 82 },
  "title": "Performance baseline + regression gates (neutral)",
  "description": "Defines SLOs, collects baseline metrics, defines load profile, and sets regression gates.",
  "tags": ["perf", "baseline", "gates", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.perf.define_slos_and_budgets.v1" },
    { "use": "task.perf.collect_baseline_metrics.v1" },
    { "use": "task.perf.define_load_profile.v1" },
    { "use": "task.perf.regression_gates.v1" }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.perf.optimize_from_evidence.neutral.v1.json") @'
{
  "id": "recipe.perf.optimize_from_evidence.neutral.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 78 },
  "title": "Optimize from evidence (neutral)",
  "description": "Uses baseline evidence to identify bottlenecks and propose safe optimizations with validation/rollback plans.",
  "tags": ["perf", "optimization", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.perf.identify_bottlenecks.v1" },
    { "use": "task.perf.propose_optimizations.safe.v1" }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.perf.capacity_plan.neutral.v1.json") @'
{
  "id": "recipe.perf.capacity_plan.neutral.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 72 },
  "title": "Capacity plan (neutral)",
  "description": "Creates a neutral capacity plan based on SLOs and load profile assumptions.",
  "tags": ["perf", "capacity", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.perf.capacity_plan.neutral.v1" }
  ] }
}
'@

# -------------------------
# PACKS (public): perf entrypoints
# -------------------------

Write-Json (Join-Path $bt "packs\pack.perf.baseline_and_gates.neutral.v1.json") @'
{
  "id": "pack.perf.baseline_and_gates.neutral.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 185 },
  "title": "Performance baseline + regression gates (neutral)",
  "description": "Defines SLOs and budgets, collects baseline metrics, defines load profile, and adds regression gates.",
  "tags": ["perf", "baseline", "gates", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.perf.baseline_and_gates.neutral.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.perf.optimize_from_evidence.neutral.v1.json") @'
{
  "id": "pack.perf.optimize_from_evidence.neutral.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 170 },
  "title": "Optimize from evidence (neutral)",
  "description": "Identifies bottlenecks and proposes safe optimizations with validation and rollback plans.",
  "tags": ["perf", "optimization", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.perf.optimize_from_evidence.neutral.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.perf.capacity_plan.neutral.v1.json") @'
{
  "id": "pack.perf.capacity_plan.neutral.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 160 },
  "title": "Capacity plan (neutral)",
  "description": "Creates a neutral capacity plan template based on SLOs and load assumptions.",
  "tags": ["perf", "capacity", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.perf.capacity_plan.neutral.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.perf.ask_targets.wait_user.v1.json") @'
{
  "id": "pack.perf.ask_targets.wait_user.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 150 },
  "title": "Define performance targets (WAIT_USER)",
  "description": "Asks only the missing questions needed to define SLOs and load profile targets.",
  "tags": ["perf", "wait_user", "slo"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["task.perf.ask_user_for_target_load.v1"] }
}
'@

# -------------------------
# GRAPHS (public): router for perf intents
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.perf_and_capacity.neutral.v1.json") @'
{
  "id": "graph.router.perf_and_capacity.neutral.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: Performance + capacity (neutral)",
  "description": "Routes perf/load/profiling/capacity intents to neutral packs and WAIT_USER when targets are missing.",
  "tags": ["router", "perf", "capacity", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Input[Perf request] --> Targets{have targets?}\n  Targets -->|no| Q[pack.perf.ask_targets.wait_user.v1]\n  Targets -->|yes| Kind{which?}\n  Kind -->|baseline + gates| B[pack.perf.baseline_and_gates.neutral.v1]\n  Kind -->|optimize| O[pack.perf.optimize_from_evidence.neutral.v1]\n  Kind -->|capacity plan| C[pack.perf.capacity_plan.neutral.v1]\n  Q --> Done[Done]\n  B --> Done\n  O --> Done\n  C --> Done"
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 24 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

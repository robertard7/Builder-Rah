# BlueprintTemplates/Seed-Corpus-Batch33.ps1
# Batch33: Capability-normalized execution (neutral)
# Detects repo capabilities and maps goal -> feasible plan, otherwise WAIT_USER with minimal questions.
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
# ATOMS (internal): capabilities model + planning + WAIT_USER
# -------------------------

Write-Json (Join-Path $bt "atoms\task.capabilities.detect_repo_features.v1.json") @'
{
  "id": "task.capabilities.detect_repo_features.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Capabilities: Detect repo features",
  "description": "Detects what a repo can plausibly do without guessing: build/test/run/package formats and key files.",
  "tags": ["capabilities", "detect", "repo", "intake"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "detect_repo_capabilities",
    "inputs": ["projectRoot"],
    "outputs": ["capabilitiesReport"],
    "reportShape": {
      "fields": [
        "ecosystemCandidates[]",
        "hasSolutionOrWorkspace",
        "hasTests",
        "hasLintOrFormatConfig",
        "hasPackagingConfig",
        "entrypoints[]",
        "knownBuildFiles[]",
        "knownTestFiles[]",
        "knownRunTargets[]",
        "knownPackageTargets[]",
        "notes[]"
      ]
    },
    "rules": [
      "detection must be evidence-based (file presence or prior receipts)",
      "do not infer tests exist unless a test target is discoverable",
      "record ambiguities rather than guessing"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.capabilities.normalize_goal_to_feasible_verbs.v1.json") @'
{
  "id": "task.capabilities.normalize_goal_to_feasible_verbs.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Capabilities: Normalize goal to feasible verbs",
  "description": "Given a user goal (one of the core verbs), chooses feasible verbs/sub-steps based on detected capabilities.",
  "tags": ["capabilities", "goal", "plan"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "normalize_goal_to_feasible_verbs",
    "inputs": ["goalVerb", "capabilitiesReport", "ecosystemHint?", "profileHint?"],
    "outputs": ["normalizedGoalPlan"],
    "rules": [
      "if requested verb is infeasible, propose minimal enabling steps (scaffold tests, add config) or WAIT_USER",
      "prefer smallest plan that satisfies the goal",
      "never introduce provider/model/toolchain constraints"
    ],
    "examples": [
      { "ifGoal": "test", "whenMissing": "hasTests=false", "then": "propose scaffold tests OR WAIT_USER approve skip tests" },
      { "ifGoal": "run", "whenMissing": "knownRunTargets empty", "then": "WAIT_USER ask for entrypoint or approve adding one" },
      { "ifGoal": "package_publish", "whenMissing": "hasPackagingConfig=false", "then": "propose scaffold packaging config OR WAIT_USER" }
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.capabilities.map_plan_to_atom_ids.v1.json") @'
{
  "id": "task.capabilities.map_plan_to_atom_ids.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 8 },
  "title": "Capabilities: Map feasible plan to atom IDs",
  "description": "Maps the normalized goal plan into concrete atom IDs (existing task.* templates) using ecosystem/profile hints.",
  "tags": ["capabilities", "map", "atoms", "router"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "map_plan_to_atom_ids",
    "inputs": ["normalizedGoalPlan", "capabilitiesReport", "ecosystemHint?", "profileHint?"],
    "outputs": ["atomExecutionPlan"],
    "mappingRules": [
      "prefer ecosystem-specific atoms when available, else fall back to generic atoms",
      "never reference templates that do not exist; if missing, route to diagnose or WAIT_USER",
      "use deterministic selection order: ecosystem-specific -> profile-specific -> generic"
    ],
    "fallbacks": [
      { "verb": "add_file", "fallbackAtom": "task.add_file.generic.v1" },
      { "verb": "replace_file", "fallbackAtom": "task.replace_file.generic.v1" },
      { "verb": "apply_patch", "fallbackAtom": "task.apply_patch.generic.v1" },
      { "verb": "diagnose_build_fail_triage", "fallbackAtom": "task.diagnose_build_fail_triage.generic.v1" }
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.capabilities.wait_user.resolve_ambiguities.v1.json") @'
{
  "id": "task.capabilities.wait_user.resolve_ambiguities.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Capabilities: WAIT_USER resolve ambiguities",
  "description": "Asks minimal questions to resolve ambiguities discovered during detection or planning.",
  "tags": ["capabilities", "wait_user", "ambiguity"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "emit_wait_user_questions",
    "inputs": ["capabilitiesReport", "goalVerb", "normalizedGoalPlan?"],
    "outputs": ["questions", "waitUser"],
    "waitUser": true,
    "questionRules": [
      "ask only what is required to execute next safe step",
      "prefer: which entrypoint to run, which project within workspace, desired test framework, desired package target type",
      "do not ask for provider/model/toolchain ids"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.capabilities.execute_atom_plan.v1.json") @'
{
  "id": "task.capabilities.execute_atom_plan.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Capabilities: Execute atom plan (delegated)",
  "description": "Executes an atomExecutionPlan as a sequence. Engine decides how to run each atom; template contains no commands.",
  "tags": ["capabilities", "execute", "plan"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "execute_atom_plan",
    "inputs": ["atomExecutionPlan", "projectRoot", "settingsPresetName?"],
    "outputs": ["executionReceipts", "finalStatus"],
    "rules": [
      "stop on hard failure and route to diagnose pack",
      "if an atom returns WAIT_USER, surface it immediately"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.capabilities.goal_runner.v1.json") @'
{
  "id": "task.capabilities.goal_runner.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 8 },
  "title": "Capabilities: Goal runner (detect -> normalize -> map -> execute)",
  "description": "End-to-end capability-aware goal runner. Executes the requested verb using detected repo capabilities and existing atom templates.",
  "tags": ["capabilities", "goal", "unstoppable"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "capability_aware_goal_runner",
    "inputs": ["projectRoot", "goalVerb", "ecosystemHint?", "profileHint?", "settingsPresetName?"],
    "outputs": ["executionReceipts", "finalStatus", "waitUser?", "questions?"],
    "pipeline": [
      { "use": "task.capabilities.detect_repo_features.v1" },
      { "use": "task.capabilities.normalize_goal_to_feasible_verbs.v1" },
      { "use": "task.capabilities.map_plan_to_atom_ids.v1" },
      { "use": "task.capabilities.execute_atom_plan.v1" }
    ],
    "onAmbiguity": { "use": "task.capabilities.wait_user.resolve_ambiguities.v1" },
    "onFailure": { "routeTo": "pack.diagnose.unstoppable.v1" }
  }
}
'@

# -------------------------
# RECIPE (public): capability-aware goal runner
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.capabilities.goal_runner.neutral.v1.json") @'
{
  "id": "recipe.capabilities.goal_runner.neutral.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 86 },
  "title": "Goal runner: Capability-aware (neutral)",
  "description": "Runs a core verb against an existing repo using detection and a deterministic mapping to existing atoms, with minimal WAIT_USER prompts.",
  "tags": ["capabilities", "goal", "neutral", "unstoppable"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.capabilities.goal_runner.v1" }
  ] }
}
'@

# -------------------------
# PACKS (public): universal goal runner + wrappers for top verbs
# -------------------------

Write-Json (Join-Path $bt "packs\pack.goal_runner.unstoppable.neutral.v1.json") @'
{
  "id": "pack.goal_runner.unstoppable.neutral.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 210 },
  "title": "Unstoppable Goal Runner (neutral)",
  "description": "Capability-aware goal execution: detects repo, maps intent to feasible steps, executes, and only asks questions when evidence is missing.",
  "tags": ["goal", "capabilities", "unstoppable", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": ["recipe.capabilities.goal_runner.neutral.v1"],
    "inputs": ["goalVerb", "ecosystemHint?", "profileHint?", "settingsPresetName?"]
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.goal.build_or_wait.neutral.v1.json") @'
{
  "id": "pack.goal.build_or_wait.neutral.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 170 },
  "title": "Goal: Build (capability-aware)",
  "description": "Builds a repo using capability detection. If build target ambiguous, asks minimal questions (workspace/project selection).",
  "tags": ["goal", "build", "capabilities"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["pack.goal_runner.unstoppable.neutral.v1"], "goalVerb": "build" }
}
'@

Write-Json (Join-Path $bt "packs\pack.goal.test_or_scaffold.neutral.v1.json") @'
{
  "id": "pack.goal.test_or_scaffold.neutral.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 170 },
  "title": "Goal: Test (capability-aware)",
  "description": "Runs tests when present; if missing, offers to scaffold tests or asks user to approve skipping.",
  "tags": ["goal", "test", "capabilities"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["pack.goal_runner.unstoppable.neutral.v1"], "goalVerb": "test" }
}
'@

Write-Json (Join-Path $bt "packs\pack.goal.run_or_wait.neutral.v1.json") @'
{
  "id": "pack.goal.run_or_wait.neutral.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 170 },
  "title": "Goal: Run (capability-aware)",
  "description": "Runs a detected entrypoint. If ambiguous (multiple apps/services), asks user to choose one.",
  "tags": ["goal", "run", "capabilities"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["pack.goal_runner.unstoppable.neutral.v1"], "goalVerb": "run" }
}
'@

Write-Json (Join-Path $bt "packs\pack.goal.package_publish.plan_only.neutral.v1.json") @'
{
  "id": "pack.goal.package_publish.plan_only.neutral.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 165 },
  "title": "Goal: Package/Publish (plan-only, capability-aware)",
  "description": "Builds manifest + gates and produces a neutral publish plan for approval. Avoids hardcoded registry/tooling.",
  "tags": ["goal", "publish", "capabilities", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["pack.package.publish.contract_first.neutral.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.goal.diagnose.unstoppable.neutral.v1.json") @'
{
  "id": "pack.goal.diagnose.unstoppable.neutral.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 180 },
  "title": "Goal: Diagnose (capability-aware)",
  "description": "Runs diagnosis flow with evidence collection and minimal fix loop. Uses repo capabilities to choose the right evidence points.",
  "tags": ["goal", "diagnose", "capabilities"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["pack.diagnose.unstoppable.v1"] }
}
'@

# -------------------------
# GRAPH (public): router for goal runner
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.goal_runner.unstoppable.v1.json") @'
{
  "id": "graph.router.goal_runner.unstoppable.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: Unstoppable goal runner",
  "description": "Routes user intents to the capability-aware goal runner or contract-first publish planning, with diagnose fallback.",
  "tags": ["router", "goal", "capabilities", "unstoppable"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  U[User intent] --> C{Intent type}\n  C -->|build/test/run/format| G[pack.goal_runner.unstoppable.neutral.v1]\n  C -->|package_publish| P[pack.goal.package_publish.plan_only.neutral.v1]\n  C -->|diagnose| D[pack.goal.diagnose.unstoppable.neutral.v1]\n  G -->|WAIT_USER| W[WAIT_USER]\n  P -->|WAIT_USER| W\n  W --> G\n  G --> Done[Done]\n  P --> Done\n  D --> Done\n  G -->|fail| DF[pack.diagnose.unstoppable.v1]\n  DF --> Done"
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 33 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

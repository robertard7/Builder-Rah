# BlueprintTemplates/Seed-Corpus-Batch34.ps1
# Batch34: Workspace & multi-project selection (neutral)
# Handles monorepos/workspaces/solutions with minimal WAIT_USER questions, then delegates to goal runner.
# No provider/toolchain hardcoding. No hardcoded paths. PS 5.1 compatible.

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
# ATOMS (internal): detect workspace, enumerate projects, wait_user, select target
# -------------------------

Write-Json (Join-Path $bt "atoms\task.workspace.detect_root_and_type.v1.json") @'
{
  "id": "task.workspace.detect_root_and_type.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 8 },
  "title": "Workspace: Detect root and type",
  "description": "Detects whether repo is a single-project repo or a workspace/monorepo (solutions, pnpm/yarn workspaces, cargo, go work, maven multi-module, etc.).",
  "tags": ["workspace", "detect", "monorepo", "solution"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "detect_workspace_root_and_type",
    "inputs": ["projectRoot"],
    "outputs": ["workspaceReport"],
    "reportShape": {
      "fields": [
        "workspaceKind",
        "workspaceRoot",
        "primaryMarkers[]",
        "candidateTargets[]",
        "notes[]",
        "ambiguities[]"
      ]
    },
    "rules": [
      "evidence-based detection via marker files only",
      "workspaceRoot must be a deterministic folder path",
      "do not guess the primary target if multiple exist"
    ],
    "workspaceKinds": [
      "single",
      "dotnet_solution",
      "node_workspace",
      "python_multi",
      "go_work",
      "cargo_workspace",
      "maven_multi_module",
      "cmake_superbuild",
      "unknown_multi"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.workspace.enumerate_targets.v1.json") @'
{
  "id": "task.workspace.enumerate_targets.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 8 },
  "title": "Workspace: Enumerate targets",
  "description": "Enumerates runnable/buildable/testable targets inside a workspace (projects, packages, modules) based on workspaceKind.",
  "tags": ["workspace", "enumerate", "targets"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "enumerate_workspace_targets",
    "inputs": ["workspaceReport", "goalVerb?"],
    "outputs": ["targetsReport"],
    "targetsShape": {
      "fields": [
        "targets[]",
        "defaultTarget?",
        "groupingHint?",
        "notes[]",
        "ambiguities[]"
      ]
    },
    "targetShape": {
      "fields": [
        "id",
        "displayName",
        "relativePath",
        "kind",
        "supportedVerbs[]",
        "evidence[]"
      ]
    },
    "rules": [
      "supportedVerbs must be inferred from evidence (eg, csproj with OutputType Exe supports run)",
      "if multiple viable targets exist, do not auto-pick unless deterministic and safe",
      "groupingHint can be used by UI (apps/libs/tests/services)"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.workspace.wait_user.select_target.v1.json") @'
{
  "id": "task.workspace.wait_user.select_target.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Workspace: WAIT_USER select target",
  "description": "When workspace has multiple viable targets for a goal, asks the user to select one with minimal friction.",
  "tags": ["workspace", "wait_user", "select"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "emit_wait_user_questions",
    "inputs": ["workspaceReport", "targetsReport", "goalVerb"],
    "outputs": ["questions", "waitUser"],
    "waitUser": true,
    "questionRules": [
      "ask for one selection: target id OR relative path OR displayed name",
      "if goalVerb=run, prefer listing only run-capable targets",
      "if goalVerb=test, prefer listing only test-capable targets",
      "do not ask for provider/model/toolchain ids",
      "do not ask for environment details unless required"
    ],
    "uiHints": {
      "control": "single_select",
      "optionsFrom": "targetsReport.targets",
      "valueFields": ["id", "relativePath", "displayName"]
    }
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.workspace.apply_target_selection.v1.json") @'
{
  "id": "task.workspace.apply_target_selection.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 8 },
  "title": "Workspace: Apply selected target",
  "description": "Applies a user-selected target to produce an effective projectRoot for downstream capability detection and execution.",
  "tags": ["workspace", "select", "apply"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "apply_workspace_target_selection",
    "inputs": ["workspaceReport", "targetsReport", "selectedTarget"],
    "outputs": ["effectiveProjectRoot", "targetContext"],
    "rules": [
      "effectiveProjectRoot must be workspaceRoot + target relativePath",
      "targetContext must include selection evidence and supportedVerbs"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.workspace.goal_entry.v1.json") @'
{
  "id": "task.workspace.goal_entry.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 9 },
  "title": "Workspace: Goal entry (select target -> goal runner)",
  "description": "Workspace-aware entrypoint: detect workspace, enumerate targets, ask user if needed, then delegate to capability-aware goal runner.",
  "tags": ["workspace", "goal", "router", "unstoppable"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "workspace_aware_goal_entry",
    "inputs": ["projectRoot", "goalVerb", "ecosystemHint?", "profileHint?", "settingsPresetName?"],
    "outputs": ["executionReceipts", "finalStatus", "waitUser?", "questions?"],
    "pipeline": [
      { "use": "task.workspace.detect_root_and_type.v1" },
      { "use": "task.workspace.enumerate_targets.v1" },
      { "decision": "if targetsReport.ambiguities not empty OR targetsReport.defaultTarget is null AND targetsReport.targets length > 1 then WAIT_USER else continue" },
      { "use": "task.workspace.apply_target_selection.v1" },
      { "delegateTo": "task.capabilities.goal_runner.v1", "inputsMap": { "projectRoot": "effectiveProjectRoot" } }
    ],
    "onWaitUser": { "use": "task.workspace.wait_user.select_target.v1" },
    "onFailure": { "routeTo": "pack.diagnose.unstoppable.v1" }
  }
}
'@

# -------------------------
# RECIPE (public): workspace-aware goal entry
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.workspace.goal_entry.neutral.v1.json") @'
{
  "id": "recipe.workspace.goal_entry.neutral.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 88 },
  "title": "Goal entry: Workspace-aware (neutral)",
  "description": "Detects workspace/monorepo, asks for target selection if needed, then runs the capability-aware goal runner.",
  "tags": ["workspace", "goal", "neutral", "unstoppable"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.workspace.goal_entry.v1" }
  ] }
}
'@

# -------------------------
# PACK (public): workspace-aware unstoppable goal runner
# -------------------------

Write-Json (Join-Path $bt "packs\pack.workspace.goal_runner.unstoppable.neutral.v1.json") @'
{
  "id": "pack.workspace.goal_runner.unstoppable.neutral.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 220 },
  "title": "Unstoppable Goal Runner (workspace-aware, neutral)",
  "description": "Handles monorepos/workspaces: selects the correct target, then runs capability-aware goal execution with minimal WAIT_USER prompts.",
  "tags": ["workspace", "goal", "capabilities", "unstoppable", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": ["recipe.workspace.goal_entry.neutral.v1"],
    "inputs": ["goalVerb", "ecosystemHint?", "profileHint?", "settingsPresetName?"]
  }
}
'@

# -------------------------
# GRAPH (public): router upgrade that prefers workspace-aware pack
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.goal_runner.workspace_aware.v1.json") @'
{
  "id": "graph.router.goal_runner.workspace_aware.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: Workspace-aware unstoppable goal runner",
  "description": "Routes user intents to a workspace-aware goal runner first. Falls back to diagnose and contract-first publishing.",
  "tags": ["router", "goal", "workspace", "unstoppable"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  U[User intent] --> C{Intent type}\n  C -->|build/test/run/format| WG[pack.workspace.goal_runner.unstoppable.neutral.v1]\n  C -->|package_publish| P[pack.goal.package_publish.plan_only.neutral.v1]\n  C -->|diagnose| D[pack.goal.diagnose.unstoppable.neutral.v1]\n  WG -->|WAIT_USER select target| W[WAIT_USER]\n  W --> WG\n  WG --> Done[Done]\n  P --> Done\n  D --> Done\n  WG -->|fail| DF[pack.diagnose.unstoppable.v1]\n  DF --> Done"
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 34 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

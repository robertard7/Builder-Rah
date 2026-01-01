# Seed-Corpus-Batch39.ps1
# Env-aware capability constraints + WAIT_USER when runtime cannot satisfy capabilities.
# Compatible with Windows PowerShell 5.1 (no ternary operator).

$ErrorActionPreference = "Stop"

function Ensure-Dir([string]$p) {
  if (-not (Test-Path -LiteralPath $p)) {
    New-Item -ItemType Directory -Path $p | Out-Null
  }
}

function Write-Utf8([string]$path, [string]$content) {
  $dir = Split-Path -Parent $path
  Ensure-Dir $dir
  Set-Content -LiteralPath $path -Value $content -Encoding utf8
  Write-Host ("Wrote: " + $path)
}

# Resolve BlueprintTemplates root robustly.
$base = Split-Path -Parent $MyInvocation.MyCommand.Path
$hereName = Split-Path -Leaf $base
if ($hereName -ieq "BlueprintTemplates") {
  $bt = $base
} else {
  $bt = Join-Path $base "BlueprintTemplates"
}

if (-not (Test-Path -LiteralPath $bt)) {
  throw "BlueprintTemplates root not found: $bt"
}

$atoms = Join-Path $bt "atoms"
$recipes = Join-Path $bt "recipes"
$packs = Join-Path $bt "packs"
$graphs = Join-Path $bt "graphs"

Ensure-Dir $atoms
Ensure-Dir $recipes
Ensure-Dir $packs
Ensure-Dir $graphs

# ----------------------------
# ATOMS (internal)
# ----------------------------

Write-Utf8 (Join-Path $atoms "task.env.detect_runtime_and_constraints.v1.json") @'
{
  "id": "task.env.detect_runtime_and_constraints.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Environment: detect runtime + constraints",
  "description": "Detects execution environment type and high-level constraints (host/container/wsl/remote) without binding to any provider, model, or toolchain.",
  "tags": ["env", "detect", "constraints", "internal"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "detect_runtime",
    "inputs": ["planContext", "settingsSnapshot?"],
    "outputs": ["runtimeProfile", "constraints"],
    "rules": [
      "do not infer hardcoded paths",
      "do not reference specific providers/models/tools",
      "prefer explicit user settings when present",
      "emit constraints as abstract capabilities (filesystem, process, network, container, etc.)"
    ],
    "runtimeProfileSchema": {
      "name": "string",
      "type": "host_windows|host_linux|container_linux|wsl|remote",
      "notes": "string?"
    },
    "constraintsSchema": {
      "allowedCapabilities": ["string"],
      "deniedCapabilities": ["string"],
      "notes": "string?"
    }
  }
}
'@

Write-Utf8 (Join-Path $atoms "task.tools.apply_env_constraints_to_capabilities.v1.json") @'
{
  "id": "task.tools.apply_env_constraints_to_capabilities.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Tools: apply environment constraints to capability refs",
  "description": "Filters/rewrites capabilityRefs based on runtime constraints. If required capabilities are not possible, produces WAIT_USER questions.",
  "tags": ["tools", "capabilities", "env", "gate", "internal"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "apply_capability_constraints",
    "inputs": ["capabilityRefs", "runtimeProfile", "constraints", "goalIntent?"],
    "outputs": ["capabilityRefsConstrained", "blockedCapabilities", "waitUser", "questions"],
    "rules": [
      "never invent tool ids",
      "do not hardcode provider/model/toolchain ids",
      "prefer capability refs; keep explicit tool refs unchanged but validate coverage later",
      "if blockedCapabilities is non-empty and blocks core verbs, set waitUser=true and emit questions"
    ],
    "questionRules": [
      "ask only what is needed to choose an execution environment or adjust the goal",
      "prefer yes/no or pick-one questions",
      "never ask for provider/model/toolchain ids"
    ]
  }
}
'@

Write-Utf8 (Join-Path $atoms "task.tools.wait_user.select_execution_environment.v1.json") @'
{
  "id": "task.tools.wait_user.select_execution_environment.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Tools: WAIT_USER select execution environment",
  "description": "When capability constraints block progress, asks the user to choose/confirm a runtime environment (host/container/wsl/remote) and scope.",
  "tags": ["tools", "env", "wait_user", "internal"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "emit_wait_user_questions",
    "inputs": ["blockedCapabilities", "runtimeProfile", "projectRootHint?"],
    "outputs": ["questions", "waitUser"],
    "waitUser": true,
    "questions": [
      {
        "id": "env.choice",
        "type": "pick_one",
        "title": "Where should this run?",
        "options": ["host", "container", "wsl", "remote"],
        "notes": "Pick the environment that has the needed tools/capabilities."
      },
      {
        "id": "env.scope",
        "type": "pick_one",
        "title": "Scope of execution",
        "options": ["repo_only", "repo_and_submodules", "workspace_multi_project"],
        "notes": "Choose the scope so tooling discovery and build targets are accurate."
      }
    ]
  }
}
'@

Write-Utf8 (Join-Path $atoms "task.tools.map_env_choice_to_execution_target.v1.json") @'
{
  "id": "task.tools.map_env_choice_to_execution_target.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Tools: map env choice to execution target",
  "description": "Maps a user-selected env choice into an abstract executionTarget and updates planContext. No hardcoded tool ids or commands.",
  "tags": ["tools", "env", "execution_target", "internal"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "select_execution_target",
    "inputs": ["userSelection", "settingsSnapshot?"],
    "outputs": ["executionTarget", "planContextDelta"],
    "executionTargetSchema": {
      "type": "host|container|wsl|remote",
      "notes": "string?",
      "hints": {
        "containerProfile": "string?",
        "remoteProfile": "string?"
      }
    },
    "rules": [
      "only reference Settings by name (eg rolePreset), never provider/model ids",
      "do not assume container names, paths, or repo roots",
      "executionTarget is descriptive; actual command wiring is handled elsewhere"
    ]
  }
}
'@

Write-Utf8 (Join-Path $atoms "task.tools.env_aware_preflight_bundle.v1.json") @'
{
  "id": "task.tools.env_aware_preflight_bundle.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Tools: env-aware preflight bundle",
  "description": "High-level atom that expresses the preflight sequence: detect env -> constrain capabilities -> WAIT_USER if blocked -> apply selection.",
  "tags": ["tools", "env", "preflight", "bundle", "internal"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "composite",
    "steps": [
      { "use": "task.env.detect_runtime_and_constraints.v1" },
      { "use": "task.tools.apply_env_constraints_to_capabilities.v1" },
      {
        "when": "waitUser == true",
        "use": "task.tools.wait_user.select_execution_environment.v1"
      },
      {
        "when": "userSelection exists",
        "use": "task.tools.map_env_choice_to_execution_target.v1"
      }
    ],
    "outputs": ["executionTarget", "capabilityRefsConstrained", "runtimeProfile", "constraints"]
  }
}
'@

# ----------------------------
# RECIPES
# ----------------------------

Write-Utf8 (Join-Path $recipes "recipe.tools.env_constraints.preflight.v1.json") @'
{
  "id": "recipe.tools.env_constraints.preflight.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 60 },
  "title": "Tools preflight: environment constraints (env-aware)",
  "description": "Applies environment-aware constraints to capability refs and can ask the user to select an execution environment if needed.",
  "tags": ["tools", "preflight", "env", "capabilities"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "use": [
      "task.tools.env_aware_preflight_bundle.v1"
    ]
  }
}
'@

# ----------------------------
# PACKS (public)
# ----------------------------

Write-Utf8 (Join-Path $packs "pack.tools.env_constraints.preflight.v1.json") @'
{
  "id": "pack.tools.env_constraints.preflight.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 120 },
  "title": "Preflight: environment-aware capability constraints",
  "description": "Runs an env-aware preflight that constrains capability refs and forces WAIT_USER if the current runtime cannot satisfy the plan.",
  "tags": ["tools", "env", "preflight", "gate"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "entry": "recipe.tools.env_constraints.preflight.v1",
    "defaults": {
      "rolePreset": "fast"
    },
    "notes": [
      "No hardcoded providers/models/toolchain ids.",
      "No hardcoded paths.",
      "This pack only shapes plan context and gating."
    ]
  }
}
'@

Write-Utf8 (Join-Path $packs "pack.goal_runner.unstoppable.env_aware.v1.json") @'
{
  "id": "pack.goal_runner.unstoppable.env_aware.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 125 },
  "title": "Goal Runner: unstoppable (env-aware preflight)",
  "description": "Goal runner that performs environment-aware capability preflight before tool prompt gates and execution.",
  "tags": ["goal_runner", "unstoppable", "env", "preflight"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "flow": [
      { "use": "pack.tools.capabilities_preset_preflight.v1" },
      { "use": "pack.tools.env_constraints.preflight.v1" },
      { "use": "pack.tools.prompt_gate.capabilities_aware.v1" },
      { "use": "pack.goal_runner.unstoppable.capabilities_aware.v1" }
    ],
    "defaults": {
      "rolePreset": "fast"
    },
    "variables": {
      "projectRoot": "${projectRoot}",
      "workspaceRoot": "${workspaceRoot?}",
      "goal": "${goal}"
    }
  }
}
'@

# ----------------------------
# GRAPHS (public)
# ----------------------------

Write-Utf8 (Join-Path $graphs "graph.router.tools.env_constraints.v1.json") @'
{
  "id": "graph.router.tools.env_constraints.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public", "priority": 80 },
  "title": "Router: tools env constraints",
  "description": "Routes tool-planning through environment detection + capability constraint gate + WAIT_USER selection when blocked.",
  "tags": ["graph", "router", "tools", "env"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  A[Goal/PlanContext] --> B[Detect runtime + constraints]\n  B --> C[Constrain capabilityRefs]\n  C -->|blocked| D[WAIT_USER: select execution environment]\n  D --> E[Map env choice -> executionTarget]\n  C -->|ok| F[Continue]\n  E --> F\n  F --> G[Next: prompt gate / execution]\n"
  }
}
'@

Write-Utf8 (Join-Path $graphs "graph.orchestrator.env_aware_preflight.v1.json") @'
{
  "id": "graph.orchestrator.env_aware_preflight.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public", "priority": 80 },
  "title": "Orchestrator: env-aware preflight (pack chain)",
  "description": "Orchestrator graph that runs capability presets -> env constraints -> prompt gates -> goal execution, with WAIT_USER on env mismatch.",
  "tags": ["graph", "orchestrator", "env", "preflight"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  S[Start] --> P1[pack.tools.capabilities_preset_preflight.v1]\n  P1 --> P2[pack.tools.env_constraints.preflight.v1]\n  P2 -->|wait_user| W[WAIT_USER]\n  W --> P2\n  P2 --> P3[pack.tools.prompt_gate.capabilities_aware.v1]\n  P3 --> P4[pack.goal_runner.unstoppable.capabilities_aware.v1]\n  P4 --> E[End]\n"
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 39 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

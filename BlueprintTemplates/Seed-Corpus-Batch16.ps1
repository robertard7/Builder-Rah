# BlueprintTemplates/Seed-Corpus-Batch16.ps1
# Batch16: Start Project Unstoppable Wizard packs + router/orchestrator graphs
# No provider/toolchain hardcoding. No raw shell commands. No hardcoded paths. PS 5.1 compatible.

$ErrorActionPreference = "Stop"

function Get-BaseDir {
  if ($PSCommandPath -and (Test-Path -LiteralPath $PSCommandPath)) { return Split-Path -Parent $PSCommandPath }
  if ($PSScriptRoot) { return $PSScriptRoot }
  return (Get-Location).Path
}

function Ensure-Dir([string]$p) {
  if (-not (Test-Path -LiteralPath $p)) { New-Item -ItemType Directory -Path $p | Out-Null }
}

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
# ATOMS (internal): wizard prompts + validate bundle
# -------------------------

Write-Json (Join-Path $bt "atoms\task.project.start.prompt_select.v1.json") @'
{
  "id": "task.project.start.prompt_select.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Start Project: Prompt select ecosystem/profile",
  "description": "Collects user intent (ecosystem/profile/name/folder) with safe defaults. If missing, WAIT_USER.",
  "tags": ["start_project", "wizard", "prompt"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "prompt_select_project_profile",
    "inputs": ["userIntent?", "defaults?"],
    "outputs": ["selection"],
    "selectionSchema": {
      "ecosystem": "dotnet|node|python|go|rust|java|cpp",
      "profile": "cli|web|api|library|gui",
      "projectName": "string",
      "folderName": "string",
      "projectRoot": "string",
      "options": "object"
    },
    "waitUserIfMissing": true
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.project.start.create_from_selection.v1.json") @'
{
  "id": "task.project.start.create_from_selection.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Start Project: Create project from selection",
  "description": "Routes to the right create_project atom for ecosystem/profile. No commands.",
  "tags": ["start_project", "create_project"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "create_project_from_selection",
    "inputs": ["selection"],
    "routes": {
      "dotnet.cli": "task.create_project.dotnet.cli.v1",
      "dotnet.gui": "task.create_project.dotnet.winforms.v1",
      "dotnet.library": "task.create_project.dotnet.library.v1",
      "node.cli": "task.create_project.node.cli.v1",
      "node.web": "task.create_project.node.web.v1",
      "python.cli": "task.create_project.python.cli.v1",
      "python.api": "task.create_project.python.api.v1",
      "go.cli": "task.create_project.go.cli.v1",
      "rust.cli": "task.create_project.rust.cli.v1",
      "java.cli": "task.create_project.java.cli.v1",
      "cpp.cmake": "task.create_project.cpp.cmake.v1"
    },
    "fallback": "task.create_project.generic.v1"
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.project.start.baseline_validate.v1.json") @'
{
  "id": "task.project.start.baseline_validate.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Start Project: Baseline validate (build/test/run/format)",
  "description": "Runs the core validate flow abstractly using existing task atoms (build/test/run/format).",
  "tags": ["start_project", "validate"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "baseline_validate",
    "inputs": ["selection"],
    "uses": [
      "task.build.${selection.ecosystem}.v1",
      "task.test.${selection.ecosystem}.v1",
      "task.run.${selection.ecosystem}.v1",
      "task.format_lint.${selection.ecosystem}.v1"
    ],
    "note": "Tasks may be missing for some ecosystems; orchestrator should skip missing tasks safely."
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.project.start.scaffold_docs_and_hygiene.v1.json") @'
{
  "id": "task.project.start.scaffold_docs_and_hygiene.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Start Project: Scaffold docs + repo hygiene",
  "description": "Applies baseline docs and hygiene packs (public templates).",
  "tags": ["start_project", "docs", "hygiene"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "apply_packs",
    "inputs": ["selection"],
    "packs": [
      "pack.docs.baseline.v1",
      "pack.repo.hygiene.baseline.v1"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.project.start.scaffold_tests_if_missing.v1.json") @'
{
  "id": "task.project.start.scaffold_tests_if_missing.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "Start Project: Scaffold tests if missing",
  "description": "Ensures a basic smoke test suite exists using existing test scaffolding atom.",
  "tags": ["start_project", "tests"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "tests_scaffold_if_missing",
    "inputs": ["selection"],
    "uses": ["task.tests.scaffold_if_missing.v1"]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.project.start.optional_containerize.v1.json") @'
{
  "id": "task.project.start.optional_containerize.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 4 },
  "title": "Start Project: Optional container/devcontainer",
  "description": "If user requests containerization, applies environment packs. Otherwise no-op.",
  "tags": ["start_project", "env", "container"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "conditional_apply_packs",
    "inputs": ["selection"],
    "when": { "anyOf": ["selection.options.containerize == true", "selection.options.devcontainer == true"] },
    "packs": [
      "pack.env.containerize.project.v1",
      "pack.env.devcontainer.only.v1"
    ]
  }
}
'@

# -------------------------
# RECIPES: start project composite (public)
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.project.start.unstoppable.v1.json") @'
{
  "id": "recipe.project.start.unstoppable.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 85 },
  "title": "Start Project: Unstoppable wizard recipe",
  "description": "Selection -> create -> hygiene/docs -> tests -> optional env -> validate -> diagnose on failure.",
  "tags": ["start_project", "wizard", "unstoppable"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.project.start.prompt_select.v1" },
      { "use": "task.project.start.create_from_selection.v1" },
      { "use": "task.project.start.scaffold_docs_and_hygiene.v1" },
      { "use": "task.project.start.scaffold_tests_if_missing.v1" },
      { "use": "task.project.start.optional_containerize.v1" },
      { "use": "task.project.start.baseline_validate.v1" },
      { "use": "pack.diagnose.unstoppable.v1" }
    ]
  }
}
'@

# -------------------------
# PACKS (public): start project unstoppable
# -------------------------

Write-Json (Join-Path $bt "packs\pack.project.start.unstoppable.v1.json") @'
{
  "id": "pack.project.start.unstoppable.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 210 },
  "title": "Start Project: Unstoppable Wizard",
  "description": "Starts a new project for popular ecosystems, applies docs + hygiene, ensures tests, optional containerization, validates, then runs unstoppable diagnose on failure.",
  "tags": ["start_project", "wizard", "multi_ecosystem"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [ "recipe.project.start.unstoppable.v1" ],
    "defaults": {
      "ecosystem": "dotnet",
      "profile": "cli",
      "rolePreset": "fast"
    }
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.project.start.unstoppable.dotnet_first.v1.json") @'
{
  "id": "pack.project.start.unstoppable.dotnet_first.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 205 },
  "title": "Start Project: Unstoppable (.NET-first defaults)",
  "description": "Same unstoppable wizard but defaults tuned for .NET projects first.",
  "tags": ["start_project", "wizard", "dotnet"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [ "recipe.project.start.unstoppable.v1" ],
    "defaults": {
      "ecosystem": "dotnet",
      "profile": "cli",
      "rolePreset": "fast"
    }
  }
}
'@

# -------------------------
# GRAPHS (public): start wizard router + orchestrator
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.project_start.unstoppable.v1.json") @'
{
  "id": "graph.router.project_start.unstoppable.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: Project Start (Unstoppable Wizard)",
  "description": "Routes 'start project' intents to the unstoppable wizard pack.",
  "tags": ["router", "start_project", "wizard"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Input[start project] --> Wizard[pack.project.start.unstoppable.v1]\n  Input -->|dotnet first| Dot[pack.project.start.unstoppable.dotnet_first.v1]\n  Input -->|unknown| Wizard"
  }
}
'@

Write-Json (Join-Path $bt "graphs\graph.orchestrator.project_start.unstoppable.v1.json") @'
{
  "id": "graph.orchestrator.project_start.unstoppable.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Orchestrator: Project Start Unstoppable",
  "description": "Orchestrator flow: prompt select -> create -> hygiene/docs -> tests -> optional env -> validate -> diagnose -> WAIT_USER if needed.",
  "tags": ["orchestrator", "start_project", "wizard"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Start[Start] --> Sel[task.project.start.prompt_select.v1]\n  Sel -->|WAIT_USER| Wait[WAIT_USER]\n  Sel --> Create[task.project.start.create_from_selection.v1]\n  Create --> Hyg[task.project.start.scaffold_docs_and_hygiene.v1]\n  Hyg --> Tests[task.project.start.scaffold_tests_if_missing.v1]\n  Tests --> Env[task.project.start.optional_containerize.v1]\n  Env --> Val[task.project.start.baseline_validate.v1]\n  Val --> Ok{green?}\n  Ok -->|yes| Done[Done]\n  Ok -->|no| Diag[pack.diagnose.unstoppable.v1]\n  Diag --> End{resolved?}\n  End -->|yes| Done\n  End -->|WAIT_USER| Wait"
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 16 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

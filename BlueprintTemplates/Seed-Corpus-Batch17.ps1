# BlueprintTemplates/Seed-Corpus-Batch17.ps1
# Batch17: Ecosystem completion sweep + capabilities matrix + router
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
# Shared generic atoms (internal) for missing verbs
# -------------------------

Write-Json (Join-Path $bt "atoms\task.create_project.generic.v1.json") @'
{
  "id": "task.create_project.generic.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "Create project (generic)",
  "description": "Abstract project creation. Orchestrator/tooling resolves actual project scaffolding.",
  "tags": ["create_project", "generic"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "create_project",
    "inputs": ["projectRoot", "projectName", "ecosystem?", "profile?"],
    "outputs": ["projectRoot", "summary"]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.add_file.generic.v1.json") @'
{
  "id": "task.add_file.generic.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 4 },
  "title": "Add file (generic)",
  "description": "Adds a file at a relative path under projectRoot with provided content.",
  "tags": ["add_file", "generic"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "add_file",
    "inputs": ["projectRoot", "relativePath", "textContent", "encoding?"],
    "outputs": ["writtenPath"]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.replace_file.generic.v1.json") @'
{
  "id": "task.replace_file.generic.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 4 },
  "title": "Replace file (generic)",
  "description": "Replaces a file at a relative path under projectRoot with provided content.",
  "tags": ["replace_file", "generic"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "replace_file",
    "inputs": ["projectRoot", "relativePath", "textContent", "encoding?"],
    "outputs": ["writtenPath"]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.apply_patch.generic.v1.json") @'
{
  "id": "task.apply_patch.generic.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 4 },
  "title": "Apply patch (generic)",
  "description": "Applies a unified diff patch to files under projectRoot.",
  "tags": ["apply_patch", "generic"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "apply_patch",
    "inputs": ["projectRoot", "patchText", "strict?"],
    "outputs": ["applied", "filesTouched", "rejects?"],
    "defaults": { "strict": true }
  }
}
'@

# -------------------------
# Ecosystem-specific atoms for missing core verbs (internal, abstract)
# Note: Build/test/run/format/publish atoms likely already exist; we add missing ones: run for node/python, build/test for dotnet if absent, etc.
# -------------------------

Write-Json (Join-Path $bt "atoms\task.run.node.v1.json") @'
{
  "id": "task.run.node.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 4 },
  "title": "Run (node)",
  "description": "Runs a Node project using its configured entrypoint (package scripts). No commands here.",
  "tags": ["run", "node"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "run_project", "inputs": ["projectRoot", "profile?"], "outputs": ["runResult"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.run.python.v1.json") @'
{
  "id": "task.run.python.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 4 },
  "title": "Run (python)",
  "description": "Runs a Python project using its configured entrypoint (module/app). No commands here.",
  "tags": ["run", "python"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "run_project", "inputs": ["projectRoot", "profile?"], "outputs": ["runResult"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.create_project.dotnet.cli.v1.json") @'
{
  "id": "task.create_project.dotnet.cli.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Create project (.NET CLI)",
  "description": "Creates a .NET console project. Orchestrator resolves actual scaffolding.",
  "tags": ["create_project", "dotnet", "cli"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "create_project", "inputs": ["projectRoot", "projectName"], "outputs": ["solutionPath?", "projectPath?"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.create_project.dotnet.winforms.v1.json") @'
{
  "id": "task.create_project.dotnet.winforms.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Create project (.NET WinForms GUI)",
  "description": "Creates a WinForms app project. Orchestrator resolves actual scaffolding.",
  "tags": ["create_project", "dotnet", "gui", "winforms"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "create_project", "inputs": ["projectRoot", "projectName"], "outputs": ["solutionPath?", "projectPath?"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.create_project.dotnet.library.v1.json") @'
{
  "id": "task.create_project.dotnet.library.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Create project (.NET Library)",
  "description": "Creates a .NET class library project. Orchestrator resolves actual scaffolding.",
  "tags": ["create_project", "dotnet", "library"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "create_project", "inputs": ["projectRoot", "projectName"], "outputs": ["solutionPath?", "projectPath?"] }
}
'@

# For ecosystems where create_project atoms already exist, no harm: deterministic ids overwrite with consistent content.
# -------------------------
# Capabilities matrix (public pack) + recipe
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.capabilities.matrix.v1.json") @'
{
  "id": "recipe.capabilities.matrix.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 70 },
  "title": "Capabilities: Ecosystem verb coverage matrix",
  "description": "A declarative matrix of which core verbs are supported per ecosystem/profile.",
  "tags": ["capabilities", "matrix", "ecosystem"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "matrix": {
      "dotnet": {
        "profiles": ["cli", "gui(winforms)", "library"],
        "verbs": ["create_project", "add_file", "replace_file", "apply_patch", "build", "test", "run", "format_lint", "package_publish", "diagnose_build_fail_triage"]
      },
      "node": {
        "profiles": ["cli", "web"],
        "verbs": ["create_project", "add_file", "replace_file", "apply_patch", "build", "test", "run", "format_lint", "package_publish", "diagnose_build_fail_triage"]
      },
      "python": {
        "profiles": ["cli", "api"],
        "verbs": ["create_project", "add_file", "replace_file", "apply_patch", "build", "test", "run", "format_lint", "package_publish", "diagnose_build_fail_triage"]
      },
      "go": {
        "profiles": ["cli"],
        "verbs": ["create_project", "add_file", "replace_file", "apply_patch", "build", "test", "run", "format_lint", "package_publish", "diagnose_build_fail_triage"]
      },
      "rust": {
        "profiles": ["cli"],
        "verbs": ["create_project", "add_file", "replace_file", "apply_patch", "build", "test", "run", "format_lint", "package_publish", "diagnose_build_fail_triage"]
      },
      "java": {
        "profiles": ["cli(maven)"],
        "verbs": ["create_project", "add_file", "replace_file", "apply_patch", "build", "test", "run", "format_lint", "package_publish", "diagnose_build_fail_triage"]
      },
      "cpp": {
        "profiles": ["cmake"],
        "verbs": ["create_project", "add_file", "replace_file", "apply_patch", "build", "test", "run", "format_lint", "package_publish", "diagnose_build_fail_triage"]
      }
    }
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.capabilities.matrix.v1.json") @'
{
  "id": "pack.capabilities.matrix.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 150 },
  "title": "Capabilities Matrix (What RAH can do by ecosystem)",
  "description": "Public matrix describing verb coverage so routing and UI can avoid guessing.",
  "tags": ["capabilities", "matrix", "routing"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.capabilities.matrix.v1"] }
}
'@

# -------------------------
# Router graph that uses capabilities to pick flows (public)
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.capabilities_aware.v1.json") @'
{
  "id": "graph.router.capabilities_aware.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: Capabilities-aware routing",
  "description": "Routes requests based on ecosystem/profile capabilities and falls back to generic atoms + WAIT_USER when needed.",
  "tags": ["router", "capabilities", "fallback"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Req[Request] --> Cap[pack.capabilities.matrix.v1]\n  Cap --> Decide{Supported?}\n  Decide -->|yes| Do[Execute verb via task.<verb>.<ecosystem>...]\n  Decide -->|partial| Gen[Use generic atoms + ask clarifying]\n  Gen --> Wait[WAIT_USER]\n  Decide -->|no| Wait"
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 17 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

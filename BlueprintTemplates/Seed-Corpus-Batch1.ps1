# BlueprintTemplates/Seed-Corpus-Batch1.ps1
# Seed BlueprintTemplates corpus (packs/atoms/recipes/graphs)
# Safe when executed as a file OR pasted into a shell.

$ErrorActionPreference = "Stop"

function Get-BaseDir {
  # If running from a .ps1 file, PSScriptRoot is set.
  if ($PSCommandPath -and (Test-Path -LiteralPath $PSCommandPath)) {
    return Split-Path -Parent $PSCommandPath
  }
  if ($PSScriptRoot) { return $PSScriptRoot }

  # Fallback for pasted/interactive execution.
  return (Get-Location).Path
}

function Ensure-Dir([string]$p) {
  if (-not (Test-Path -LiteralPath $p)) {
    New-Item -ItemType Directory -Path $p | Out-Null
  }
}

function Write-Json([string]$path, [string]$json) {
  $dir = Split-Path -Parent $path
  Ensure-Dir $dir
  $json2 = $json.Replace("`r`n","`n").Replace("`r","`n")
  Set-Content -LiteralPath $path -Value $json2 -Encoding UTF8
  Write-Host "Wrote: $path"
}

# Determine output root. If executed from repo root, we write into ./BlueprintTemplates.
# If executed from within BlueprintTemplates, we write into ./ (current folder).
$base = Get-BaseDir
$hereName = Split-Path -Leaf $base

if ($hereName -ieq "BlueprintTemplates") {
  $bt = $base
} else {
  $bt = Join-Path $base "BlueprintTemplates"
}

# Ensure core folders
Ensure-Dir (Join-Path $bt "packs")
Ensure-Dir (Join-Path $bt "atoms")
Ensure-Dir (Join-Path $bt "recipes")
Ensure-Dir (Join-Path $bt "graphs")

# -------------------------
# PACKS (public)
# -------------------------
Write-Json (Join-Path $bt "packs\pack.project.start.v1.json") @'
{
  "id": "pack.project.start.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 120 },
  "title": "Start New Project",
  "description": "Guided project initialization across supported ecosystems.",
  "tags": ["project", "start", "init"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.create_project.${ecosystem}.${profile}.v1",
      "task.add_file.generic.v1",
      "task.build.${ecosystem}.v1",
      "task.run.${ecosystem}.v1"
    ]
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.project.desktop.forms.v1.json") @'
{
  "id": "pack.project.desktop.forms.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 130 },
  "title": ".NET WinForms Desktop App",
  "description": "Create a Windows desktop application using WinForms.",
  "tags": ["dotnet", "winforms", "desktop"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "ecosystem": "dotnet",
    "profile": "winforms",
    "uses": [
      "task.create_project.dotnet.winforms.v1",
      "task.build.dotnet.v1",
      "task.run.dotnet.v1"
    ]
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.project.web.app.v1.json") @'
{
  "id": "pack.project.web.app.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 125 },
  "title": "Node.js Web Application",
  "description": "Bootstrap a basic Node.js web application.",
  "tags": ["node", "web"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "ecosystem": "node",
    "profile": "web",
    "uses": [
      "task.create_project.node.web.v1",
      "task.run.node.v1"
    ]
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.project.api.service.v1.json") @'
{
  "id": "pack.project.api.service.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 125 },
  "title": "Python API Service",
  "description": "Create a lightweight Python API service.",
  "tags": ["python", "api"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "ecosystem": "python",
    "profile": "api",
    "uses": [
      "task.create_project.python.api.v1",
      "task.run.python.v1"
    ]
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.diagnose.buildfail.v1.json") @'
{
  "id": "pack.diagnose.buildfail.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 140 },
  "title": "Diagnose Build Failure",
  "description": "General-purpose build failure triage and diagnosis.",
  "tags": ["diagnose", "build", "triage"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.diagnose_build_fail_triage.generic.v1"
    ]
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.project.cli.tool.v1.json") @'
{
  "id": "pack.project.cli.tool.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 135 },
  "title": "CLI Tool Project",
  "description": "Guided creation of a CLI tool across common ecosystems.",
  "tags": ["project", "cli", "tool"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.create_project.${ecosystem}.cli.v1",
      "task.build.${ecosystem}.v1",
      "task.test.${ecosystem}.v1",
      "task.run.${ecosystem}.v1"
    ]
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.project.library.v1.json") @'
{
  "id": "pack.project.library.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 130 },
  "title": "Library Project",
  "description": "Create a reusable library package in supported ecosystems.",
  "tags": ["project", "library"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.create_project.${ecosystem}.library.v1",
      "task.build.${ecosystem}.v1",
      "task.test.${ecosystem}.v1",
      "task.package_publish.${ecosystem}.v1"
    ]
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.patch.apply.v1.json") @'
{
  "id": "pack.patch.apply.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 150 },
  "title": "Apply Patch Safely",
  "description": "Safely apply patch changes with verification and minimal damage.",
  "tags": ["patch", "apply", "safe"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.apply_patch.generic.v1",
      "task.build.${ecosystem}.v1",
      "task.test.${ecosystem}.v1"
    ]
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.add.feature.v1.json") @'
{
  "id": "pack.add.feature.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 140 },
  "title": "Add Feature",
  "description": "Guided feature addition: plan, modify files, patch, validate.",
  "tags": ["feature", "add", "guided"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.add_file.generic.v1",
      "task.replace_file.generic.v1",
      "task.apply_patch.generic.v1",
      "task.build.${ecosystem}.v1",
      "task.test.${ecosystem}.v1"
    ]
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.format.lint.v1.json") @'
{
  "id": "pack.format.lint.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 125 },
  "title": "Format and Lint",
  "description": "Run formatting and linting workflow for the current project ecosystem.",
  "tags": ["format", "lint", "quality"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.format_lint.${ecosystem}.v1",
      "task.build.${ecosystem}.v1",
      "task.test.${ecosystem}.v1"
    ]
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.package.publish.v1.json") @'
{
  "id": "pack.package.publish.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 120 },
  "title": "Package and Publish",
  "description": "Package and publish flow (requires credentials configured elsewhere).",
  "tags": ["package", "publish", "release"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.build.${ecosystem}.v1",
      "task.test.${ecosystem}.v1",
      "task.package_publish.${ecosystem}.v1"
    ]
  }
}
'@

# -------------------------
# GRAPHS (public)
# -------------------------
Write-Json (Join-Path $bt "graphs\graph.router.project_start.v1.json") @'
{
  "id": "graph.router.project_start.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Project Start Router",
  "description": "Routes start-project requests into ecosystem-specific packs.",
  "tags": ["router", "project"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Start --> ChooseEcosystem\n  ChooseEcosystem --> DotNet\n  ChooseEcosystem --> Node\n  ChooseEcosystem --> Python\n  DotNet --> PackDotNet\n  Node --> PackNode\n  Python --> PackPython"
  }
}
'@

Write-Json (Join-Path $bt "graphs\graph.orchestrator.pack_expand_wait_user.v1.json") @'
{
  "id": "graph.orchestrator.pack_expand_wait_user.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Pack Expand With User Gate",
  "description": "Expands a pack, waits for user approval, then executes.",
  "tags": ["orchestrator", "approval"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Expand --> WAIT_USER\n  WAIT_USER -->|approved| Execute\n  WAIT_USER -->|edit| Expand"
  }
}
'@

Write-Json (Join-Path $bt "graphs\graph.router.diagnose.v1.json") @'
{
  "id": "graph.router.diagnose.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Diagnose Router",
  "description": "Routes build/test/run failures into triage and repair flows.",
  "tags": ["router", "diagnose", "triage"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  R[router] --> IsFail{gate: cmd_run_tests}\n  IsFail -->|yes| T[route: RunTests]\n  IsFail -->|no| Next{gate: is_taskboard}\n  Next -->|yes| B[route: TaskBoardIngest]\n  Next -->|no| D[route: BrainstormPlanRequest]"
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 1 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

# BlueprintTemplates/Seed-Corpus-Batch3.ps1
# Batch3: Complete verb coverage for dotnet/cpp + add triage graphs + add wizard packs.
# Windows PowerShell 5.1 compatible. No provider/toolchain IDs. No hardcoded paths.

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
Ensure-Dir (Join-Path $bt "recipes")
Ensure-Dir (Join-Path $bt "graphs")

# -------------------------
# ATOMS: dotnet verb completion
# -------------------------

Write-Json (Join-Path $bt "atoms\task.format_lint.dotnet.v1.json") @'
{
  "id": "task.format_lint.dotnet.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 12 },
  "title": "Format/Lint .NET",
  "description": "Format and lint .NET projects using configured tooling/settings.",
  "tags": ["dotnet", "format", "lint"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "format_lint",
    "ecosystem": "dotnet",
    "inputs": ["projectRoot", "solutionPath"]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.package_publish.dotnet.v1.json") @'
{
  "id": "task.package_publish.dotnet.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 12 },
  "title": "Package/Publish .NET",
  "description": "Package and publish .NET artifacts using configured feeds/credentials.",
  "tags": ["dotnet", "package", "publish"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "package_publish",
    "ecosystem": "dotnet",
    "inputs": ["projectRoot", "solutionPath", "releaseVersion"]
  }
}
'@

# -------------------------
# ATOMS: cpp publish (optional but rounds out the verbs)
# -------------------------

Write-Json (Join-Path $bt "atoms\task.package_publish.cpp.v1.json") @'
{
  "id": "task.package_publish.cpp.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 11 },
  "title": "Package/Publish C++",
  "description": "Package and publish a C++ build artifact using configured packaging settings.",
  "tags": ["cpp", "package", "publish"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "package_publish",
    "ecosystem": "cpp",
    "inputs": ["projectRoot", "buildFolderName", "packageSpec", "releaseVersion"]
  }
}
'@

# -------------------------
# ATOMS: generic diagnose variants (still abstract, better routing targets)
# -------------------------

Write-Json (Join-Path $bt "atoms\task.diagnose.restore_fail.v1.json") @'
{
  "id": "task.diagnose.restore_fail.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Diagnose Restore Failure",
  "description": "Classify and propose fixes for dependency restore/resolve failures.",
  "tags": ["diagnose", "restore", "dependencies"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "diagnose", "subtype": "restore_fail", "inputs": ["logText"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.diagnose.compile_fail.v1.json") @'
{
  "id": "task.diagnose.compile_fail.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Diagnose Compile Failure",
  "description": "Classify and propose fixes for compilation failures.",
  "tags": ["diagnose", "compile"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "diagnose", "subtype": "compile_fail", "inputs": ["logText"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.diagnose.test_fail.v1.json") @'
{
  "id": "task.diagnose.test_fail.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Diagnose Test Failure",
  "description": "Classify and propose fixes for failing tests.",
  "tags": ["diagnose", "test"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "diagnose", "subtype": "test_fail", "inputs": ["logText"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.diagnose.run_fail.v1.json") @'
{
  "id": "task.diagnose.run_fail.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Diagnose Run Failure",
  "description": "Classify and propose fixes for runtime failures (startup/crash/config).",
  "tags": ["diagnose", "run", "runtime"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "diagnose", "subtype": "run_fail", "inputs": ["logText"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.diagnose.publish_fail.v1.json") @'
{
  "id": "task.diagnose.publish_fail.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Diagnose Publish Failure",
  "description": "Classify and propose fixes for packaging/publishing failures.",
  "tags": ["diagnose", "publish", "package"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "diagnose", "subtype": "publish_fail", "inputs": ["logText"] }
}
'@

# -------------------------
# PACKS: wizard + ecosystem convenience (public)
# -------------------------

Write-Json (Join-Path $bt "packs\pack.project.start.wizard.v1.json") @'
{
  "id": "pack.project.start.wizard.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 160 },
  "title": "Start Project Wizard",
  "description": "Wizardized project start: choose ecosystem/profile, apply role preset, create, run, validate.",
  "tags": ["project", "wizard", "start"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "preset.role.${rolePreset}.v1",
      "task.create_project.${ecosystem}.${profile}.v1",
      "task.format_lint.${ecosystem}.v1",
      "task.build.${ecosystem}.v1",
      "task.test.${ecosystem}.v1",
      "task.run.${ecosystem}.v1"
    ]
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.project.cli.tool.all.v1.json") @'
{
  "id": "pack.project.cli.tool.all.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 150 },
  "title": "CLI Tool (Any Ecosystem)",
  "description": "Create a CLI tool in dotnet/node/python/go/rust/java/cpp using the same guided verb flow.",
  "tags": ["cli", "project", "multi"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.create_project.${ecosystem}.cli.v1",
      "task.format_lint.${ecosystem}.v1",
      "task.build.${ecosystem}.v1",
      "task.test.${ecosystem}.v1",
      "task.run.${ecosystem}.v1"
    ]
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.diagnose.classify_and_fix.v1.json") @'
{
  "id": "pack.diagnose.classify_and_fix.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 170 },
  "title": "Diagnose: Classify and Fix",
  "description": "Classify failure type then run targeted diagnosis and minimal safe repair steps.",
  "tags": ["diagnose", "repair", "triage"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.diagnose.restore_fail.v1",
      "task.diagnose.compile_fail.v1",
      "task.diagnose.test_fail.v1",
      "task.diagnose.run_fail.v1",
      "task.diagnose.publish_fail.v1",
      "task.apply_patch.generic.v1",
      "task.build.${ecosystem}.v1",
      "task.test.${ecosystem}.v1"
    ]
  }
}
'@

# -------------------------
# GRAPHS: triage router + orchestrator
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.diagnose.classify.v1.json") @'
{
  "id": "graph.router.diagnose.classify.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Diagnose Classifier Router",
  "description": "Routes a failure log into restore/compile/test/run/publish diagnosis targets.",
  "tags": ["router", "diagnose", "classify"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Log[Failure Log] --> Classify{classify_failure}\n  Classify -->|restore| R[task.diagnose.restore_fail.v1]\n  Classify -->|compile| C[task.diagnose.compile_fail.v1]\n  Classify -->|test| T[task.diagnose.test_fail.v1]\n  Classify -->|run| U[task.diagnose.run_fail.v1]\n  Classify -->|publish| P[task.diagnose.publish_fail.v1]\n  Classify -->|unknown| G[task.diagnose_build_fail_triage.generic.v1]"
  }
}
'@

Write-Json (Join-Path $bt "graphs\graph.orchestrator.diagnose.minimal_fix.v1.json") @'
{
  "id": "graph.orchestrator.diagnose.minimal_fix.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Diagnose Orchestrator: Minimal Fix",
  "description": "Orchestrator flow: classify -> propose minimal patch -> WAIT_USER -> apply -> verify -> loop.",
  "tags": ["orchestrator", "diagnose", "minimal"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  FailLog --> Classify\n  Classify --> ProposeFix\n  ProposeFix --> WAIT_USER\n  WAIT_USER -->|approved| Apply[task.apply_patch.generic.v1]\n  WAIT_USER -->|edit| ProposeFix\n  Apply --> VerifyBuild[task.build.${ecosystem}.v1]\n  VerifyBuild --> VerifyTest[task.test.${ecosystem}.v1]\n  VerifyTest -->|ok| Done\n  VerifyTest -->|fail| FailLog"
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 3 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

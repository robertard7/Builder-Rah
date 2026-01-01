# BlueprintTemplates/Seed-Corpus-Batch2.ps1
# Batch2: Fill out 10-verb atom coverage for common ecosystems + add more graphs.
# Works on Windows PowerShell 5.1 and PowerShell 7+.
# No provider/toolchain IDs. No hardcoded paths. Abstract actions only.

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
if ($hereName -ieq "BlueprintTemplates") {
  $bt = $base
} else {
  $bt = Join-Path $base "BlueprintTemplates"
}

Ensure-Dir (Join-Path $bt "atoms")
Ensure-Dir (Join-Path $bt "graphs")
Ensure-Dir (Join-Path $bt "packs")
Ensure-Dir (Join-Path $bt "recipes")

# -------------------------
# CORE VERB ATOMS (ecosystem coverage)
# -------------------------

Write-Json (Join-Path $bt "atoms\task.build.node.v1.json") @'
{
  "id": "task.build.node.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 11 },
  "title": "Build Node",
  "description": "Build a Node project using configured build script/settings.",
  "tags": ["node", "build"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "build", "ecosystem": "node", "inputs": ["projectRoot"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.test.node.v1.json") @'
{
  "id": "task.test.node.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 11 },
  "title": "Test Node",
  "description": "Run Node project tests using configured test script/settings.",
  "tags": ["node", "test"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "test", "ecosystem": "node", "inputs": ["projectRoot"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.format_lint.node.v1.json") @'
{
  "id": "task.format_lint.node.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 11 },
  "title": "Format/Lint Node",
  "description": "Format and lint a Node project using configured tooling.",
  "tags": ["node", "format", "lint"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "format_lint", "ecosystem": "node", "inputs": ["projectRoot"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.package_publish.node.v1.json") @'
{
  "id": "task.package_publish.node.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 11 },
  "title": "Package/Publish Node",
  "description": "Package and publish a Node package using configured registry credentials.",
  "tags": ["node", "package", "publish"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "package_publish", "ecosystem": "node", "inputs": ["projectRoot", "releaseVersion"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.build.python.v1.json") @'
{
  "id": "task.build.python.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 11 },
  "title": "Build Python",
  "description": "Build/package a Python project using configured build settings.",
  "tags": ["python", "build"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "build", "ecosystem": "python", "inputs": ["projectRoot"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.test.python.v1.json") @'
{
  "id": "task.test.python.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 11 },
  "title": "Test Python",
  "description": "Run Python tests using configured runner/settings.",
  "tags": ["python", "test"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "test", "ecosystem": "python", "inputs": ["projectRoot"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.format_lint.python.v1.json") @'
{
  "id": "task.format_lint.python.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 11 },
  "title": "Format/Lint Python",
  "description": "Format and lint Python using configured tooling.",
  "tags": ["python", "format", "lint"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "format_lint", "ecosystem": "python", "inputs": ["projectRoot"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.package_publish.python.v1.json") @'
{
  "id": "task.package_publish.python.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 11 },
  "title": "Package/Publish Python",
  "description": "Package and publish Python using configured index/credentials.",
  "tags": ["python", "package", "publish"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "package_publish", "ecosystem": "python", "inputs": ["projectRoot", "releaseVersion"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.test.go.v1.json") @'
{
  "id": "task.test.go.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 11 },
  "title": "Test Go",
  "description": "Run Go tests using configured settings.",
  "tags": ["go", "test"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "test", "ecosystem": "go", "inputs": ["projectRoot"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.run.go.v1.json") @'
{
  "id": "task.run.go.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 11 },
  "title": "Run Go",
  "description": "Run a Go CLI/app with configured run target.",
  "tags": ["go", "run"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "run", "ecosystem": "go", "inputs": ["projectRoot", "runTarget"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.format_lint.go.v1.json") @'
{
  "id": "task.format_lint.go.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 11 },
  "title": "Format/Lint Go",
  "description": "Format and lint Go using configured tooling.",
  "tags": ["go", "format", "lint"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "format_lint", "ecosystem": "go", "inputs": ["projectRoot"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.package_publish.go.v1.json") @'
{
  "id": "task.package_publish.go.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 11 },
  "title": "Package/Publish Go",
  "description": "Package and publish a Go module using configured registry settings.",
  "tags": ["go", "package", "publish"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "package_publish", "ecosystem": "go", "inputs": ["projectRoot", "releaseVersion"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.test.rust.v1.json") @'
{
  "id": "task.test.rust.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 11 },
  "title": "Test Rust",
  "description": "Run Rust tests using configured settings.",
  "tags": ["rust", "test"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "test", "ecosystem": "rust", "inputs": ["projectRoot"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.run.rust.v1.json") @'
{
  "id": "task.run.rust.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 11 },
  "title": "Run Rust",
  "description": "Run a Rust binary using configured run target.",
  "tags": ["rust", "run"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "run", "ecosystem": "rust", "inputs": ["projectRoot", "runTarget"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.format_lint.rust.v1.json") @'
{
  "id": "task.format_lint.rust.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 11 },
  "title": "Format/Lint Rust",
  "description": "Format and lint Rust using configured tooling.",
  "tags": ["rust", "format", "lint"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "format_lint", "ecosystem": "rust", "inputs": ["projectRoot"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.package_publish.rust.v1.json") @'
{
  "id": "task.package_publish.rust.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 11 },
  "title": "Package/Publish Rust",
  "description": "Package and publish a Rust crate using configured registry settings.",
  "tags": ["rust", "package", "publish"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "package_publish", "ecosystem": "rust", "inputs": ["projectRoot", "releaseVersion"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.test.java.v1.json") @'
{
  "id": "task.test.java.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 11 },
  "title": "Test Java (Maven)",
  "description": "Run Java tests using Maven configuration.",
  "tags": ["java", "maven", "test"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "test", "ecosystem": "java", "inputs": ["projectRoot"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.run.java.v1.json") @'
{
  "id": "task.run.java.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 11 },
  "title": "Run Java (Maven)",
  "description": "Run a Java app using configured Maven exec/run settings.",
  "tags": ["java", "maven", "run"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "run", "ecosystem": "java", "inputs": ["projectRoot", "runTarget"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.format_lint.java.v1.json") @'
{
  "id": "task.format_lint.java.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 11 },
  "title": "Format/Lint Java",
  "description": "Format and lint Java using configured tooling.",
  "tags": ["java", "format", "lint"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "format_lint", "ecosystem": "java", "inputs": ["projectRoot"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.package_publish.java.v1.json") @'
{
  "id": "task.package_publish.java.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 11 },
  "title": "Package/Publish Java (Maven)",
  "description": "Package and publish Java artifacts using configured repository credentials.",
  "tags": ["java", "maven", "package", "publish"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "package_publish", "ecosystem": "java", "inputs": ["projectRoot", "releaseVersion"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.test.cpp.v1.json") @'
{
  "id": "task.test.cpp.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 11 },
  "title": "Test C++ (CMake)",
  "description": "Run CMake/CTest tests using configured settings.",
  "tags": ["cpp", "cmake", "test"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "test", "ecosystem": "cpp", "inputs": ["projectRoot", "buildFolderName"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.run.cpp.v1.json") @'
{
  "id": "task.run.cpp.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 11 },
  "title": "Run C++ (CMake)",
  "description": "Run a built C++ target from a CMake build output.",
  "tags": ["cpp", "cmake", "run"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "run", "ecosystem": "cpp", "inputs": ["projectRoot", "buildFolderName", "runTarget"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.format_lint.cpp.v1.json") @'
{
  "id": "task.format_lint.cpp.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 11 },
  "title": "Format/Lint C++",
  "description": "Format and lint C++ using configured tooling (clang-format/clang-tidy or equivalents).",
  "tags": ["cpp", "format", "lint"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "format_lint", "ecosystem": "cpp", "inputs": ["projectRoot"] }
}
'@

# -------------------------
# GRAPHS: stronger default routing
# -------------------------
Write-Json (Join-Path $bt "graphs\graph.router.default_unstoppable.v1.json") @'
{
  "id": "graph.router.default_unstoppable.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router Default (Unstoppable)",
  "description": "Routes common intents into packs and falls back to diagnosis/plan when uncertain.",
  "tags": ["router", "default"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Input --> DetectIntent{intent?}\n  DetectIntent -->|start project| StartPack[pack.project.start.v1]\n  DetectIntent -->|add feature| FeaturePack[pack.add.feature.v1]\n  DetectIntent -->|apply patch| PatchPack[pack.patch.apply.v1]\n  DetectIntent -->|format/lint| FormatPack[pack.format.lint.v1]\n  DetectIntent -->|publish| PublishPack[pack.package.publish.v1]\n  DetectIntent -->|build fail| DiagnosePack[pack.diagnose.buildfail.v1]\n  DetectIntent -->|unknown| Fallback[Brainstorm/Plan]"
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 2 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

# BlueprintTemplates/Seed-Corpus-Batch8.ps1
# Batch8: release hygiene (version/changelog/license/release notes) + scaffold tests if missing + release router.
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
Ensure-Dir (Join-Path $bt "graphs")
Ensure-Dir (Join-Path $bt "recipes")

# -------------------------
# ATOMS: release hygiene primitives (internal)
# -------------------------

Write-Json (Join-Path $bt "atoms\task.release.bump_version.v1.json") @'
{
  "id": "task.release.bump_version.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Release: Bump Version",
  "description": "Bump version according to ecosystem conventions (no hardcoded tooling).",
  "tags": ["release", "version"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "release_bump_version",
    "inputs": ["projectRoot", "ecosystem", "releaseVersion"],
    "targets": [
      { "ecosystem": "node", "file": "package.json", "field": "version" },
      { "ecosystem": "python", "file": "pyproject.toml", "field": "project.version" },
      { "ecosystem": "dotnet", "fileHint": "*.csproj", "field": "Version" },
      { "ecosystem": "java", "file": "pom.xml", "field": "version" },
      { "ecosystem": "rust", "file": "Cargo.toml", "field": "package.version" }
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.release.update_changelog.v1.json") @'
{
  "id": "task.release.update_changelog.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Release: Update Changelog",
  "description": "Append release notes into CHANGELOG.md using Keep a Changelog-ish structure.",
  "tags": ["release", "changelog"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "release_update_changelog",
    "inputs": ["projectRoot", "releaseVersion", "changesAdded", "changesChanged", "changesFixed", "changesRemoved", "changesSecurity"]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.release.generate_notes.v1.json") @'
{
  "id": "task.release.generate_notes.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Release: Generate Release Notes",
  "description": "Generate RELEASE_NOTES.md from changelog entries and brief summary.",
  "tags": ["release", "notes"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "release_generate_notes",
    "inputs": ["projectRoot", "releaseVersion", "summary", "highlights", "breakingChanges", "upgradeNotes"]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.release.ensure_license.v1.json") @'
{
  "id": "task.release.ensure_license.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "Release: Ensure License File",
  "description": "Ensure LICENSE exists (user chooses SPDX id/name; never assumes).",
  "tags": ["release", "license"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "ensure_license",
    "inputs": ["projectRoot", "licenseName", "licenseText"],
    "notes": "User must supply licenseName and licenseText (no auto-legal)."
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.tests.scaffold_if_missing.v1.json") @'
{
  "id": "task.tests.scaffold_if_missing.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Tests: Scaffold if Missing",
  "description": "If no tests detected, add a minimal smoke test using an ecosystem-appropriate recipe.",
  "tags": ["tests", "scaffold", "smoke"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "tests_scaffold_if_missing",
    "inputs": ["projectRoot"],
    "detect": { "anyGlob": "tests/**,test/**,**/*Tests*.*,**/*test*.*" },
    "whenMissing": {
      "dotnet": ["recipe.dotnet.tests.xunit_smoke.v1"],
      "python": ["recipe.python.tests.pytest_smoke.v1"],
      "node": ["recipe.node.tests.basic_smoke.v1"],
      "go": ["recipe.go.tests.basic_smoke.v1"],
      "rust": ["recipe.rust.tests.basic_smoke.v1"],
      "java": ["recipe.java.tests.basic_smoke.v1"],
      "cpp": ["recipe.cpp.tests.basic_smoke.v1"]
    }
  }
}
'@

# -------------------------
# PACKS: release prep
# -------------------------

Write-Json (Join-Path $bt "packs\pack.release.prep.v1.json") @'
{
  "id": "pack.release.prep.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 170 },
  "title": "Release Prep",
  "description": "Prepares a project for release: ensure tests exist, run CI, bump version, update changelog, generate notes, gated publish.",
  "tags": ["release", "prep", "publish"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.detect.project_shape.v1",
      "task.tests.scaffold_if_missing.v1",
      "pack.ci.pipeline.matrix.v1",
      "task.release.bump_version.v1",
      "task.release.update_changelog.v1",
      "task.release.generate_notes.v1",
      "pack.package.publish.gated.v1"
    ]
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.release.hygiene.only.v1.json") @'
{
  "id": "pack.release.hygiene.only.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 150 },
  "title": "Release Hygiene Only",
  "description": "Changelog + notes + license checks, no publishing.",
  "tags": ["release", "hygiene"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.release.ensure_license.v1",
      "task.release.update_changelog.v1",
      "task.release.generate_notes.v1"
    ]
  }
}
'@

# -------------------------
# GRAPHS: release router
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.release_prep.v1.json") @'
{
  "id": "graph.router.release_prep.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: Release Prep",
  "description": "Routes release intents to prep/hygiene/publish-gated packs.",
  "tags": ["router", "release"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Input[release intent] --> Pick{which?}\n  Pick -->|prep| Prep[pack.release.prep.v1]\n  Pick -->|hygiene| Hyg[pack.release.hygiene.only.v1]\n  Pick -->|publish| Pub[pack.package.publish.gated.v1]\n  Pick -->|other| Fallback[graph.router.goal_by_maturity.v1]"
  }
}
'@

# -------------------------
# RECIPES: ecosystem smoke tests (file-only)
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.node.tests.basic_smoke.v1.json") @'
{
  "id": "recipe.node.tests.basic_smoke.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 50 },
  "title": "Node Tests: Basic Smoke",
  "description": "Adds a minimal test file using built-in assert (no deps assumed).",
  "tags": ["node", "test", "smoke"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/test/smoke.test.js", "onConflict": "skip", "text": "const assert = require('assert');\n\ndescribe('smoke', () => {\n  it('works', () => {\n    assert.strictEqual(1 + 1, 2);\n  });\n});\n" } }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.go.tests.basic_smoke.v1.json") @'
{
  "id": "recipe.go.tests.basic_smoke.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 50 },
  "title": "Go Tests: Basic Smoke",
  "description": "Adds a minimal go test file.",
  "tags": ["go", "test", "smoke"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/smoke_test.go", "onConflict": "skip", "text": "package main\n\nimport \"testing\"\n\nfunc TestSmoke(t *testing.T) {\n  if 1+1 != 2 { t.Fatal(\"math broke\") }\n}\n" } }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.rust.tests.basic_smoke.v1.json") @'
{
  "id": "recipe.rust.tests.basic_smoke.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 50 },
  "title": "Rust Tests: Basic Smoke",
  "description": "Adds a minimal Rust test module.",
  "tags": ["rust", "test", "smoke"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/lib.rs", "onConflict": "skip", "text": "#[cfg(test)]\nmod tests {\n  #[test]\n  fn smoke() {\n    assert_eq!(1 + 1, 2);\n  }\n}\n" } }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.java.tests.basic_smoke.v1.json") @'
{
  "id": "recipe.java.tests.basic_smoke.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 50 },
  "title": "Java Tests: Basic Smoke",
  "description": "Adds a minimal test file placeholder (framework not assumed).",
  "tags": ["java", "test", "smoke"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/test/java/${groupPath}/SmokeTest.java", "onConflict": "skip", "text": "package ${groupId};\n\npublic final class SmokeTest {\n  public static void main(String[] args) {\n    if (1 + 1 != 2) throw new RuntimeException(\"math broke\");\n    System.out.println(\"OK\");\n  }\n}\n" } }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.cpp.tests.basic_smoke.v1.json") @'
{
  "id": "recipe.cpp.tests.basic_smoke.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 50 },
  "title": "C++ Tests: Basic Smoke",
  "description": "Adds a minimal test source file (framework not assumed).",
  "tags": ["cpp", "test", "smoke"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/tests/smoke.cpp", "onConflict": "skip", "text": "#include <cassert>\n\nint main() {\n  assert(1 + 1 == 2);\n  return 0;\n}\n" } }
    ]
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 8 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

# BlueprintTemplates/Seed-Corpus-Batch5.ps1
# Batch5: Monorepo/workspace packs + safe file ops atoms + project-shape router + useful recipes.
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
# ATOMS: safe file operations (internal primitives, abstract)
# -------------------------

Write-Json (Join-Path $bt "atoms\task.replace_file.guarded.v1.json") @'
{
  "id": "task.replace_file.guarded.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "Replace File (Guarded)",
  "description": "Replace a file only if guards match (hash/contains markers) to prevent accidental clobber.",
  "tags": ["replace_file", "guard", "safe"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "replace_file",
    "mode": "guarded",
    "inputs": ["path", "text"],
    "guards": {
      "sha256EqualsOptional": "${expectedSha256?}",
      "mustContainAllOptional": "${mustContainAll?}",
      "mustNotContainAnyOptional": "${mustNotContainAny?}"
    }
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.add_file.conflict_policy.v1.json") @'
{
  "id": "task.add_file.conflict_policy.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "Add File (Conflict Policy)",
  "description": "Add a file with a conflict policy (fail/skip/rename) if path exists.",
  "tags": ["add_file", "safe"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "add_file",
    "mode": "conflict_policy",
    "inputs": ["path", "text"],
    "policy": "${onConflict}" 
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.apply_patch.verified.v1.json") @'
{
  "id": "task.apply_patch.verified.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "Apply Patch (Verified)",
  "description": "Apply a patch then verify by running configured build/test steps (no hardcoded commands).",
  "tags": ["apply_patch", "verify", "safe"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "apply_patch",
    "mode": "verified",
    "inputs": ["patchText", "projectRoot"],
    "verify": {
      "build": "task.build.${ecosystem}.v1",
      "test": "task.test.${ecosystem}.v1"
    }
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.detect.project_shape.v1.json") @'
{
  "id": "task.detect.project_shape.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 4 },
  "title": "Detect Project Shape",
  "description": "Detect ecosystem/profile by inspecting file patterns (no shell execution).",
  "tags": ["detect", "shape", "router"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "detect_project_shape",
    "inputs": ["projectRoot"],
    "signals": [
      { "ifFileExists": "global.json", "hint": "dotnet" },
      { "ifAnyGlob": "**/*.sln", "hint": "dotnet" },
      { "ifFileExists": "package.json", "hint": "node" },
      { "ifAnyGlob": "pyproject.toml,requirements.txt,setup.py", "hint": "python" },
      { "ifFileExists": "go.mod", "hint": "go" },
      { "ifFileExists": "Cargo.toml", "hint": "rust" },
      { "ifFileExists": "pom.xml", "hint": "java" },
      { "ifAnyGlob": "CMakeLists.txt", "hint": "cpp" }
    ],
    "outputs": ["ecosystem", "profileGuess"]
  }
}
'@

# -------------------------
# PACKS: monorepo/workspace (public)
# -------------------------

Write-Json (Join-Path $bt "packs\pack.project.monorepo.dotnet.v1.json") @'
{
  "id": "pack.project.monorepo.dotnet.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 155 },
  "title": ".NET Monorepo (Solution + Multiple Projects)",
  "description": "Sets up a solution-style monorepo with common project folders and validation flow.",
  "tags": ["dotnet", "monorepo", "sln"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "ecosystem": "dotnet",
    "uses": [
      "task.create_project.dotnet.cli.v1",
      "recipe.dotnet.sln.multi_project_layout.v1",
      "task.build.dotnet.v1",
      "task.test.dotnet.v1"
    ]
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.project.monorepo.node.workspaces.v1.json") @'
{
  "id": "pack.project.monorepo.node.workspaces.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 155 },
  "title": "Node Monorepo (Workspaces)",
  "description": "Sets up a Node monorepo with workspaces for packages/apps.",
  "tags": ["node", "monorepo", "workspaces"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "ecosystem": "node",
    "uses": [
      "recipe.node.workspaces.base.v1",
      "recipe.node.web.basic_routes.v1",
      "task.test.node.v1",
      "task.run.node.v1"
    ]
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.project.python.src_layout.v1.json") @'
{
  "id": "pack.project.python.src_layout.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 145 },
  "title": "Python Project (src/ layout)",
  "description": "Sets up a Python src layout and minimal tests.",
  "tags": ["python", "src", "layout"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "ecosystem": "python",
    "uses": [
      "recipe.python.src_layout.base.v1",
      "recipe.python.tests.pytest_smoke.v1",
      "task.test.python.v1"
    ]
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.patch.apply.verified.v1.json") @'
{
  "id": "pack.patch.apply.verified.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 165 },
  "title": "Apply Patch (Verified)",
  "description": "Apply a patch and verify via build/test. Uses ecosystem detection when needed.",
  "tags": ["patch", "verify", "safe"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.detect.project_shape.v1",
      "task.apply_patch.verified.v1"
    ]
  }
}
'@

# -------------------------
# GRAPHS: route “add feature” into recipes based on project shape
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.feature_by_shape.v1.json") @'
{
  "id": "graph.router.feature_by_shape.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Feature Router by Project Shape",
  "description": "Detects project shape and routes to a sensible starter recipe for feature addition.",
  "tags": ["router", "feature", "shape"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Input[add feature] --> Detect[task.detect.project_shape.v1]\n  Detect -->|node| R1[recipe.node.web.basic_routes.v1]\n  Detect -->|python| R2[recipe.python.api.basic_endpoints.v1]\n  Detect -->|go| R3[recipe.go.cli.flags.v1]\n  Detect -->|rust| R4[recipe.rust.cli.args_logging.v1]\n  Detect -->|java| R5[recipe.java.cli.maven.hello.v1]\n  Detect -->|cpp| R6[recipe.cpp.cmake.hello_app.v1]\n  Detect -->|dotnet| R7[recipe.dotnet.tests.xunit_smoke.v1]\n  Detect -->|unknown| Fallback[pack.add.feature.v1]"
  }
}
'@

# -------------------------
# RECIPES: missing useful scaffolds
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.node.workspaces.base.v1.json") @'
{
  "id": "recipe.node.workspaces.base.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 60 },
  "title": "Node Workspaces Base",
  "description": "Adds a basic workspace root package.json and folders.",
  "tags": ["node", "workspaces", "monorepo"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/package.json", "onConflict": "fail", "text": "{\n  \"name\": \"${projectName}\",\n  \"private\": true,\n  \"workspaces\": [\"packages/*\", \"apps/*\"]\n}\n" } },
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/README.md", "onConflict": "skip", "text": "# ${projectName}\n\nMonorepo workspace.\n" } }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.python.src_layout.base.v1.json") @'
{
  "id": "recipe.python.src_layout.base.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 60 },
  "title": "Python src/ Layout Base",
  "description": "Adds src/ package layout and minimal metadata files.",
  "tags": ["python", "src", "layout"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/${projectName}/__init__.py", "onConflict": "skip", "text": "" } },
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/pyproject.toml", "onConflict": "fail", "text": "[project]\nname = \"${projectName}\"\nversion = \"0.1.0\"\nrequires-python = \">=3.10\"\n\n[tool.pytest.ini_options]\ntestpaths = [\"tests\"]\n" } }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.python.tests.pytest_smoke.v1.json") @'
{
  "id": "recipe.python.tests.pytest_smoke.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 55 },
  "title": "Python Tests: Pytest Smoke",
  "description": "Adds a minimal pytest test file.",
  "tags": ["python", "test", "pytest"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/tests/test_smoke.py", "onConflict": "skip", "text": "def test_smoke():\n    assert 1 + 1 == 2\n" } }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.dotnet.tests.xunit_smoke.v1.json") @'
{
  "id": "recipe.dotnet.tests.xunit_smoke.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 55 },
  "title": ".NET Tests: xUnit Smoke",
  "description": "Adds a minimal xUnit test file (assumes test project exists or will be created).",
  "tags": ["dotnet", "test", "xunit"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/tests/SmokeTests/SmokeTests.cs", "onConflict": "skip", "text": "using Xunit;\n\npublic sealed class SmokeTests\n{\n  [Fact]\n  public void It_works()\n  {\n    Assert.True(true);\n  }\n}\n" } }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.dotnet.sln.multi_project_layout.v1.json") @'
{
  "id": "recipe.dotnet.sln.multi_project_layout.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 60 },
  "title": ".NET Solution Multi-Project Layout",
  "description": "Creates common folders and a README for a solution-style repo.",
  "tags": ["dotnet", "sln", "layout"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/README.md", "onConflict": "skip", "text": "# ${projectName}\n\nSolution-based repository.\n\nFolders:\n- src/\n- tests/\n" } },
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/.keep", "onConflict": "skip", "text": "" } },
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/tests/.keep", "onConflict": "skip", "text": "" } }
    ]
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 5 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

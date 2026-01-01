# BlueprintTemplates/Seed-Corpus-Batch13.ps1
# Batch13: monorepo / workspace scaffolds + router
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
# ATOMS (internal)
# -------------------------

Write-Json (Join-Path $bt "atoms\task.monorepo.detect_workspace.v1.json") @'
{
  "id": "task.monorepo.detect_workspace.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Monorepo: Detect workspace",
  "description": "Detects multi-project repo patterns and suggests a workspace strategy. No commands; analysis-only.",
  "tags": ["monorepo", "workspace", "detect"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "detect_workspace",
    "inputs": ["projectRoot"],
    "signals": [
      "multiple solution files",
      "multiple package manifests",
      "multiple module descriptors",
      "src/apps/libs folders"
    ],
    "outputs": ["workspaceType", "projectList", "recommendedLayout"]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.monorepo.scaffold.workspace_root.v1.json") @'
{
  "id": "task.monorepo.scaffold.workspace_root.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Monorepo: Scaffold workspace root",
  "description": "Creates a conventional monorepo layout and root docs. No commands, no toolchain assumptions.",
  "tags": ["monorepo", "workspace", "layout"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "scaffold_workspace_root",
    "inputs": ["projectRoot", "workspaceType"],
    "routes": {
      "generic": ["recipe.monorepo.workspace.generic.v1"],
      "dotnet": ["recipe.monorepo.workspace.dotnet.v1"],
      "node": ["recipe.monorepo.workspace.node.v1"],
      "python": ["recipe.monorepo.workspace.python.v1"],
      "go": ["recipe.monorepo.workspace.go.v1"],
      "rust": ["recipe.monorepo.workspace.rust.v1"],
      "java": ["recipe.monorepo.workspace.java.v1"],
      "cpp": ["recipe.monorepo.workspace.cpp.v1"]
    }
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.monorepo.add_project_skeleton.v1.json") @'
{
  "id": "task.monorepo.add_project_skeleton.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "Monorepo: Add project skeleton",
  "description": "Adds a new app/lib skeleton under apps/ or libs/ with minimal wiring. Uses existing create_project atoms by ecosystem.",
  "tags": ["monorepo", "workspace", "project"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "monorepo_add_project_skeleton",
    "inputs": ["projectRoot", "ecosystem", "profile", "name", "kind"],
    "delegates": {
      "dotnet.cli": "task.create_project.dotnet.cli.v1",
      "dotnet.library": "task.create_project.dotnet.library.v1",
      "node.cli": "task.create_project.node.cli.v1",
      "node.web": "task.create_project.node.web.v1",
      "python.cli": "task.create_project.python.cli.v1",
      "python.api": "task.create_project.python.api.v1",
      "go.cli": "task.create_project.go.cli.v1",
      "rust.cli": "task.create_project.rust.cli.v1",
      "java.cli": "task.create_project.java.cli.v1",
      "cpp.cmake": "task.create_project.cpp.cmake.v1"
    }
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.monorepo.add_root_build_docs.v1.json") @'
{
  "id": "task.monorepo.add_root_build_docs.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "Monorepo: Add root build/test/run docs",
  "description": "Adds root docs describing how to build/test/run multiple projects via Settings (no commands).",
  "tags": ["monorepo", "docs"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "monorepo_add_root_docs",
    "inputs": ["projectRoot"],
    "route": "recipe.monorepo.root.docs.v1"
  }
}
'@

# -------------------------
# PACKS (public)
# -------------------------

Write-Json (Join-Path $bt "packs\pack.monorepo.workspace.bootstrap.v1.json") @'
{
  "id": "pack.monorepo.workspace.bootstrap.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 170 },
  "title": "Monorepo: Workspace Bootstrap",
  "description": "Scaffolds a monorepo workspace root (apps/libs/docs), detects current layout, and adds root docs.",
  "tags": ["monorepo", "workspace", "bootstrap"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.monorepo.detect_workspace.v1",
      "task.monorepo.scaffold.workspace_root.v1",
      "task.monorepo.add_root_build_docs.v1",
      "pack.repo.hygiene.baseline.v1"
    ],
    "defaults": { "workspaceType": "generic", "ecosystem": "generic" }
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.monorepo.add_app_or_lib.v1.json") @'
{
  "id": "pack.monorepo.add_app_or_lib.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 160 },
  "title": "Monorepo: Add App or Library",
  "description": "Adds a new app or library into apps/ or libs/ and updates workspace docs.",
  "tags": ["monorepo", "workspace", "add"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.monorepo.add_project_skeleton.v1",
      "task.monorepo.add_root_build_docs.v1"
    ],
    "defaults": { "ecosystem": "dotnet", "profile": "cli", "kind": "app" }
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.monorepo.standardize.layout.v1.json") @'
{
  "id": "pack.monorepo.standardize.layout.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 150 },
  "title": "Monorepo: Standardize Layout",
  "description": "Normalizes an existing multi-project repo into a clearer apps/libs structure and adds docs (non-destructive guidance).",
  "tags": ["monorepo", "layout", "standardize"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.monorepo.detect_workspace.v1",
      "recipe.monorepo.migration.plan_only.v1"
    ]
  }
}
'@

# -------------------------
# GRAPHS (public): router
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.monorepo.v1.json") @'
{
  "id": "graph.router.monorepo.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: Monorepo / Workspace",
  "description": "Routes workspace/monorepo requests, including detect+bootstrap and add app/lib.",
  "tags": ["router", "monorepo", "workspace"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Input[workspace request] --> Pick{intent?}\n  Pick -->|bootstrap| Boot[pack.monorepo.workspace.bootstrap.v1]\n  Pick -->|add app/lib| Add[pack.monorepo.add_app_or_lib.v1]\n  Pick -->|standardize existing| Std[pack.monorepo.standardize.layout.v1]\n  Pick -->|unsure| Boot"
  }
}
'@

# -------------------------
# RECIPES (workspace scaffolds)
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.monorepo.workspace.generic.v1.json") @'
{
  "id": "recipe.monorepo.workspace.generic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 55 },
  "title": "Workspace: Generic scaffold",
  "description": "Creates apps/, libs/, docs/ with minimal README stubs.",
  "tags": ["monorepo", "workspace", "generic"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.ensure_dir.v1", "with": { "path": "${projectRoot}/apps" } },
    { "use": "task.ensure_dir.v1", "with": { "path": "${projectRoot}/libs" } },
    { "use": "task.ensure_dir.v1", "with": { "path": "${projectRoot}/docs" } },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/apps/README.md", "onConflict": "skip", "text": "# Apps\n\nApplication projects live here.\n" } },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/libs/README.md", "onConflict": "skip", "text": "# Libraries\n\nReusable libraries live here.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.monorepo.root.docs.v1.json") @'
{
  "id": "recipe.monorepo.root.docs.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 50 },
  "title": "Workspace: Root docs",
  "description": "Adds docs describing how to build/test/run in a multi-project repo using Settings.",
  "tags": ["monorepo", "docs"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.ensure_dir.v1", "with": { "path": "${projectRoot}/docs" } },
    { "use": "task.add_file.conflict_policy.v1", "with": {
      "path": "${projectRoot}/docs/workspace.md",
      "onConflict": "skip",
      "text": "# Workspace\n\nThis repo contains multiple projects.\n\n## Layout\n- apps/: applications\n- libs/: shared libraries\n\n## Build/Test/Run\nUse your environment Settings to select how each ecosystem builds/tests/runs.\n\n## Adding a new project\nUse the 'Monorepo: Add App or Library' pack and choose ecosystem/profile.\n"
    } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.monorepo.migration.plan_only.v1.json") @'
{
  "id": "recipe.monorepo.migration.plan_only.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 45 },
  "title": "Workspace: Migration plan (non-destructive)",
  "description": "Produces a plan for standardizing layout without moving files automatically.",
  "tags": ["monorepo", "migration", "plan"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.diagnose_build_fail_triage.generic.v1" },
    { "use": "task.add_file.conflict_policy.v1", "with": {
      "path": "${projectRoot}/docs/workspace_migration_plan.md",
      "onConflict": "skip",
      "text": "# Workspace Migration Plan\n\n## Goals\n- Standardize to apps/ and libs/\n- Minimize moving/renaming\n\n## Inventory\nList current projects, their build files, and dependencies.\n\n## Proposed mapping\n- Map each project into apps/ or libs/\n\n## Steps\n- Move in small batches\n- Verify build/test each batch using Settings\n"
    } }
  ] }
}
'@

# Ecosystem workspace notes (thin overlays)
Write-Json (Join-Path $bt "recipes\recipe.monorepo.workspace.dotnet.v1.json") @'
{
  "id": "recipe.monorepo.workspace.dotnet.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 50 },
  "title": "Workspace: .NET conventions",
  "description": "Generic workspace scaffold plus .NET notes (solution layout guidance, no commands).",
  "tags": ["monorepo", "dotnet"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "recipe.monorepo.workspace.generic.v1" },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/workspace_dotnet.md", "onConflict": "skip", "text": "# Workspace (.NET)\n\n- Consider a root solution that references apps/libs.\n- Keep shared libraries in libs/.\n- Build/test via Settings.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.monorepo.workspace.node.v1.json") @'
{
  "id": "recipe.monorepo.workspace.node.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 50 },
  "title": "Workspace: Node conventions",
  "description": "Generic workspace scaffold plus Node workspace notes.",
  "tags": ["monorepo", "node"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "recipe.monorepo.workspace.generic.v1" },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/workspace_node.md", "onConflict": "skip", "text": "# Workspace (Node)\n\n- Consider workspaces for shared packages.\n- Keep apps in apps/ and packages/libs in libs/.\n- Scripts and tooling remain Settings-driven.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.monorepo.workspace.python.v1.json") @'
{
  "id": "recipe.monorepo.workspace.python.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 50 },
  "title": "Workspace: Python conventions",
  "description": "Generic workspace scaffold plus Python notes.",
  "tags": ["monorepo", "python"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "recipe.monorepo.workspace.generic.v1" },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/workspace_python.md", "onConflict": "skip", "text": "# Workspace (Python)\n\n- Use src/ style within each python project where preferred.\n- Keep shared libs in libs/.\n- Environment/lint/test stays Settings-driven.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.monorepo.workspace.go.v1.json") @'
{
  "id": "recipe.monorepo.workspace.go.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 50 },
  "title": "Workspace: Go conventions",
  "description": "Generic workspace scaffold plus Go notes.",
  "tags": ["monorepo", "go"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "recipe.monorepo.workspace.generic.v1" },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/workspace_go.md", "onConflict": "skip", "text": "# Workspace (Go)\n\n- Keep shared packages in libs/.\n- Consider a workspace approach if you have multiple modules.\n- Build/test via Settings.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.monorepo.workspace.rust.v1.json") @'
{
  "id": "recipe.monorepo.workspace.rust.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 50 },
  "title": "Workspace: Rust conventions",
  "description": "Generic workspace scaffold plus Rust notes.",
  "tags": ["monorepo", "rust"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "recipe.monorepo.workspace.generic.v1" },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/workspace_rust.md", "onConflict": "skip", "text": "# Workspace (Rust)\n\n- Prefer a workspace to share crates.\n- Keep apps in apps/ and shared crates in libs/.\n- Build/test via Settings.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.monorepo.workspace.java.v1.json") @'
{
  "id": "recipe.monorepo.workspace.java.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 50 },
  "title": "Workspace: Java conventions",
  "description": "Generic workspace scaffold plus Java notes.",
  "tags": ["monorepo", "java"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "recipe.monorepo.workspace.generic.v1" },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/workspace_java.md", "onConflict": "skip", "text": "# Workspace (Java)\n\n- Keep apps and shared libs separated.\n- Consider multi-module layouts.\n- Build/test via Settings.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.monorepo.workspace.cpp.v1.json") @'
{
  "id": "recipe.monorepo.workspace.cpp.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 50 },
  "title": "Workspace: C++ conventions",
  "description": "Generic workspace scaffold plus CMake notes.",
  "tags": ["monorepo", "cpp"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "recipe.monorepo.workspace.generic.v1" },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/workspace_cpp.md", "onConflict": "skip", "text": "# Workspace (C++)\n\n- Prefer a root build orchestration strategy described in docs.\n- Keep shared libraries in libs/.\n- Build/test via Settings.\n" } }
  ] }
}
'@

Write-Host ""
Write-Host "Done seeding batch 13 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

# BlueprintTemplates/Seed-Corpus-Batch14.ps1
# Batch14: Docs-as-a-Product (ADRs, architecture docs, contributing/security, api docs skeletons)
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

Write-Json (Join-Path $bt "atoms\task.docs.scaffold.readme.v1.json") @'
{
  "id": "task.docs.scaffold.readme.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Docs: Scaffold README",
  "description": "Adds a high-quality README skeleton (goals, quickstart placeholders, structure). No commands.",
  "tags": ["docs", "readme"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "docs_scaffold_readme", "inputs": ["projectRoot", "projectName"], "route": "recipe.docs.readme.base.v1" }
}
'@

Write-Json (Join-Path $bt "atoms\task.docs.scaffold.contributing.v1.json") @'
{
  "id": "task.docs.scaffold.contributing.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Docs: Scaffold CONTRIBUTING",
  "description": "Adds contributing guidelines that reference Settings for build/test/lint, with no hardcoded commands.",
  "tags": ["docs", "contributing"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "docs_scaffold_contributing", "inputs": ["projectRoot"], "route": "recipe.docs.contributing.base.v1" }
}
'@

Write-Json (Join-Path $bt "atoms\task.docs.scaffold.security_policy.v1.json") @'
{
  "id": "task.docs.scaffold.security_policy.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Docs: Scaffold SECURITY policy",
  "description": "Adds SECURITY.md and disclosure guidelines. No vendor assumptions.",
  "tags": ["docs", "security"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "docs_scaffold_security", "inputs": ["projectRoot"], "route": "recipe.docs.security.policy.v1" }
}
'@

Write-Json (Join-Path $bt "atoms\task.docs.scaffold_architecture.v1.json") @'
{
  "id": "task.docs.scaffold_architecture.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Docs: Scaffold Architecture",
  "description": "Creates docs/architecture with a system overview, key modules, and boundaries. No commands.",
  "tags": ["docs", "architecture"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "docs_scaffold_architecture", "inputs": ["projectRoot"], "route": "recipe.docs.architecture.base.v1" }
}
'@

Write-Json (Join-Path $bt "atoms\task.docs.scaffold_adr_log.v1.json") @'
{
  "id": "task.docs.scaffold_adr_log.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "Docs: Scaffold ADR log",
  "description": "Creates docs/adr/ and an ADR index plus a template.",
  "tags": ["docs", "adr"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "docs_scaffold_adr", "inputs": ["projectRoot"], "route": "recipe.docs.adr.base.v1" }
}
'@

Write-Json (Join-Path $bt "atoms\task.docs.scaffold_api_reference.v1.json") @'
{
  "id": "task.docs.scaffold_api_reference.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "Docs: Scaffold API reference placeholders",
  "description": "Adds docs/api/ skeleton and notes on how to generate/reference API docs using Settings. No tool assumptions.",
  "tags": ["docs", "api"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "docs_scaffold_api", "inputs": ["projectRoot", "ecosystem"], "routes": {
    "generic": ["recipe.docs.api.generic.v1"],
    "dotnet": ["recipe.docs.api.dotnet.v1"],
    "node": ["recipe.docs.api.node.v1"],
    "python": ["recipe.docs.api.python.v1"],
    "go": ["recipe.docs.api.go.v1"],
    "rust": ["recipe.docs.api.rust.v1"],
    "java": ["recipe.docs.api.java.v1"],
    "cpp": ["recipe.docs.api.cpp.v1"]
  } }
}
'@

Write-Json (Join-Path $bt "atoms\task.docs.scaffold_code_of_conduct.v1.json") @'
{
  "id": "task.docs.scaffold_code_of_conduct.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 4 },
  "title": "Docs: Scaffold Code of Conduct",
  "description": "Adds a friendly Code of Conduct template.",
  "tags": ["docs", "coc"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "docs_scaffold_coc", "inputs": ["projectRoot"], "route": "recipe.docs.coc.base.v1" }
}
'@

# -------------------------
# PACKS (public)
# -------------------------

Write-Json (Join-Path $bt "packs\pack.docs.baseline.v1.json") @'
{
  "id": "pack.docs.baseline.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 175 },
  "title": "Docs: Baseline (README + Contributing + Security + Architecture)",
  "description": "Adds the essential documentation set that makes a repo usable by humans and future-you.",
  "tags": ["docs", "baseline", "readme", "architecture"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.docs.scaffold.readme.v1",
      "task.docs.scaffold.contributing.v1",
      "task.docs.scaffold.security_policy.v1",
      "task.docs.scaffold_architecture.v1",
      "task.docs.scaffold_adr_log.v1"
    ],
    "defaults": { "ecosystem": "generic" }
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.docs.api_reference.v1.json") @'
{
  "id": "pack.docs.api_reference.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 155 },
  "title": "Docs: API Reference Skeleton",
  "description": "Adds docs/api placeholders and instructions to generate API docs via your Settings. No tool assumptions.",
  "tags": ["docs", "api"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.docs.scaffold_api_reference.v1"
    ],
    "defaults": { "ecosystem": "generic" }
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.docs.community.v1.json") @'
{
  "id": "pack.docs.community.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 145 },
  "title": "Docs: Community Files",
  "description": "Adds Code of Conduct and improves repo friendliness.",
  "tags": ["docs", "community"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.docs.scaffold_code_of_conduct.v1",
      "task.docs.scaffold.contributing.v1",
      "task.docs.scaffold.security_policy.v1"
    ]
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.docs.architecture.deepen.v1.json") @'
{
  "id": "pack.docs.architecture.deepen.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 140 },
  "title": "Docs: Architecture Deepen",
  "description": "Adds architecture docs + ADRs and a place to capture decisions.",
  "tags": ["docs", "architecture", "adr"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": [ "task.docs.scaffold_architecture.v1", "task.docs.scaffold_adr_log.v1" ] }
}
'@

# -------------------------
# GRAPHS (public): router
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.docs.v1.json") @'
{
  "id": "graph.router.docs.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: Documentation",
  "description": "Routes documentation requests to baseline/API/community/architecture packs.",
  "tags": ["router", "docs"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Input[docs request] --> Pick{need?}\n  Pick -->|baseline docs| Base[pack.docs.baseline.v1]\n  Pick -->|api reference| Api[pack.docs.api_reference.v1]\n  Pick -->|community files| Comm[pack.docs.community.v1]\n  Pick -->|architecture + adrs| Arch[pack.docs.architecture.deepen.v1]\n  Pick -->|unsure| Base"
  }
}
'@

# -------------------------
# RECIPES
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.docs.readme.base.v1.json") @'
{
  "id": "recipe.docs.readme.base.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 60 },
  "title": "README: Base",
  "description": "High quality README skeleton with placeholders and structure.",
  "tags": ["docs", "readme"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": {
      "path": "${projectRoot}/README.md",
      "onConflict": "skip",
      "text": "# ${projectName}\n\n## What this is\nDescribe the goal in 2-4 sentences.\n\n## Status\n- Stability: <alpha/beta/stable>\n- Supported ecosystems: <list>\n\n## Quickstart\nThis repo intentionally avoids hardcoding commands. Use your Settings to build/test/run.\n\n## Project structure\n- apps/: applications (optional)\n- libs/: shared libraries (optional)\n- docs/: documentation\n\n## Contributing\nSee CONTRIBUTING.md\n\n## Security\nSee SECURITY.md\n"
    } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.docs.contributing.base.v1.json") @'
{
  "id": "recipe.docs.contributing.base.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 55 },
  "title": "CONTRIBUTING: Base",
  "description": "Contributing guidelines that reference Settings for workflow.",
  "tags": ["docs", "contributing"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": {
      "path": "${projectRoot}/CONTRIBUTING.md",
      "onConflict": "skip",
      "text": "# Contributing\n\n## Principles\n- Keep changes minimal and reviewable.\n- Avoid hardcoding environment assumptions.\n\n## Local workflow\nUse your Settings to:\n- build\n- test\n- format/lint\n\n## Pull requests\n- Include rationale\n- Include test coverage notes\n- Prefer small diffs\n"
    } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.docs.security.policy.v1.json") @'
{
  "id": "recipe.docs.security.policy.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 52 },
  "title": "SECURITY: Policy",
  "description": "Security disclosure template and policy.",
  "tags": ["docs", "security"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": {
      "path": "${projectRoot}/SECURITY.md",
      "onConflict": "skip",
      "text": "# Security Policy\n\n## Reporting a vulnerability\nPlease report privately.\n\n## Scope\n- Source code\n- Build artifacts (where applicable)\n- Supply chain concerns\n\n## Response\nWe aim to acknowledge reports promptly and coordinate a fix.\n"
    } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.docs.architecture.base.v1.json") @'
{
  "id": "recipe.docs.architecture.base.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 58 },
  "title": "Architecture: Base",
  "description": "docs/architecture skeleton with overview and boundaries.",
  "tags": ["docs", "architecture"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.ensure_dir.v1", "with": { "path": "${projectRoot}/docs/architecture" } },
    { "use": "task.add_file.conflict_policy.v1", "with": {
      "path": "${projectRoot}/docs/architecture/overview.md",
      "onConflict": "skip",
      "text": "# Architecture Overview\n\n## Goals\n- What the system must do\n\n## Non-goals\n- What it must not do\n\n## Modules\nDescribe key modules and boundaries.\n\n## Data flows\nDescribe major flows at a high level.\n\n## Settings\nDocument the important Settings keys by name (no provider/toolchain IDs).\n"
    } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.docs.adr.base.v1.json") @'
{
  "id": "recipe.docs.adr.base.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 54 },
  "title": "ADR: Base",
  "description": "Creates ADR folder, index, and ADR template.",
  "tags": ["docs", "adr"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.ensure_dir.v1", "with": { "path": "${projectRoot}/docs/adr" } },
    { "use": "task.add_file.conflict_policy.v1", "with": {
      "path": "${projectRoot}/docs/adr/README.md",
      "onConflict": "skip",
      "text": "# Architecture Decision Records\n\n- Keep decisions small and specific.\n- Link to PRs/issues if available.\n"
    } },
    { "use": "task.add_file.conflict_policy.v1", "with": {
      "path": "${projectRoot}/docs/adr/template.md",
      "onConflict": "skip",
      "text": "# ADR: <title>\n\n## Status\nProposed | Accepted | Deprecated\n\n## Context\nWhat problem are we solving?\n\n## Decision\nWhat did we decide?\n\n## Consequences\nWhat do we gain/lose?\n"
    } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.docs.coc.base.v1.json") @'
{
  "id": "recipe.docs.coc.base.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 48 },
  "title": "Code of Conduct: Base",
  "description": "Adds a basic Code of Conduct.",
  "tags": ["docs", "coc"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": {
      "path": "${projectRoot}/CODE_OF_CONDUCT.md",
      "onConflict": "skip",
      "text": "# Code of Conduct\n\nBe respectful.\n\n## Our standards\n- Be kind\n- Assume good intent\n- Keep feedback constructive\n\n## Enforcement\nProject maintainers may moderate content.\n"
    } }
  ] }
}
'@

# API reference recipes: simple placeholders per ecosystem
Write-Json (Join-Path $bt "recipes\recipe.docs.api.generic.v1.json") @'
{
  "id": "recipe.docs.api.generic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 46 },
  "title": "API Docs: Generic placeholder",
  "description": "Adds docs/api placeholder with guidance.",
  "tags": ["docs", "api"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.ensure_dir.v1", "with": { "path": "${projectRoot}/docs/api" } },
    { "use": "task.add_file.conflict_policy.v1", "with": {
      "path": "${projectRoot}/docs/api/README.md",
      "onConflict": "skip",
      "text": "# API Reference\n\nGenerate or curate API docs using your environment Settings.\n\n- Do not hardcode tool commands here.\n- Keep docs versioned with releases.\n"
    } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.docs.api.dotnet.v1.json") @'
{
  "id": "recipe.docs.api.dotnet.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 46 },
  "title": "API Docs: .NET placeholder",
  "description": "Adds docs/api with .NET notes.",
  "tags": ["docs", "api", "dotnet"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "recipe.docs.api.generic.v1" },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/api/dotnet.md", "onConflict": "skip", "text": "# API (.NET)\n\n- Decide doc generation via Settings.\n- Keep output paths configurable.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.docs.api.node.v1.json") @'
{
  "id": "recipe.docs.api.node.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 46 },
  "title": "API Docs: Node placeholder",
  "description": "Adds docs/api with Node notes.",
  "tags": ["docs", "api", "node"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "recipe.docs.api.generic.v1" },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/api/node.md", "onConflict": "skip", "text": "# API (Node)\n\n- Decide doc generation via Settings.\n- Keep runtime configuration external.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.docs.api.python.v1.json") @'
{
  "id": "recipe.docs.api.python.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 46 },
  "title": "API Docs: Python placeholder",
  "description": "Adds docs/api with Python notes.",
  "tags": ["docs", "api", "python"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "recipe.docs.api.generic.v1" },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/api/python.md", "onConflict": "skip", "text": "# API (Python)\n\n- Decide doc generation via Settings.\n- Keep dependency tool selection configurable.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.docs.api.go.v1.json") @'
{
  "id": "recipe.docs.api.go.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 46 },
  "title": "API Docs: Go placeholder",
  "description": "Adds docs/api with Go notes.",
  "tags": ["docs", "api", "go"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "recipe.docs.api.generic.v1" },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/api/go.md", "onConflict": "skip", "text": "# API (Go)\n\n- Decide doc generation via Settings.\n- Keep module/workspace details documented.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.docs.api.rust.v1.json") @'
{
  "id": "recipe.docs.api.rust.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 46 },
  "title": "API Docs: Rust placeholder",
  "description": "Adds docs/api with Rust notes.",
  "tags": ["docs", "api", "rust"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "recipe.docs.api.generic.v1" },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/api/rust.md", "onConflict": "skip", "text": "# API (Rust)\n\n- Decide doc generation via Settings.\n- Keep docs aligned with crate versions.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.docs.api.java.v1.json") @'
{
  "id": "recipe.docs.api.java.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 46 },
  "title": "API Docs: Java placeholder",
  "description": "Adds docs/api with Java notes.",
  "tags": ["docs", "api", "java"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "recipe.docs.api.generic.v1" },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/api/java.md", "onConflict": "skip", "text": "# API (Java)\n\n- Decide doc generation via Settings.\n- Keep module docs consistent.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.docs.api.cpp.v1.json") @'
{
  "id": "recipe.docs.api.cpp.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 46 },
  "title": "API Docs: C++ placeholder",
  "description": "Adds docs/api with C++ notes.",
  "tags": ["docs", "api", "cpp"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "recipe.docs.api.generic.v1" },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/api/cpp.md", "onConflict": "skip", "text": "# API (C++)\n\n- Decide doc generation via Settings.\n- Keep headers/public interfaces documented.\n" } }
  ] }
}
'@

Write-Host ""
Write-Host "Done seeding batch 14 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

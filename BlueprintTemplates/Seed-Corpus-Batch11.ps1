# BlueprintTemplates/Seed-Corpus-Batch11.ps1
# Batch11: repo hygiene + contributor flow + issue/pr templates + code style notes
# Windows PowerShell 5.1 compatible. No provider/toolchain hardcoding. No shell commands.

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

Write-Json (Join-Path $bt "atoms\task.repo.hygiene.scaffold.v1.json") @'
{
  "id": "task.repo.hygiene.scaffold.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Repo Hygiene: Scaffold",
  "description": "Adds docs + templates for PR/issue, contributing, and basic repo hygiene files without assuming toolchain or provider.",
  "tags": ["repo", "hygiene", "contributing", "templates"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "repo_hygiene_scaffold",
    "inputs": ["projectRoot", "ecosystem"],
    "routes": {
      "generic": ["recipe.repo.hygiene.generic.v1"],
      "dotnet": ["recipe.repo.hygiene.dotnet.v1"],
      "node": ["recipe.repo.hygiene.node.v1"],
      "python": ["recipe.repo.hygiene.python.v1"],
      "go": ["recipe.repo.hygiene.go.v1"],
      "rust": ["recipe.repo.hygiene.rust.v1"],
      "java": ["recipe.repo.hygiene.java.v1"],
      "cpp": ["recipe.repo.hygiene.cpp.v1"]
    }
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.repo.detect_and_normalize_layout.v1.json") @'
{
  "id": "task.repo.detect_and_normalize_layout.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Repo: Detect & Normalize Layout",
  "description": "Detects basic project layout and adds missing conventional folders/docs as needed.",
  "tags": ["repo", "layout", "normalize"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "detect_and_normalize_layout",
    "inputs": ["projectRoot"],
    "effects": [
      "ensure docs folder exists",
      "ensure .github templates folder exists when appropriate",
      "ensure basic README exists if missing"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.repo.add_gitignore_by_ecosystem.v1.json") @'
{
  "id": "task.repo.add_gitignore_by_ecosystem.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Repo: Add .gitignore by ecosystem",
  "description": "Adds/merges safe .gitignore defaults based on ecosystem. Avoids destructive replace.",
  "tags": ["repo", "gitignore"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "add_gitignore_by_ecosystem",
    "inputs": ["projectRoot", "ecosystem"],
    "mergePolicy": "append_if_missing_lines",
    "routes": {
      "generic": ["recipe.security.gitignore.sane_defaults.v1"],
      "dotnet": ["recipe.repo.gitignore.dotnet.v1"],
      "node": ["recipe.repo.gitignore.node.v1"],
      "python": ["recipe.repo.gitignore.python.v1"],
      "go": ["recipe.repo.gitignore.go.v1"],
      "rust": ["recipe.repo.gitignore.rust.v1"],
      "java": ["recipe.repo.gitignore.java.v1"],
      "cpp": ["recipe.repo.gitignore.cpp.v1"]
    }
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.repo.add_editorconfig_by_ecosystem.v1.json") @'
{
  "id": "task.repo.add_editorconfig_by_ecosystem.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Repo: Add .editorconfig",
  "description": "Adds .editorconfig for consistent formatting. No toolchain assumptions.",
  "tags": ["repo", "editorconfig", "formatting"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "add_editorconfig",
    "inputs": ["projectRoot", "ecosystem"],
    "route": "recipe.repo.editorconfig.base.v1"
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.repo.add_changelog_strategy.v1.json") @'
{
  "id": "task.repo.add_changelog_strategy.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Repo: Add changelog strategy",
  "description": "Adds changelog conventions and a basic CHANGELOG.md stub if missing.",
  "tags": ["repo", "changelog", "release"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "add_changelog_strategy",
    "inputs": ["projectRoot"],
    "route": "recipe.repo.release.changelog_conventions.v1"
  }
}
'@

# -------------------------
# PACKS (public)
# -------------------------

Write-Json (Join-Path $bt "packs\pack.repo.hygiene.baseline.v1.json") @'
{
  "id": "pack.repo.hygiene.baseline.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 175 },
  "title": "Repo Hygiene: Baseline",
  "description": "Adds sane repo hygiene: README/contributing/issue+PR templates, .gitignore, .editorconfig, and changelog conventions.",
  "tags": ["repo", "hygiene", "contributing", "templates"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.repo.detect_and_normalize_layout.v1",
      "task.detect.project_shape.v1",
      "task.repo.add_gitignore_by_ecosystem.v1",
      "task.repo.add_editorconfig_by_ecosystem.v1",
      "task.repo.add_changelog_strategy.v1",
      "task.repo.hygiene.scaffold.v1"
    ],
    "defaults": { "ecosystem": "generic" }
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.repo.convert.folder_to_repo.v1.json") @'
{
  "id": "pack.repo.convert.folder_to_repo.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 185 },
  "title": "Convert: Folder → Repo (Hygiene + Structure)",
  "description": "Turns a random folder into a shippable repo baseline (docs/templates/ignore/format conventions).",
  "tags": ["repo", "convert", "hygiene"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.detect.project_shape.v1",
      "task.repo.detect_and_normalize_layout.v1",
      "pack.repo.hygiene.baseline.v1"
    ]
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.repo.release.ready.v1.json") @'
{
  "id": "pack.repo.release.ready.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 172 },
  "title": "Repo: Release Ready",
  "description": "Adds release hygiene docs: versioning, changelog, licensing checklist, and publish gating notes.",
  "tags": ["repo", "release", "versioning"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.release.bump_version.v1",
      "task.release.update_changelog.v1",
      "task.release.ensure_license.v1",
      "task.publish.gated.v1",
      "recipe.repo.release.versioning_strategy.v1"
    ]
  }
}
'@

# -------------------------
# GRAPHS (public): router
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.repo_hygiene.v1.json") @'
{
  "id": "graph.router.repo_hygiene.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: Repo Hygiene",
  "description": "Routes repo hygiene / contributing / templates / gitignore requests.",
  "tags": ["router", "repo", "hygiene"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Input[repo hygiene] --> Pick{intent?}\n  Pick -->|baseline| Base[pack.repo.hygiene.baseline.v1]\n  Pick -->|convert folder| Conv[pack.repo.convert.folder_to_repo.v1]\n  Pick -->|release ready| Rel[pack.repo.release.ready.v1]\n  Pick -->|unsure| Base"
  }
}
'@

# -------------------------
# RECIPES (public): hygiene scaffolds
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.repo.editorconfig.base.v1.json") @'
{
  "id": "recipe.repo.editorconfig.base.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 45 },
  "title": "Repo: .editorconfig base",
  "description": "Adds a cross-ecosystem .editorconfig baseline.",
  "tags": ["repo", "editorconfig"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/.editorconfig", "onConflict": "merge_if_possible_else_skip", "text": "root = true\n\n[*]\ncharset = utf-8\nend_of_line = lf\ninsert_final_newline = true\ntrim_trailing_whitespace = true\nindent_style = space\nindent_size = 2\n\n[*.cs]\nindent_size = 4\n\n[*.{json,yml,yaml}]\nindent_size = 2\n\n[*.md]\ntrim_trailing_whitespace = false\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.repo.release.changelog_conventions.v1.json") @'
{
  "id": "recipe.repo.release.changelog_conventions.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 40 },
  "title": "Repo: Changelog conventions",
  "description": "Adds CHANGELOG.md and notes on how to maintain it.",
  "tags": ["repo", "release", "changelog"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/CHANGELOG.md", "onConflict": "skip", "text": "# Changelog\n\nAll notable changes to this project will be documented here.\n\n## Unreleased\n- Added:\n- Changed:\n- Fixed:\n- Security:\n\n## 0.1.0\n- Initial release.\n" } },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/release_process.md", "onConflict": "skip", "text": "# Release Process\n\n1) Update version (project-specific)\n2) Update CHANGELOG.md\n3) Ensure LICENSE is present\n4) Run tests and build\n5) Publish/package via your environment/settings\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.repo.release.versioning_strategy.v1.json") @'
{
  "id": "recipe.repo.release.versioning_strategy.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 35 },
  "title": "Repo: Versioning strategy",
  "description": "Adds a versioning strategy doc (semver-style guidance).",
  "tags": ["repo", "release", "versioning"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/versioning.md", "onConflict": "skip", "text": "# Versioning\n\nRecommended strategy:\n- use semantic versioning (MAJOR.MINOR.PATCH)\n- bump MAJOR for breaking changes\n- bump MINOR for backward-compatible features\n- bump PATCH for backward-compatible bug fixes\n\nExact mechanics depend on your ecosystem and environment/settings.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.repo.hygiene.generic.v1.json") @'
{
  "id": "recipe.repo.hygiene.generic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 55 },
  "title": "Repo Hygiene: Generic",
  "description": "Adds basic README, contributing, code of conduct note, and issue/PR templates.",
  "tags": ["repo", "hygiene", "templates"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/README.md", "onConflict": "skip", "text": "# ${projectName}\n\n## Overview\nDescribe what this project does.\n\n## Quickstart\n- Build/test/run steps depend on your ecosystem and environment settings.\n\n## Contributing\nSee CONTRIBUTING.md.\n" } },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/CONTRIBUTING.md", "onConflict": "skip", "text": "# Contributing\n\n## How to contribute\n- Create a branch\n- Make focused changes\n- Add/adjust tests if applicable\n- Keep changes minimal and well-described\n\n## Commit hygiene\n- clear messages\n- small diffs\n\n## Security\nIf you discover a security issue, follow the SECURITY.md policy.\n" } },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/SECURITY.md", "onConflict": "skip", "text": "# Security Policy\n\n## Reporting\nPlease report security issues privately.\n\n## Supported versions\nDefine supported versions here.\n" } },

    { "use": "task.ensure_dir.v1", "with": { "path": "${projectRoot}/.github/ISSUE_TEMPLATE" } },
    { "use": "task.ensure_dir.v1", "with": { "path": "${projectRoot}/.github" } },

    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/.github/pull_request_template.md", "onConflict": "skip", "text": "## Summary\n\n## Testing\n- [ ] tests run\n- [ ] build succeeds\n\n## Notes\n" } },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/.github/ISSUE_TEMPLATE/bug_report.md", "onConflict": "skip", "text": "---\nname: Bug report\ndescription: Report a bug\n---\n\n## What happened?\n\n## Expected behavior\n\n## Steps to reproduce\n\n## Logs / screenshots\n\n## Environment\n- OS:\n- Version:\n" } },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/.github/ISSUE_TEMPLATE/feature_request.md", "onConflict": "skip", "text": "---\nname: Feature request\ndescription: Propose an improvement\n---\n\n## Problem\n\n## Proposal\n\n## Alternatives\n\n## Risks\n" } }
  ] }
}
'@

# Ecosystem-specific hygiene recipes can layer on generic + add small extras
Write-Json (Join-Path $bt "recipes\recipe.repo.hygiene.dotnet.v1.json") @'
{
  "id": "recipe.repo.hygiene.dotnet.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 50 },
  "title": ".NET Repo Hygiene",
  "description": "Generic hygiene plus dotnet-specific notes (no commands).",
  "tags": ["dotnet", "repo", "hygiene"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "recipe.repo.hygiene.generic.v1" },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/dotnet_notes.md", "onConflict": "skip", "text": "# .NET Notes\n\n- Keep solutions/projects minimal.\n- Prefer deterministic builds when possible.\n- Wire formatting/linting via environment/settings.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.repo.hygiene.node.v1.json") @'
{
  "id": "recipe.repo.hygiene.node.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 50 },
  "title": "Node Repo Hygiene",
  "description": "Generic hygiene plus node notes (no tool assumptions).",
  "tags": ["node", "repo", "hygiene"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "recipe.repo.hygiene.generic.v1" },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/node_notes.md", "onConflict": "skip", "text": "# Node Notes\n\n- Keep dependencies minimal.\n- Prefer lockfiles.\n- Wire lint/format/test via environment/settings.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.repo.hygiene.python.v1.json") @'
{
  "id": "recipe.repo.hygiene.python.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 50 },
  "title": "Python Repo Hygiene",
  "description": "Generic hygiene plus python notes.",
  "tags": ["python", "repo", "hygiene"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "recipe.repo.hygiene.generic.v1" },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/python_notes.md", "onConflict": "skip", "text": "# Python Notes\n\n- Use virtual environments.\n- Pin dependencies.\n- Wire lint/format/test via environment/settings.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.repo.hygiene.go.v1.json") @'
{
  "id": "recipe.repo.hygiene.go.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 50 },
  "title": "Go Repo Hygiene",
  "description": "Generic hygiene plus go notes.",
  "tags": ["go", "repo", "hygiene"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "recipe.repo.hygiene.generic.v1" },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/go_notes.md", "onConflict": "skip", "text": "# Go Notes\n\n- Keep modules tidy.\n- Prefer small packages.\n- Wire test/build via environment/settings.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.repo.hygiene.rust.v1.json") @'
{
  "id": "recipe.repo.hygiene.rust.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 50 },
  "title": "Rust Repo Hygiene",
  "description": "Generic hygiene plus rust notes.",
  "tags": ["rust", "repo", "hygiene"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "recipe.repo.hygiene.generic.v1" },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/rust_notes.md", "onConflict": "skip", "text": "# Rust Notes\n\n- Keep features minimal.\n- Avoid unnecessary dependencies.\n- Wire test/build via environment/settings.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.repo.hygiene.java.v1.json") @'
{
  "id": "recipe.repo.hygiene.java.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 50 },
  "title": "Java Repo Hygiene",
  "description": "Generic hygiene plus java notes.",
  "tags": ["java", "repo", "hygiene"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "recipe.repo.hygiene.generic.v1" },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/java_notes.md", "onConflict": "skip", "text": "# Java Notes\n\n- Keep modules minimal.\n- Prefer clear package structure.\n- Wire test/build via environment/settings.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.repo.hygiene.cpp.v1.json") @'
{
  "id": "recipe.repo.hygiene.cpp.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 50 },
  "title": "C++ Repo Hygiene",
  "description": "Generic hygiene plus CMake notes.",
  "tags": ["cpp", "repo", "hygiene"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "recipe.repo.hygiene.generic.v1" },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/cpp_notes.md", "onConflict": "skip", "text": "# C++ Notes\n\n- Keep targets small.\n- Prefer modern CMake and a clear src/include split.\n- Wire lint/format/test via environment/settings.\n" } }
  ] }
}
'@

# Ecosystem .gitignore add-ons (merged by atom policy)
Write-Json (Join-Path $bt "recipes\recipe.repo.gitignore.dotnet.v1.json") @'
{
  "id": "recipe.repo.gitignore.dotnet.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 35 },
  "title": ".gitignore: .NET add-ons",
  "description": "Adds dotnet-specific ignore lines (safe merge).",
  "tags": ["dotnet", "gitignore"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/.gitignore", "onConflict": "merge_if_possible_else_append", "text": "\n# .NET\nbin/\nobj/\n*.user\n*.suo\n*.cache\n" } }
  ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.repo.gitignore.node.v1.json") @'
{
  "id": "recipe.repo.gitignore.node.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 35 },
  "title": ".gitignore: Node add-ons",
  "description": "Adds node-specific ignore lines (safe merge).",
  "tags": ["node", "gitignore"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/.gitignore", "onConflict": "merge_if_possible_else_append", "text": "\n# Node\nnode_modules/\ndist/\n.nyc_output/\ncoverage/\n" } }
  ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.repo.gitignore.python.v1.json") @'
{
  "id": "recipe.repo.gitignore.python.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 35 },
  "title": ".gitignore: Python add-ons",
  "description": "Adds python-specific ignore lines (safe merge).",
  "tags": ["python", "gitignore"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/.gitignore", "onConflict": "merge_if_possible_else_append", "text": "\n# Python\n__pycache__/\n*.pyc\n.venv/\n.env/\n" } }
  ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.repo.gitignore.go.v1.json") @'
{
  "id": "recipe.repo.gitignore.go.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 35 },
  "title": ".gitignore: Go add-ons",
  "description": "Adds go-specific ignore lines (safe merge).",
  "tags": ["go", "gitignore"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/.gitignore", "onConflict": "merge_if_possible_else_append", "text": "\n# Go\n/bin/\n*.exe\n" } }
  ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.repo.gitignore.rust.v1.json") @'
{
  "id": "recipe.repo.gitignore.rust.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 35 },
  "title": ".gitignore: Rust add-ons",
  "description": "Adds rust-specific ignore lines (safe merge).",
  "tags": ["rust", "gitignore"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/.gitignore", "onConflict": "merge_if_possible_else_append", "text": "\n# Rust\n/target/\n" } }
  ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.repo.gitignore.java.v1.json") @'
{
  "id": "recipe.repo.gitignore.java.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 35 },
  "title": ".gitignore: Java add-ons",
  "description": "Adds java-specific ignore lines (safe merge).",
  "tags": ["java", "gitignore"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/.gitignore", "onConflict": "merge_if_possible_else_append", "text": "\n# Java\n*.class\n/target/\n/build/\n" } }
  ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.repo.gitignore.cpp.v1.json") @'
{
  "id": "recipe.repo.gitignore.cpp.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 35 },
  "title": ".gitignore: C++ add-ons",
  "description": "Adds C++ build ignore lines (safe merge).",
  "tags": ["cpp", "gitignore"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/.gitignore", "onConflict": "merge_if_possible_else_append", "text": "\n# C++\n/build/\n/out/\n*.o\n*.obj\n" } }
  ] }
}
'@

Write-Host ""
Write-Host "Done seeding batch 11 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

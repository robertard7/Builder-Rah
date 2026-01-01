# BlueprintTemplates/Seed-Corpus-Batch7.ps1
# Batch7: maturity-aware routing + matrix CI + dependency triage + security baseline + publish gate.
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
# ATOMS: detect maturity + matrix pipeline + dep triage + security baseline + publish gate
# -------------------------

Write-Json (Join-Path $bt "atoms\task.detect.project_maturity.v1.json") @'
{
  "id": "task.detect.project_maturity.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 4 },
  "title": "Detect Project Maturity",
  "description": "Detect maturity by looking for indicators: tests, CI files, packaging metadata, docs.",
  "tags": ["detect", "maturity"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "detect_project_maturity",
    "inputs": ["projectRoot"],
    "signals": [
      { "ifAnyGlob": "tests/**,test/**", "score": 2 },
      { "ifAnyGlob": ".github/workflows/**,.gitlab-ci.yml,azure-pipelines.yml", "score": 2 },
      { "ifAnyGlob": "CHANGELOG.md,docs/**", "score": 1 },
      { "ifAnyGlob": "LICENSE,NOTICE", "score": 1 },
      { "ifAnyGlob": "pyproject.toml,package.json,pom.xml,Cargo.toml,*.csproj,CMakeLists.txt", "score": 1 }
    ],
    "outputs": ["maturityLevel"],
    "levels": [
      { "name": "new", "maxScore": 1 },
      { "name": "active", "maxScore": 4 },
      { "name": "release", "maxScore": 999 }
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.ci.pipeline.matrix.v1.json") @'
{
  "id": "task.ci.pipeline.matrix.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "CI Pipeline (Matrix)",
  "description": "Run CI steps across a profile matrix (debug/release, lint on/off) using settings-driven executors.",
  "tags": ["ci", "matrix", "pipeline"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "pipeline_matrix",
    "inputs": ["projectRoot"],
    "matrix": {
      "buildProfile": ["debug", "release"],
      "lint": ["on"]
    },
    "steps": [
      "task.detect.project_shape.v1",
      "task.format_lint.${ecosystem}.v1",
      "task.build.${ecosystem}.v1",
      "task.test.${ecosystem}.v1"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.diagnose.dependency_upgrade_triage.v1.json") @'
{
  "id": "task.diagnose.dependency_upgrade_triage.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Diagnose: Dependency Upgrade Triage",
  "description": "Analyze dependency upgrade issues (breaking changes, version conflicts) and propose minimal resolution.",
  "tags": ["diagnose", "dependencies", "upgrade"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "diagnose",
    "subtype": "dependency_upgrade_triage",
    "inputs": ["projectRoot", "logText"],
    "outputs": ["proposedPatch", "riskNotes"]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.security.baseline.v1.json") @'
{
  "id": "task.security.baseline.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Security Baseline (Abstract)",
  "description": "Apply a lightweight security baseline: secrets hygiene hints, dependency audit hooks, and safe defaults. No tool IDs.",
  "tags": ["security", "baseline"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "security_baseline",
    "inputs": ["projectRoot"],
    "checks": [
      { "type": "ensure_gitignore", "notes": "Avoid committing secrets/build output." },
      { "type": "scan_patterns", "patterns": ["BEGIN PRIVATE KEY", "AKIA", "xoxb-", "password="], "notes": "Heuristic only." },
      { "type": "dependency_audit_hook", "notes": "Delegate to configured environment for ecosystem." }
    ],
    "outputs": ["findings"]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.publish.gated.v1.json") @'
{
  "id": "task.publish.gated.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Package/Publish (Gated)",
  "description": "Gate publish behind: clean lint/build/test + optional security baseline + WAIT_USER approval.",
  "tags": ["publish", "gate", "release"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "publish_gated",
    "inputs": ["projectRoot", "releaseVersion"],
    "prechecks": [
      "task.detect.project_shape.v1",
      "task.format_lint.${ecosystem}.v1",
      "task.build.${ecosystem}.v1",
      "task.test.${ecosystem}.v1",
      "task.security.baseline.v1"
    ],
    "waitUser": true,
    "publish": "task.package_publish.${ecosystem}.v1"
  }
}
'@

# -------------------------
# PACKS: matrix CI, dependency upgrade triage, security baseline, publish gated
# -------------------------

Write-Json (Join-Path $bt "packs\pack.ci.pipeline.matrix.v1.json") @'
{
  "id": "pack.ci.pipeline.matrix.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 170 },
  "title": "CI Pipeline (Matrix)",
  "description": "Runs a profile matrix CI (debug/release) using ecosystem detection.",
  "tags": ["ci", "matrix", "pipeline"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["task.ci.pipeline.matrix.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.diagnose.dependency_upgrade.v1.json") @'
{
  "id": "pack.diagnose.dependency_upgrade.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 160 },
  "title": "Diagnose: Dependency Upgrade",
  "description": "Triage dependency upgrade failures, propose minimal patch, WAIT_USER, then verify.",
  "tags": ["diagnose", "dependencies", "upgrade"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.detect.project_shape.v1",
      "task.diagnose.dependency_upgrade_triage.v1",
      "task.apply_patch.verified.v1"
    ]
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.security.baseline.v1.json") @'
{
  "id": "pack.security.baseline.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 155 },
  "title": "Security Baseline",
  "description": "Applies lightweight security hygiene checks and suggests safe defaults.",
  "tags": ["security", "baseline"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["task.security.baseline.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.package.publish.gated.v1.json") @'
{
  "id": "pack.package.publish.gated.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 165 },
  "title": "Package/Publish (Gated)",
  "description": "Runs lint/build/test/security baseline and requires user approval before publishing.",
  "tags": ["publish", "gate", "release"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["task.publish.gated.v1"] }
}
'@

# -------------------------
# GRAPHS: maturity-aware router (new vs active vs release)
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.goal_by_maturity.v1.json") @'
{
  "id": "graph.router.goal_by_maturity.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: Goal by Project Maturity",
  "description": "Routes goals differently for new vs active vs release projects.",
  "tags": ["router", "goal", "maturity"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Goal[User Goal] --> Shape[task.detect.project_shape.v1]\n  Shape --> Maturity[task.detect.project_maturity.v1]\n\n  Maturity -->|new| NewFlow{goal}\n  Maturity -->|active| ActiveFlow{goal}\n  Maturity -->|release| ReleaseFlow{goal}\n\n  NewFlow -->|start project| Start[pack.project.start.wizard.v1]\n  NewFlow -->|add feature| AddF[graph.router.feature_by_shape.v1]\n  NewFlow -->|ci| CI1[pack.ci.pipeline.v1]\n\n  ActiveFlow -->|ci| CI2[pack.ci.pipeline.matrix.v1]\n  ActiveFlow -->|diagnose buildfail| D1[pack.diagnose.classify_and_fix.v1]\n  ActiveFlow -->|upgrade deps| U1[pack.diagnose.dependency_upgrade.v1]\n  ActiveFlow -->|security| S1[pack.security.baseline.v1]\n\n  ReleaseFlow -->|publish| P[pack.package.publish.gated.v1]\n  ReleaseFlow -->|ci| CI3[pack.ci.pipeline.matrix.v1]\n  ReleaseFlow -->|security| S2[pack.security.baseline.v1]\n  ReleaseFlow -->|diagnose| D2[pack.diagnose.classify_and_fix.v1]"
  }
}
'@

# -------------------------
# RECIPES: baseline .gitignore suggestions (generic but helpful)
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.security.gitignore.sane_defaults.v1.json") @'
{
  "id": "recipe.security.gitignore.sane_defaults.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 40 },
  "title": "Security: .gitignore Sane Defaults",
  "description": "Adds a conservative .gitignore for common build outputs and secrets.",
  "tags": ["security", "gitignore"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/.gitignore", "onConflict": "skip", "text": "# build outputs\nbin/\nobj/\ndist/\nbuild/\n\n# logs\n*.log\n\n# env/secrets\n.env\n*.key\n*.pem\n\n# node\nnode_modules/\n\n# python\n__pycache__/\n.venv/\n\n# rust\n/target/\n\n# java\n/target/\n" } }
    ]
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 7 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

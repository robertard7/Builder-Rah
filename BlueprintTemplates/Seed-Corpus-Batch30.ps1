# BlueprintTemplates/Seed-Corpus-Batch30.ps1
# Batch30: Detect & Adapt (neutral): detect ecosystem + layout + entrypoints; map to correct build/test/run targets;
# provide "adaptive" packs/recipes and a router graph.
# No provider/toolchain hardcoding. No raw shell commands. No hardcoded paths. PS 5.1 compatible.

$ErrorActionPreference = "Stop"

function Get-BaseDir {
  if ($PSCommandPath -and (Test-Path -LiteralPath $PSCommandPath)) { return Split-Path -Parent $PSCommandPath }
  if ($PSScriptRoot) { return $PScriptRoot }
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
# ATOMS (internal): detection + normalization + dispatch
# -------------------------

Write-Json (Join-Path $bt "atoms\task.detect.ecosystem_and_entrypoints.v1.json") @'
{
  "id": "task.detect.ecosystem_and_entrypoints.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 8 },
  "title": "Detect: Ecosystem(s) + entrypoints",
  "description": "Scans repo for ecosystem signals (dotnet/node/python/go/rust/java/cpp), project layout, and build/test/run entrypoints without assuming tools.",
  "tags": ["detect", "ecosystem", "entrypoints", "layout"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "detect_ecosystem_and_entrypoints",
    "inputs": ["projectRoot", "hintEcosystem?", "hintProfile?"],
    "outputs": ["detectionReport"],
    "signals": {
      "dotnet": ["*.sln", "*.csproj", "global.json", "Directory.Build.props"],
      "node": ["package.json", "pnpm-lock.yaml", "yarn.lock", "package-lock.json"],
      "python": ["pyproject.toml", "requirements.txt", "setup.py", "Pipfile"],
      "go": ["go.mod", "go.work"],
      "rust": ["Cargo.toml"],
      "java": ["pom.xml"],
      "cpp": ["CMakeLists.txt"]
    },
    "rules": [
      "support multi-ecosystem repos (monorepo) and return a set",
      "identify likely root(s) for builds and tests",
      "record candidate run targets (cli main, web start, service entry) abstractly",
      "do not hardcode paths beyond relative discovery under projectRoot"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.detect.repo_layout_and_conventions.v1.json") @'
{
  "id": "task.detect.repo_layout_and_conventions.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Detect: Repo layout + conventions",
  "description": "Infers layout patterns: monorepo, src/tests, apps/packages, solutions, modules; suggests normalization steps.",
  "tags": ["detect", "layout", "conventions"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "detect_repo_layout_and_conventions",
    "inputs": ["projectRoot", "detectionReport?"],
    "outputs": ["layoutReport"],
    "rules": [
      "detect src/ and tests/ layout when present",
      "detect workspace roots (monorepo) and app subfolders",
      "avoid destructive moves; propose safe normalization plans only"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.detect.project_maturity_and_risk.v1.json") @'
{
  "id": "task.detect.project_maturity_and_risk.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Detect: Maturity + risk profile",
  "description": "Scores project maturity (prototype/standard/production) and recommends which packs/routes to apply.",
  "tags": ["detect", "maturity", "risk"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "detect_project_maturity_and_risk",
    "inputs": ["projectRoot", "detectionReport", "layoutReport?"],
    "outputs": ["maturityReport"],
    "signals": [
      "tests presence and coverage hints",
      "ci files presence",
      "docs presence (README/SECURITY/CONTRIBUTING)",
      "release artifacts or publish config presence",
      "observability hints (logging/metrics placeholders)"
    ],
    "rules": [
      "never assume cloud vendor",
      "use conservative defaults; if uncertain, request minimal clarification"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.detect.wait_user.ambiguities.v1.json") @'
{
  "id": "task.detect.wait_user.ambiguities.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Detect: Ask user to resolve ambiguities (WAIT_USER)",
  "description": "If detection yields multiple plausible targets (e.g., multiple apps), asks minimal questions to choose safely.",
  "tags": ["detect", "wait_user", "ambiguity"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "emit_wait_user_questions",
    "inputs": ["detectionReport", "layoutReport", "goalVerb?"],
    "outputs": ["questions", "waitUser"],
    "waitUser": true,
    "questionRules": [
      "ask only what is necessary to pick a target: which app, which package, which profile (cli/web/api/gui)",
      "prefer offering discovered choices rather than open-ended questions",
      "never ask provider/toolchain/model ids"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.adapt.map_goal_to_tasks.v1.json") @'
{
  "id": "task.adapt.map_goal_to_tasks.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 8 },
  "title": "Adapt: Map goal verb to ecosystem-specific tasks",
  "description": "Translates a user goal (build/test/run/format/publish/etc.) into the correct ecosystem-specific atom ids.",
  "tags": ["adapt", "routing", "verbs"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "map_goal_to_tasks",
    "inputs": ["goalVerb", "detectionReport", "preferredEcosystem?", "preferredProfile?"],
    "outputs": ["taskPlan"],
    "mappingRules": [
      "if ecosystem is clear, pick task.<verb>.<ecosystem>.*.v1 when available",
      "fall back to task.<verb>.generic.v1 when ecosystem-specific is missing",
      "for multi-ecosystem, prefer the ecosystem aligned to the user's goal/profile",
      "never invent ids; only reference ids that exist in manifest"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.adapt.execute_goal_plan.v1.json") @'
{
  "id": "task.adapt.execute_goal_plan.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Adapt: Execute goal plan (abstract)",
  "description": "Executes the mapped goal plan using existing atoms (build/test/run/format/publish/diagnose) without embedding commands here.",
  "tags": ["adapt", "execute", "plan"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "execute_task_plan",
    "inputs": ["taskPlan", "projectRoot", "selection?"],
    "outputs": ["result"],
    "rules": [
      "respect WAIT_USER nodes if required",
      "prefer minimal steps; stop on error and route to diagnose pack",
      "do not alter provider/toolchain settings"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.adapt.on_fail.route_to_diagnose.v1.json") @'
{
  "id": "task.adapt.on_fail.route_to_diagnose.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Adapt: On failure, route to diagnose",
  "description": "If build/test/run/publish fails, captures evidence and routes to the diagnose unstoppable pack/graph.",
  "tags": ["adapt", "diagnose", "triage"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "route_on_failure",
    "inputs": ["goalVerb", "result", "projectRoot"],
    "outputs": ["nextPackId", "evidenceBundle"],
    "rules": [
      "prefer pack.diagnose.unstoppable.v1 when present",
      "include stage hint when possible (restore/compile/test/run/publish/format)",
      "avoid destructive changes; propose minimal fixes"
    ]
  }
}
'@

# -------------------------
# RECIPES (public): detect & adapt flows
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.detect_and_adapt.goal.v1.json") @'
{
  "id": "recipe.detect_and_adapt.goal.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 90 },
  "title": "Detect & Adapt: Execute a goal in any repo",
  "description": "Detects ecosystem/layout, resolves ambiguities via WAIT_USER, maps goal verb to correct tasks, executes, and routes failures to diagnose.",
  "tags": ["detect", "adapt", "goal", "unstoppable"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.detect.ecosystem_and_entrypoints.v1" },
    { "use": "task.detect.repo_layout_and_conventions.v1" },
    { "use": "task.detect.project_maturity_and_risk.v1" },
    { "use": "task.detect.wait_user.ambiguities.v1" },
    { "use": "task.adapt.map_goal_to_tasks.v1" },
    { "use": "task.adapt.execute_goal_plan.v1" },
    { "use": "task.adapt.on_fail.route_to_diagnose.v1" }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.detect_and_adapt.repo_intake.v1.json") @'
{
  "id": "recipe.detect_and_adapt.repo_intake.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 78 },
  "title": "Detect & Adapt: Repo intake (no execution)",
  "description": "Detects ecosystem/layout/maturity and produces a concise plan of recommended packs to apply next.",
  "tags": ["detect", "intake", "plan_only"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.detect.ecosystem_and_entrypoints.v1" },
    { "use": "task.detect.repo_layout_and_conventions.v1" },
    { "use": "task.detect.project_maturity_and_risk.v1" }
  ] }
}
'@

# -------------------------
# PACKS (public): user-facing entrypoints
# -------------------------

Write-Json (Join-Path $bt "packs\pack.detect_and_adapt.goal_runner.v1.json") @'
{
  "id": "pack.detect_and_adapt.goal_runner.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 210 },
  "title": "Detect & Adapt: Run any goal (build/test/run/format/publish)",
  "description": "Works on almost any repo: detect, resolve ambiguity, map goal to tasks, execute, diagnose if needed.",
  "tags": ["detect", "adapt", "goal", "unstoppable"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": ["recipe.detect_and_adapt.goal.v1"],
    "inputs": {
      "goalVerb": "one of: build/test/run/format_lint/package_publish/diagnose_build_fail_triage/create_project/add_file/replace_file/apply_patch",
      "hintEcosystem?": "optional ecosystem hint",
      "hintProfile?": "optional profile hint (cli/web/api/gui/library)"
    }
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.detect_and_adapt.repo_intake.v1.json") @'
{
  "id": "pack.detect_and_adapt.repo_intake.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 160 },
  "title": "Detect & Adapt: Repo intake",
  "description": "Analyzes a repo and recommends next packs (docs, hygiene, tests, security, env) without executing changes.",
  "tags": ["detect", "intake", "analysis"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.detect_and_adapt.repo_intake.v1"] }
}
'@

# -------------------------
# GRAPHS (public): routing for detect/adapt
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.detect_and_adapt.unstoppable.v1.json") @'
{
  "id": "graph.router.detect_and_adapt.unstoppable.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: Detect & Adapt (unstoppable)",
  "description": "Routes generic intents to detect/adapt packs, then to diagnose on failures.",
  "tags": ["router", "detect", "adapt", "unstoppable"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  U[User intent] --> D{Need repo analysis only?}\n  D -->|yes| Intake[pack.detect_and_adapt.repo_intake.v1]\n  D -->|no, execute goal| Goal[pack.detect_and_adapt.goal_runner.v1]\n  Goal -->|success| Done[Done]\n  Goal -->|fail| Diag[pack.diagnose.unstoppable.v1]\n  Intake --> Done\n  Diag --> Done"
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 30 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

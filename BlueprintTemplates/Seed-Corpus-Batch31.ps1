# BlueprintTemplates/Seed-Corpus-Batch31.ps1
# Batch31: Patch + Refactor Safety Rails (neutral)
# Adds internal atoms for patch preflight/budgets/safety, plus public recipes/packs and router graph.
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
# ATOMS (internal): safety rails around patch/refactor
# -------------------------

Write-Json (Join-Path $bt "atoms\task.patch.preflight.scan_targets.v1.json") @'
{
  "id": "task.patch.preflight.scan_targets.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 8 },
  "title": "Patch Preflight: Scan targets and classify change risk",
  "description": "Validates patch targets exist, classifies file types (code/config/docs), detects generated files and lockfiles, and flags high-risk targets.",
  "tags": ["patch", "preflight", "safety", "risk"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "patch_preflight_scan_targets",
    "inputs": ["projectRoot", "patchPlan", "allowGenerated?"],
    "outputs": ["preflightReport"],
    "rules": [
      "never patch binaries or vendor folders unless explicitly allowed",
      "treat lockfiles as sensitive; only change if patchPlan explicitly includes them",
      "prefer minimal surface area: smallest file set that achieves goal"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.patch.define_change_budget.v1.json") @'
{
  "id": "task.patch.define_change_budget.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Patch Safety: Define change budget",
  "description": "Defines a hard budget on number of files/lines changed and prohibits broad rewrites unless user explicitly requests.",
  "tags": ["patch", "budget", "safety"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "define_change_budget",
    "inputs": ["goalVerb", "maturityReport?", "patchPlan?"],
    "outputs": ["changeBudget"],
    "defaults": {
      "maxFilesChanged": 6,
      "maxTotalLinesChanged": 250,
      "maxSingleFileLinesChanged": 180,
      "allowRenameMove": false,
      "allowLockfileChange": false
    },
    "rules": [
      "if goal is simple (build fix), keep budget tight",
      "if goal is refactor, still incremental: prefer staged refactors",
      "if budget would be exceeded, trigger WAIT_USER with options"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.patch.plan_surgical_patch.v1.json") @'
{
  "id": "task.patch.plan_surgical_patch.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 8 },
  "title": "Patch Plan: Surgical patch plan",
  "description": "Creates a minimal patch plan (add/replace/apply_patch) constrained by change budget and preflight risk flags.",
  "tags": ["patch", "plan", "surgical"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "plan_surgical_patch",
    "inputs": ["projectRoot", "goal", "evidenceBundle?", "preflightReport?", "changeBudget"],
    "outputs": ["patchPlan"],
    "rules": [
      "prefer add_file over replace_file when possible",
      "prefer patch hunks over whole-file replacement",
      "avoid formatting-only changes unless goalVerb is format_lint",
      "never invent tool ids; use existing generic file/patch atoms if needed"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.patch.wait_user.approve_budget_or_scope.v1.json") @'
{
  "id": "task.patch.wait_user.approve_budget_or_scope.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Patch Safety: Ask user to approve budget/scope (WAIT_USER)",
  "description": "If patch plan exceeds budget or touches sensitive files, asks user to approve or narrow scope.",
  "tags": ["patch", "wait_user", "budget"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "emit_wait_user_questions",
    "inputs": ["preflightReport", "changeBudget", "patchPlan"],
    "outputs": ["questions", "waitUser"],
    "waitUser": true,
    "questionRules": [
      "offer options: narrow scope / increase budget / allow lockfile / allow rename-move",
      "ask for confirmation when touching lockfiles, generated files, or wide refactors",
      "keep questions minimal and specific"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.patch.apply_with_receipts.v1.json") @'
{
  "id": "task.patch.apply_with_receipts.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 8 },
  "title": "Patch Apply: Apply patch with receipts",
  "description": "Applies the surgical patch plan using existing file/patch atoms and produces receipts (what changed, why, evidence).",
  "tags": ["patch", "apply", "receipts"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "apply_patch_with_receipts",
    "inputs": ["projectRoot", "patchPlan", "changeBudget"],
    "outputs": ["patchReceipts"],
    "receipts": {
      "include": ["filesChanged", "lineCounts", "rationale", "linksToEvidence", "riskFlags"]
    },
    "rules": [
      "do not exceed approved budget",
      "if any apply fails, stop and route to diagnose",
      "ensure receipts are deterministic and human-readable"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.refactor.safe_rename_move.plan.v1.json") @'
{
  "id": "task.refactor.safe_rename_move.plan.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Refactor Plan: Safe rename/move plan",
  "description": "Plans a safe rename/move refactor with references update and a verification checklist (build/test).",
  "tags": ["refactor", "rename", "move", "plan"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "plan_safe_rename_move",
    "inputs": ["projectRoot", "renameMap", "detectionReport?"],
    "outputs": ["refactorPlan"],
    "rules": [
      "plan must include reference updates across code/config/docs",
      "require verification steps after rename/move",
      "avoid large moves unless user explicitly requests"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.refactor.extract_abstraction.plan.v1.json") @'
{
  "id": "task.refactor.extract_abstraction.plan.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Refactor Plan: Extract abstraction plan",
  "description": "Plans extracting an interface/trait/module with minimal behavior change and tests or golden checks.",
  "tags": ["refactor", "abstraction", "plan"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "plan_extract_abstraction",
    "inputs": ["projectRoot", "targetArea", "goal", "changeBudget"],
    "outputs": ["refactorPlan"],
    "rules": [
      "no functional changes unless explicitly needed",
      "prefer adding tests or characterization checks first",
      "keep plan incremental"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.patch.validate_post_apply.v1.json") @'
{
  "id": "task.patch.validate_post_apply.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 8 },
  "title": "Patch Validate: Post-apply validation",
  "description": "Runs the appropriate validate steps (build/test/run/format) using existing atoms and routes failures to diagnose.",
  "tags": ["patch", "validate", "diagnose"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "validate_post_apply",
    "inputs": ["projectRoot", "goalVerb", "detectionReport?", "selection?"],
    "outputs": ["validationResult", "evidenceBundle?"],
    "rules": [
      "use ecosystem-specific atoms when available, else generic",
      "if validation fails, collect evidence and route to diagnose packs/graphs"
    ]
  }
}
'@

# -------------------------
# RECIPES (public): patch/refactor safe flows
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.patch.surgical_apply_and_validate.v1.json") @'
{
  "id": "recipe.patch.surgical_apply_and_validate.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 92 },
  "title": "Surgical Patch: Preflight -> Budget -> Apply -> Validate",
  "description": "Applies minimal safe patches with receipts and validates (build/test/run/format) afterward. Asks user only when scope is risky.",
  "tags": ["patch", "surgical", "safe", "validate"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.detect.ecosystem_and_entrypoints.v1" },
    { "use": "task.patch.preflight.scan_targets.v1" },
    { "use": "task.patch.define_change_budget.v1" },
    { "use": "task.patch.plan_surgical_patch.v1" },
    { "use": "task.patch.wait_user.approve_budget_or_scope.v1" },
    { "use": "task.patch.apply_with_receipts.v1" },
    { "use": "task.patch.validate_post_apply.v1" }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.refactor.safe_rename_move.surgical.v1.json") @'
{
  "id": "recipe.refactor.safe_rename_move.surgical.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 80 },
  "title": "Refactor: Safe rename/move (staged)",
  "description": "Plans a safe rename/move, applies it within budget, then validates. Avoids broad moves unless approved.",
  "tags": ["refactor", "rename", "move", "safe"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.detect.ecosystem_and_entrypoints.v1" },
    { "use": "task.patch.define_change_budget.v1" },
    { "use": "task.refactor.safe_rename_move.plan.v1" },
    { "use": "task.patch.wait_user.approve_budget_or_scope.v1" },
    { "use": "task.patch.apply_with_receipts.v1" },
    { "use": "task.patch.validate_post_apply.v1" }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.refactor.extract_abstraction.surgical.v1.json") @'
{
  "id": "recipe.refactor.extract_abstraction.surgical.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 78 },
  "title": "Refactor: Extract abstraction (staged)",
  "description": "Extracts an interface/trait/module incrementally with minimal behavior change and validation.",
  "tags": ["refactor", "abstraction", "safe", "staged"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.detect.ecosystem_and_entrypoints.v1" },
    { "use": "task.patch.define_change_budget.v1" },
    { "use": "task.refactor.extract_abstraction.plan.v1" },
    { "use": "task.patch.wait_user.approve_budget_or_scope.v1" },
    { "use": "task.patch.apply_with_receipts.v1" },
    { "use": "task.patch.validate_post_apply.v1" }
  ] }
}
'@

# -------------------------
# PACKS (public)
# -------------------------

Write-Json (Join-Path $bt "packs\pack.patch.surgical_apply.v1.json") @'
{
  "id": "pack.patch.surgical_apply.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 230 },
  "title": "Surgical Patch Apply (safe rails)",
  "description": "Preflight + change budget + minimal patch + receipts + post-apply validation. Refuses broad rewrites without explicit approval.",
  "tags": ["patch", "safe", "surgical", "unstoppable"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": ["recipe.patch.surgical_apply_and_validate.v1"],
    "inputs": {
      "goalVerb": "apply_patch or build/test/run/format_lint/publish fix",
      "patchPlan?": "optional patch plan hints",
      "allowGenerated?": "optional boolean"
    }
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.refactor.safe_rename_move.v2.json") @'
{
  "id": "pack.refactor.safe_rename_move.v2",
  "kind": "pack",
  "version": 2,
  "meta": { "visibility": "public", "priority": 170 },
  "title": "Refactor: Safe rename/move (rails)",
  "description": "Staged rename/move with budget approval and validation.",
  "tags": ["refactor", "rename", "move", "safe"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.refactor.safe_rename_move.surgical.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.refactor.extract_abstraction.v2.json") @'
{
  "id": "pack.refactor.extract_abstraction.v2",
  "kind": "pack",
  "version": 2,
  "meta": { "visibility": "public", "priority": 165 },
  "title": "Refactor: Extract abstraction (rails)",
  "description": "Staged extraction of interfaces/traits/modules with minimal behavior change.",
  "tags": ["refactor", "abstraction", "safe"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.refactor.extract_abstraction.surgical.v1"] }
}
'@

# -------------------------
# GRAPH (public): router for patch/refactor safety rails
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.patch_and_refactor_rails.v1.json") @'
{
  "id": "graph.router.patch_and_refactor_rails.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: Patch + Refactor safety rails",
  "description": "Routes patch/refactor intents through safe rails (preflight, budget, receipts, validate) and diagnose on failure.",
  "tags": ["router", "patch", "refactor", "safety"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  U[User intent: patch/refactor] --> C{Intent type}\n  C -->|apply patch or fix build| P[pack.patch.surgical_apply.v1]\n  C -->|rename/move| R1[pack.refactor.safe_rename_move.v2]\n  C -->|extract abstraction| R2[pack.refactor.extract_abstraction.v2]\n  P -->|success| Done[Done]\n  R1 -->|success| Done\n  R2 -->|success| Done\n  P -->|fail| Diag[pack.diagnose.unstoppable.v1]\n  R1 -->|fail| Diag\n  R2 -->|fail| Diag\n  Diag --> Done"
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 31 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

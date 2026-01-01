# BlueprintTemplates/Seed-Corpus-Batch15.ps1
# Batch15: Unstoppable Diagnose upgrades (evidence capture, classify, minimal-fix, wait-user gates)
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
# ATOMS (internal): evidence + classification + safe fix loop
# -------------------------

Write-Json (Join-Path $bt "atoms\task.diagnose.collect_evidence.v1.json") @'
{
  "id": "task.diagnose.collect_evidence.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Diagnose: Collect evidence bundle",
  "description": "Collects structured evidence: errors, logs, layout, environment hints. No commands; orchestrator supplies data sources.",
  "tags": ["diagnose", "evidence", "triage"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "collect_evidence",
    "inputs": ["projectRoot", "lastErrorText?", "lastLogText?", "changedFiles?", "runtimeContext?"],
    "outputs": ["evidenceBundle"],
    "bundleSchema": {
      "summary": "string",
      "errorFingerprint": "string",
      "suspectedStage": "restore|compile|test|run|publish|format|unknown",
      "keyPaths": "array",
      "snippets": "array",
      "nextQuestions": "array"
    }
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.diagnose.classify_stage.v1.json") @'
{
  "id": "task.diagnose.classify_stage.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Diagnose: Classify failure stage",
  "description": "Classifies evidence into restore/compile/test/run/publish/format buckets.",
  "tags": ["diagnose", "classify"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "classify_failure_stage",
    "inputs": ["evidenceBundle"],
    "outputs": ["stage", "confidence", "reasons"]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.diagnose.ask_user_for_missing_info.v1.json") @'
{
  "id": "task.diagnose.ask_user_for_missing_info.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Diagnose: Ask user for missing info (WAIT_USER)",
  "description": "Produces a minimal question set when evidence is insufficient. Intended to trigger WAIT_USER.",
  "tags": ["diagnose", "wait_user"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "emit_wait_user_questions",
    "inputs": ["evidenceBundle", "stage?"],
    "outputs": ["questions", "waitUser": true],
    "questionRules": [
      "ask only for missing evidence needed to decide next safe step",
      "prefer file paths, exact errors, and minimal reproduction",
      "never ask for provider/model/toolchain ids"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.diagnose.propose_minimal_fix.v1.json") @'
{
  "id": "task.diagnose.propose_minimal_fix.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Diagnose: Propose minimal fix",
  "description": "Generates a minimal safe change plan (file edits/patches) based on evidence and stage.",
  "tags": ["diagnose", "fix", "minimal"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "propose_minimal_fix",
    "inputs": ["projectRoot", "stage", "evidenceBundle"],
    "outputs": ["fixPlan"],
    "fixPlanSchema": {
      "intent": "string",
      "risk": "low|medium|high",
      "changes": "array",
      "rollbackHint": "string",
      "validation": "array"
    },
    "constraints": [
      "no hardcoded paths; use variables",
      "no provider/toolchain assumptions",
      "prefer apply_patch over large rewrites",
      "keep diffs small"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.diagnose.apply_fix_and_validate.v1.json") @'
{
  "id": "task.diagnose.apply_fix_and_validate.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Diagnose: Apply fix and validate",
  "description": "Applies fixPlan using generic patch primitives, then validates by rerunning stage-appropriate tasks (abstract).",
  "tags": ["diagnose", "apply", "validate"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "apply_fix_and_validate",
    "inputs": ["projectRoot", "fixPlan", "stage", "ecosystem?"],
    "uses": [
      "task.apply_patch.generic.v1",
      "task.add_file.generic.v1",
      "task.replace_file.generic.v1"
    ],
    "validationRoutes": {
      "restore": ["task.build.${ecosystem}.v1"],
      "compile": ["task.build.${ecosystem}.v1"],
      "test": ["task.test.${ecosystem}.v1"],
      "run": ["task.run.${ecosystem}.v1"],
      "publish": ["task.package_publish.${ecosystem}.v1"],
      "format": ["task.format_lint.${ecosystem}.v1"]
    }
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.diagnose.loop_until_green_or_wait.v1.json") @'
{
  "id": "task.diagnose.loop_until_green_or_wait.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Diagnose: Loop until green or WAIT_USER",
  "description": "Repeats classify -> minimal fix -> validate until success or insufficient evidence triggers WAIT_USER.",
  "tags": ["diagnose", "loop"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "diagnose_loop",
    "inputs": ["projectRoot", "ecosystem?", "maxIters?"],
    "defaultMaxIters": 3,
    "uses": [
      "task.diagnose.collect_evidence.v1",
      "task.diagnose.classify_stage.v1",
      "task.diagnose.propose_minimal_fix.v1",
      "task.diagnose.apply_fix_and_validate.v1",
      "task.diagnose.ask_user_for_missing_info.v1"
    ]
  }
}
'@

# -------------------------
# RECIPES: minimal-fix playbooks (abstract) per stage
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.diagnose.minimal_fix.restore.v1.json") @'
{
  "id": "recipe.diagnose.minimal_fix.restore.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 65 },
  "title": "Diagnose: Restore minimal fix playbook",
  "description": "Evidence-driven restore failure triage and minimal remediation plan (no commands).",
  "tags": ["diagnose", "restore"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.diagnose.collect_evidence.v1" },
    { "use": "task.diagnose.classify_stage.v1" },
    { "use": "task.diagnose.propose_minimal_fix.v1", "with": { "stage": "restore" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.diagnose.minimal_fix.compile.v1.json") @'
{
  "id": "recipe.diagnose.minimal_fix.compile.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 65 },
  "title": "Diagnose: Compile minimal fix playbook",
  "description": "Evidence-driven compile failure triage and minimal remediation plan (no commands).",
  "tags": ["diagnose", "compile"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.diagnose.collect_evidence.v1" },
    { "use": "task.diagnose.classify_stage.v1" },
    { "use": "task.diagnose.propose_minimal_fix.v1", "with": { "stage": "compile" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.diagnose.minimal_fix.test.v1.json") @'
{
  "id": "recipe.diagnose.minimal_fix.test.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 62 },
  "title": "Diagnose: Test minimal fix playbook",
  "description": "Evidence-driven test failure triage and minimal remediation plan (no commands).",
  "tags": ["diagnose", "test"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.diagnose.collect_evidence.v1" },
    { "use": "task.diagnose.classify_stage.v1" },
    { "use": "task.diagnose.propose_minimal_fix.v1", "with": { "stage": "test" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.diagnose.minimal_fix.run.v1.json") @'
{
  "id": "recipe.diagnose.minimal_fix.run.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 60 },
  "title": "Diagnose: Run minimal fix playbook",
  "description": "Evidence-driven runtime failure triage and minimal remediation plan (no commands).",
  "tags": ["diagnose", "run"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.diagnose.collect_evidence.v1" },
    { "use": "task.diagnose.classify_stage.v1" },
    { "use": "task.diagnose.propose_minimal_fix.v1", "with": { "stage": "run" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.diagnose.minimal_fix.publish.v1.json") @'
{
  "id": "recipe.diagnose.minimal_fix.publish.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 60 },
  "title": "Diagnose: Publish minimal fix playbook",
  "description": "Evidence-driven publish failure triage and minimal remediation plan (no commands).",
  "tags": ["diagnose", "publish"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.diagnose.collect_evidence.v1" },
    { "use": "task.diagnose.classify_stage.v1" },
    { "use": "task.diagnose.propose_minimal_fix.v1", "with": { "stage": "publish" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.diagnose.minimal_fix.format.v1.json") @'
{
  "id": "recipe.diagnose.minimal_fix.format.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 58 },
  "title": "Diagnose: Format/Lint minimal fix playbook",
  "description": "Evidence-driven lint/format failure triage and minimal remediation plan (no commands).",
  "tags": ["diagnose", "format", "lint"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.diagnose.collect_evidence.v1" },
    { "use": "task.diagnose.classify_stage.v1" },
    { "use": "task.diagnose.propose_minimal_fix.v1", "with": { "stage": "format" } }
  ] }
}
'@

# -------------------------
# PACKS (public): unstoppable diagnose flows
# -------------------------

Write-Json (Join-Path $bt "packs\pack.diagnose.unstoppable.v1.json") @'
{
  "id": "pack.diagnose.unstoppable.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 190 },
  "title": "Diagnose: Unstoppable (classify -> minimal fix -> validate -> WAIT_USER)",
  "description": "Collects evidence, classifies failure stage, proposes minimal fix, validates, repeats up to 3 iterations, otherwise asks targeted questions (WAIT_USER).",
  "tags": ["diagnose", "unstoppable", "triage", "fix"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.diagnose.loop_until_green_or_wait.v1"
    ],
    "defaults": { "maxIters": 3 }
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.diagnose.minimal_fix.by_stage.v1.json") @'
{
  "id": "pack.diagnose.minimal_fix.by_stage.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 165 },
  "title": "Diagnose: Minimal Fix (by stage)",
  "description": "Routes to a stage-specific minimal-fix playbook for restore/compile/test/run/publish/format failures.",
  "tags": ["diagnose", "minimal", "route"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "routes": {
      "restore": "recipe.diagnose.minimal_fix.restore.v1",
      "compile": "recipe.diagnose.minimal_fix.compile.v1",
      "test": "recipe.diagnose.minimal_fix.test.v1",
      "run": "recipe.diagnose.minimal_fix.run.v1",
      "publish": "recipe.diagnose.minimal_fix.publish.v1",
      "format": "recipe.diagnose.minimal_fix.format.v1"
    },
    "uses": [
      "task.diagnose.collect_evidence.v1",
      "task.diagnose.classify_stage.v1"
    ]
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.diagnose.wait_user.questions_only.v1.json") @'
{
  "id": "pack.diagnose.wait_user.questions_only.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 140 },
  "title": "Diagnose: Ask only what’s missing (WAIT_USER)",
  "description": "When the system can’t decide safely, it asks a minimal set of targeted questions and waits.",
  "tags": ["diagnose", "wait_user"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": [ "task.diagnose.collect_evidence.v1", "task.diagnose.ask_user_for_missing_info.v1" ] }
}
'@

# -------------------------
# GRAPHS (public): routers + orchestrator pattern
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.diagnose.unstoppable.v1.json") @'
{
  "id": "graph.router.diagnose.unstoppable.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: Diagnose Unstoppable",
  "description": "Routes build failures to unstoppable diagnose loop or stage playbooks.",
  "tags": ["router", "diagnose", "unstoppable"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Fail[build/test/run/publish failed] --> Ev[task.diagnose.collect_evidence.v1]\n  Ev --> Cl[task.diagnose.classify_stage.v1]\n  Cl -->|high confidence| ByStage[pack.diagnose.minimal_fix.by_stage.v1]\n  Cl -->|low confidence| Unstop[pack.diagnose.unstoppable.v1]\n  ByStage --> MaybeFix{need fix?}\n  MaybeFix -->|yes| Unstop\n  MaybeFix -->|no| Wait[pack.diagnose.wait_user.questions_only.v1]"
  }
}
'@

Write-Json (Join-Path $bt "graphs\graph.orchestrator.unstoppable_minimal_fix_wait_user.v1.json") @'
{
  "id": "graph.orchestrator.unstoppable_minimal_fix_wait_user.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Orchestrator: Diagnose (evidence -> fix -> validate -> WAIT_USER)",
  "description": "Orchestrator flow: collect evidence, plan minimal fix, apply, validate, loop; if insufficient evidence, WAIT_USER.",
  "tags": ["orchestrator", "diagnose", "wait_user"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Start[Start] --> Ev[task.diagnose.collect_evidence.v1]\n  Ev --> Cl[task.diagnose.classify_stage.v1]\n  Cl --> Plan[task.diagnose.propose_minimal_fix.v1]\n  Plan --> Apply[task.diagnose.apply_fix_and_validate.v1]\n  Apply --> Check{green?}\n  Check -->|yes| Done[Done]\n  Check -->|no and iter left| Ev\n  Check -->|no and insufficient evidence| Ask[task.diagnose.ask_user_for_missing_info.v1]\n  Ask --> WAIT[WAIT_USER]"
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 15 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

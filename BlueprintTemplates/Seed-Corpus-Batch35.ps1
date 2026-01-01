# BlueprintTemplates/Seed-Corpus-Batch35.ps1
# Batch35: Tool prompt registry + tool id validation hardening (neutral)
# Ensures: Tools without prompts do not exist. Emits WAIT_USER with missing tool prompt IDs.
# No provider/toolchain hardcoding. No hardcoded paths. PS 5.1 compatible.

$ErrorActionPreference = "Stop"

function Get-BaseDir {
  if ($PSCommandPath -and (Test-Path -LiteralPath $PSCommandPath)) { return Split-Path -Parent $PSCommandPath }
  if ($PSScriptRoot) { return $PSScriptRoot }
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
# ATOMS (internal): collect referenced tool ids -> validate against prompt registry -> WAIT_USER if missing
# -------------------------

Write-Json (Join-Path $bt "atoms\task.tools.collect_references_from_plan.v1.json") @'
{
  "id": "task.tools.collect_references_from_plan.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 9 },
  "title": "Tools: Collect referenced tool IDs from a plan",
  "description": "Extracts tool IDs referenced by a plan (packs/recipes/atoms/graphs) from declared toolRefs or inferred usage metadata. Produces a deduplicated list with source traces.",
  "tags": ["tools", "prompts", "validate", "collect"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "collect_tool_references",
    "inputs": ["planContext"],
    "outputs": ["toolRefReport"],
    "toolRefReportShape": {
      "fields": [
        "toolIds[]",
        "toolRefs[]",
        "notes[]"
      ]
    },
    "toolRefShape": {
      "fields": [
        "toolId",
        "sourceId",
        "sourceKind",
        "pathHint?",
        "reason?"
      ]
    },
    "rules": [
      "dedupe toolIds preserving stable ordering by first occurrence",
      "include source traces for every toolId",
      "do not invent toolIds not present in planContext"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.tools.validate_prompt_registry.v1.json") @'
{
  "id": "task.tools.validate_prompt_registry.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 9 },
  "title": "Tools: Validate tool IDs against prompt registry",
  "description": "Validates that every referenced tool ID has an associated tool prompt definition. Missing prompts cause a controlled WAIT_USER path.",
  "tags": ["tools", "prompts", "registry", "validate"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "validate_tool_prompts_exist",
    "inputs": ["toolRefReport", "promptRegistrySnapshot"],
    "outputs": ["promptValidationReport"],
    "promptValidationReportShape": {
      "fields": [
        "missingToolIds[]",
        "presentToolIds[]",
        "byToolId[]",
        "notes[]"
      ]
    },
    "byToolIdShape": {
      "fields": [
        "toolId",
        "isPresent",
        "promptKey?",
        "sourceRefs[]"
      ]
    },
    "rules": [
      "promptRegistrySnapshot must be treated as authoritative",
      "no silent fallback if missingToolIds not empty",
      "do not auto-generate prompt contents here; this is validation only"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.tools.wait_user.missing_prompts.v1.json") @'
{
  "id": "task.tools.wait_user.missing_prompts.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Tools: WAIT_USER missing prompt definitions",
  "description": "Emits a minimal actionable list of missing tool prompt IDs and where they were referenced. Intended to block execution until fixed.",
  "tags": ["tools", "prompts", "wait_user", "registry"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "emit_wait_user_questions",
    "inputs": ["promptValidationReport"],
    "outputs": ["questions", "waitUser"],
    "waitUser": true,
    "questionRules": [
      "present only missing toolIds",
      "include at least one reference trace per missing id",
      "do not ask for provider/model/toolchain ids",
      "ask user whether to: (A) add prompt definitions, (B) remove tool refs from plan, or (C) choose alternative tools already defined"
    ],
    "uiHints": {
      "control": "checklist_and_choice",
      "fields": [
        "missingToolIds",
        "recommendedActions"
      ]
    }
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.tools.enforce_no_tool_without_prompt_gate.v1.json") @'
{
  "id": "task.tools.enforce_no_tool_without_prompt_gate.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 10 },
  "title": "Tools: Enforce 'no tool without prompt' gate",
  "description": "Preflight gate for any execution: collect tool refs, validate prompt registry, block with WAIT_USER if missing.",
  "tags": ["tools", "gate", "prompts", "preflight"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "tool_prompt_gate",
    "inputs": ["planContext", "promptRegistrySnapshot"],
    "outputs": ["gateResult", "waitUser?", "questions?"],
    "pipeline": [
      { "use": "task.tools.collect_references_from_plan.v1" },
      { "use": "task.tools.validate_prompt_registry.v1" },
      { "decision": "if promptValidationReport.missingToolIds length > 0 then WAIT_USER else continue" }
    ],
    "onWaitUser": { "use": "task.tools.wait_user.missing_prompts.v1" },
    "gateResultShape": {
      "fields": [
        "isPass",
        "missingToolIds[]",
        "notes[]"
      ]
    },
    "rules": [
      "isPass must be false if any missingToolIds exist",
      "do not mutate planContext",
      "do not generate prompts here"
    ]
  }
}
'@

# -------------------------
# RECIPE (public): tool prompt gate preflight
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.tools.prompt_gate.preflight.v1.json") @'
{
  "id": "recipe.tools.prompt_gate.preflight.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 90 },
  "title": "Preflight: Tool prompt gate (no tool without prompt)",
  "description": "Ensures that every tool referenced by the selected plan has a prompt definition. Blocks with WAIT_USER if missing.",
  "tags": ["tools", "prompts", "preflight", "gate"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.tools.enforce_no_tool_without_prompt_gate.v1" }
  ] }
}
'@

# -------------------------
# PACK (public): tool prompt gate (drop-in before execution)
# -------------------------

Write-Json (Join-Path $bt "packs\pack.tools.prompt_gate.preflight.v1.json") @'
{
  "id": "pack.tools.prompt_gate.preflight.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 210 },
  "title": "Preflight Tool Prompt Gate",
  "description": "Hard blocks execution if any referenced tool lacks a prompt definition. Produces a clean WAIT_USER remediation list.",
  "tags": ["tools", "prompts", "preflight", "gate", "unstoppable"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": ["recipe.tools.prompt_gate.preflight.v1"],
    "inputs": ["planContext", "promptRegistrySnapshot"]
  }
}
'@

# -------------------------
# GRAPH (public): orchestrator preflight stage wiring (data-only mermaid)
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.orchestrator.preflight_tool_prompt_gate.v1.json") @'
{
  "id": "graph.orchestrator.preflight_tool_prompt_gate.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Orchestrator: Preflight tool prompt gate",
  "description": "Adds a preflight step that validates tool prompt coverage. If missing prompts, enters WAIT_USER before any execution.",
  "tags": ["orchestrator", "tools", "prompts", "preflight", "wait_user"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  S[Start] --> G[pack.tools.prompt_gate.preflight.v1]\n  G -->|PASS| N[Next: expand pack/recipe plan]\n  G -->|WAIT_USER missing prompts| W[WAIT_USER]\n  W --> G\n  G -->|FAIL hard| D[pack.diagnose.unstoppable.v1]\n  D --> End[End]"
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 35 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

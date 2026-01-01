# BlueprintTemplates/Seed-Corpus-Batch36.ps1
# Batch36: Explicit toolRefs metadata + normalization utilities + routing
# Goal: Make tool prompt gate deterministic: plans declare tools via toolRefs (no guessing)
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
# ATOM (internal): normalize toolRefs across plan objects
# -------------------------

Write-Json (Join-Path $bt "atoms\task.tools.normalize_toolrefs.v1.json") @'
{
  "id": "task.tools.normalize_toolrefs.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 9 },
  "title": "Tools: Normalize toolRefs metadata",
  "description": "Collects explicit toolRefs[] metadata from plan context (packs/recipes/graphs/atoms) and outputs a stable deduped list with source traces. Prefer explicit toolRefs over inferred hints.",
  "tags": ["tools", "toolrefs", "normalize", "prompts", "preflight"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "normalize_toolrefs",
    "inputs": ["planContext"],
    "outputs": ["toolRefReport"],
    "rules": [
      "toolRefs may appear at content.toolRefs, content.toolRefIds, or top-level toolRefs (treat all as aliases)",
      "dedupe toolIds preserving stable ordering by first occurrence",
      "include source traces (sourceId, sourceKind) for every toolId",
      "if no explicit toolRefs exist anywhere, emit empty toolIds list and add a note: 'no explicit toolRefs found'"
    ],
    "toolRefReportShape": {
      "fields": [
        "toolIds[]",
        "toolRefs[]",
        "notes[]"
      ]
    }
  }
}
'@

# -------------------------
# ATOM (internal): tool prompt gate that uses normalize_toolrefs first
# -------------------------

Write-Json (Join-Path $bt "atoms\task.tools.prompt_gate.explicit_first.v1.json") @'
{
  "id": "task.tools.prompt_gate.explicit_first.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 10 },
  "title": "Tools: Prompt gate (explicit toolRefs first)",
  "description": "Runs tool prompt gate preferring explicit toolRefs metadata. If none declared, falls back to existing reference collector. Blocks with WAIT_USER if missing prompts.",
  "tags": ["tools", "prompts", "gate", "preflight", "wait_user"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "tool_prompt_gate_explicit_first",
    "inputs": ["planContext", "promptRegistrySnapshot"],
    "outputs": ["gateResult", "waitUser?", "questions?"],
    "pipeline": [
      { "use": "task.tools.normalize_toolrefs.v1" },
      { "decision": "if toolRefReport.toolIds length == 0 then collect inferred tool refs else continue" },
      { "useIf": { "condition": "toolRefReport.toolIds length == 0", "use": "task.tools.collect_references_from_plan.v1" } },
      { "use": "task.tools.validate_prompt_registry.v1" },
      { "decision": "if promptValidationReport.missingToolIds length > 0 then WAIT_USER else continue" }
    ],
    "onWaitUser": { "use": "task.tools.wait_user.missing_prompts.v1" },
    "rules": [
      "never invent tool ids",
      "no silent fallback if missing prompts",
      "do not generate prompt contents here"
    ]
  }
}
'@

# -------------------------
# RECIPE (public): explicit toolrefs declaration scaffold (data-only)
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.tools.toolrefs.declare_template.v1.json") @'
{
  "id": "recipe.tools.toolrefs.declare_template.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 80 },
  "title": "Declare toolRefs for a plan (template)",
  "description": "Template recipe: declare toolRefs[] in your pack/recipe/graph content so tool prompt gate becomes deterministic. This recipe does not execute anything; it documents the schema via content.toolRefs.",
  "tags": ["tools", "toolrefs", "schema", "prompts"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "toolRefs": [
      {
        "toolId": "example.tool.id",
        "reason": "Why this plan may call the tool",
        "optional": true
      }
    ],
    "steps": [
      {
        "use": "task.add_file.generic.v1",
        "with": {
          "path": "${projectRoot}/.rah/toolrefs.json",
          "note": "Optional: write a machine-readable toolRefs summary for the workspace if your system supports it."
        }
      }
    ]
  }
}
'@

# -------------------------
# PACK (public): prompt gate using explicit-first atom
# -------------------------

Write-Json (Join-Path $bt "packs\pack.tools.prompt_gate.explicit_first.v1.json") @'
{
  "id": "pack.tools.prompt_gate.explicit_first.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 215 },
  "title": "Preflight Tool Prompt Gate (explicit toolRefs first)",
  "description": "Same as the preflight prompt gate, but prefers explicit toolRefs declared in plan templates for deterministic coverage.",
  "tags": ["tools", "prompts", "preflight", "gate", "toolrefs"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "toolRefs": [],
    "uses": ["task.tools.prompt_gate.explicit_first.v1"],
    "inputs": ["planContext", "promptRegistrySnapshot"]
  }
}
'@

# -------------------------
# GRAPH (public): router for tool prompt coverage issues
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.tools.prompt_coverage.v1.json") @'
{
  "id": "graph.router.tools.prompt_coverage.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: Tools prompt coverage",
  "description": "Routes into tool prompt gate and blocks with WAIT_USER if missing prompt definitions for referenced tools.",
  "tags": ["router", "tools", "prompts", "wait_user", "gate"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  A[Goal: validate tool prompts] --> G[pack.tools.prompt_gate.explicit_first.v1]\n  G -->|PASS| OK[Continue]\n  G -->|WAIT_USER missing prompts| W[WAIT_USER]\n  W --> G\n  G -->|FAIL hard| D[pack.diagnose.unstoppable.v1]\n  D --> End[End]"
  }
}
'@

# -------------------------
# EXAMPLE PACKS (public): demonstrate toolRefs declaration without hardcoding commands/providers
# -------------------------

Write-Json (Join-Path $bt "packs\pack.project.start.declare_toolrefs.v1.json") @'
{
  "id": "pack.project.start.declare_toolrefs.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 120 },
  "title": "Project Start: Declare likely toolRefs (template)",
  "description": "Demonstrates how a pack can declare toolRefs metadata so prompt gate can validate coverage before execution. This pack does not hardcode tools; it uses placeholder IDs to be replaced by your manifest-backed toolset.",
  "tags": ["project", "start", "toolrefs", "schema"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "toolRefs": [
      { "toolId": "workspace.create_project", "reason": "Create a new project skeleton for chosen ecosystem/profile", "optional": false },
      { "toolId": "workspace.add_file", "reason": "Write initial files and configs", "optional": false },
      { "toolId": "workspace.build", "reason": "Build the created project", "optional": true },
      { "toolId": "workspace.test", "reason": "Run tests if present", "optional": true }
    ],
    "uses": [
      "pack.tools.prompt_gate.explicit_first.v1",
      "pack.project.start.unstoppable.v1"
    ]
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.diagnose.buildfail.declare_toolrefs.v1.json") @'
{
  "id": "pack.diagnose.buildfail.declare_toolrefs.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 125 },
  "title": "Diagnose Build Fail: Declare likely toolRefs (template)",
  "description": "Demonstrates declaring toolRefs for diagnose flows. Replace tool IDs with your manifest tool IDs that gather logs, run build/test, and apply patches.",
  "tags": ["diagnose", "build", "toolrefs", "schema"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "toolRefs": [
      { "toolId": "workspace.collect_logs", "reason": "Collect build output, errors, and environment info", "optional": false },
      { "toolId": "workspace.build", "reason": "Re-run build for confirmation", "optional": true },
      { "toolId": "workspace.test", "reason": "Run tests after fix", "optional": true },
      { "toolId": "workspace.apply_patch", "reason": "Apply minimal fix patches", "optional": true }
    ],
    "uses": [
      "pack.tools.prompt_gate.explicit_first.v1",
      "pack.diagnose.unstoppable.v1"
    ]
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 36 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

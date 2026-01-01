# BlueprintTemplates/Seed-Corpus-Batch37.ps1
# Batch37: Capability-based toolRefs (capabilities -> toolIds resolved at runtime)
# No provider/toolchain hardcoding. No commands. No hardcoded paths. PS 5.1 compatible.

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
# RECIPE (public): capabilities taxonomy (data-only)
# -------------------------
Write-Json (Join-Path $bt "recipes\recipe.tools.capabilities.taxonomy.v1.json") @'
{
  "id": "recipe.tools.capabilities.taxonomy.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 90 },
  "title": "Tools: Capabilities taxonomy (neutral)",
  "description": "Defines a neutral capability vocabulary for tool selection without hardcoding tool IDs. Resolved at runtime against the active tool manifest.",
  "tags": ["tools", "capabilities", "taxonomy", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "capabilityGroups": [
      {
        "name": "workspace.core",
        "capabilities": [
          "workspace.create_project",
          "workspace.add_file",
          "workspace.replace_file",
          "workspace.apply_patch",
          "workspace.build",
          "workspace.test",
          "workspace.run",
          "workspace.format_lint",
          "workspace.package_publish",
          "workspace.diagnose_build_fail_triage"
        ]
      },
      {
        "name": "workspace.intake",
        "capabilities": [
          "workspace.detect_ecosystem",
          "workspace.detect_entrypoints",
          "workspace.detect_layout",
          "workspace.detect_maturity",
          "workspace.collect_logs"
        ]
      },
      {
        "name": "workspace.repo_ops",
        "capabilities": [
          "repo.init",
          "repo.status",
          "repo.commit",
          "repo.branch",
          "repo.diff",
          "repo.apply_patch"
        ]
      },
      {
        "name": "workspace.packaging",
        "capabilities": [
          "artifact.manifest.generate",
          "artifact.gates.validate",
          "publish.plan.emit",
          "publish.execute"
        ]
      }
    ],
    "resolutionRules": [
      "capabilities are resolved to toolIds by matching against the active manifest's declared capabilities/tags",
      "if multiple toolIds satisfy a capability, prefer the one matching envScope/os/hostKind and with highest internal ranking",
      "if no toolId matches a required capability, WAIT_USER with a minimal request: 'add prompt+tool mapping for capability X'"
    ]
  }
}
'@

# -------------------------
# ATOM (internal): resolve capabilities -> toolIds using runtime manifest
# -------------------------
Write-Json (Join-Path $bt "atoms\task.tools.resolve_capabilities_to_toolids.v1.json") @'
{
  "id": "task.tools.resolve_capabilities_to_toolids.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 12 },
  "title": "Tools: Resolve capabilities to toolIds",
  "description": "Maps capabilityRefs[] to concrete toolIds using the active tool manifest at runtime. Produces toolRefs[] suitable for prompt gating. Does not execute tools.",
  "tags": ["tools", "capabilities", "resolve", "preflight", "wait_user"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "resolve_capabilities_to_toolids",
    "inputs": ["capabilityRefs", "toolManifestSnapshot", "runtimeEnv"],
    "outputs": ["resolvedToolRefs", "missingCapabilities", "notes", "waitUser?"],
    "matchInputs": {
      "runtimeEnvFields": ["os", "hostKind", "envScope", "workspaceRoot?"],
      "capabilityRefShape": { "fields": ["capability", "required", "reason?", "constraints?"] }
    },
    "selectionRules": [
      "only select toolIds that exist in toolManifestSnapshot",
      "prefer tools whose metadata matches runtimeEnv.os and runtimeEnv.hostKind when available",
      "if required capability has zero matches, add to missingCapabilities and set waitUser true",
      "dedupe resolved toolIds preserving stable order by first resolution"
    ],
    "outputShape": {
      "resolvedToolRefs": [
        { "toolId": "string", "capability": "string", "required": true, "reason": "string" }
      ]
    }
  }
}
'@

# -------------------------
# ATOM (internal): merge explicit toolRefs + capabilityRefs (explicit wins)
# -------------------------
Write-Json (Join-Path $bt "atoms\task.tools.merge_explicit_and_capability_toolrefs.v1.json") @'
{
  "id": "task.tools.merge_explicit_and_capability_toolrefs.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 11 },
  "title": "Tools: Merge explicit toolRefs + capabilityRefs",
  "description": "Combines explicitly declared toolRefs with resolved toolRefs from capabilityRefs. Explicit toolRefs take precedence and are not overridden.",
  "tags": ["tools", "toolrefs", "capabilities", "merge"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "merge_toolrefs",
    "inputs": ["explicitToolRefs", "resolvedToolRefs"],
    "outputs": ["mergedToolRefs", "notes"],
    "rules": [
      "explicit toolRefs always included first in order",
      "resolved toolRefs appended if toolId not already present",
      "do not invent toolIds"
    ]
  }
}
'@

# -------------------------
# ATOM (internal): prompt gate, but with capabilities resolution step
# -------------------------
Write-Json (Join-Path $bt "atoms\task.tools.prompt_gate.capabilities_aware.v1.json") @'
{
  "id": "task.tools.prompt_gate.capabilities_aware.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 13 },
  "title": "Tools: Prompt gate (capabilities-aware)",
  "description": "Resolves capabilityRefs to toolIds, merges with explicit toolRefs, then validates prompt registry coverage. WAIT_USER on missing capabilities or missing prompts.",
  "tags": ["tools", "capabilities", "prompts", "gate", "wait_user"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "tool_prompt_gate_capabilities_aware",
    "inputs": ["planContext", "toolManifestSnapshot", "promptRegistrySnapshot", "runtimeEnv"],
    "outputs": ["gateResult", "waitUser?", "questions?"],
    "pipeline": [
      { "use": "task.tools.normalize_toolrefs.v1" },
      { "extract": "capabilityRefs from planContext.content.capabilityRefs (and aliases content.capabilities)" },
      { "use": "task.tools.resolve_capabilities_to_toolids.v1" },
      { "decision": "if missingCapabilities length > 0 then WAIT_USER else continue" },
      { "use": "task.tools.merge_explicit_and_capability_toolrefs.v1" },
      { "use": "task.tools.validate_prompt_registry.v1" },
      { "decision": "if promptValidationReport.missingToolIds length > 0 then WAIT_USER else PASS" }
    ],
    "onWaitUser": {
      "action": "emit_wait_user_questions",
      "questionRules": [
        "ask only for missing capability/tool prompt coverage needed to proceed",
        "request exact tool id(s) from manifest and confirm prompt file exists",
        "never ask for provider/model/toolchain ids"
      ]
    }
  }
}
'@

# -------------------------
# RECIPE (public): add capabilityRefs to a pack/plan (template)
# -------------------------
Write-Json (Join-Path $bt "recipes\recipe.tools.capabilityrefs.declare_template.v1.json") @'
{
  "id": "recipe.tools.capabilityrefs.declare_template.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 85 },
  "title": "Declare capabilityRefs for a plan (template)",
  "description": "Template recipe showing capabilityRefs[] which are resolved to toolIds at runtime. Keeps templates tool-manifest-neutral.",
  "tags": ["tools", "capabilities", "schema", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "capabilityRefs": [
      { "capability": "workspace.build", "required": true, "reason": "Need to compile project" },
      { "capability": "workspace.test", "required": false, "reason": "Run tests if present" }
    ],
    "notes": [
      "capabilityRefs are resolved at runtime against your active tool manifest",
      "use constraints to limit selection by os/hostKind/envScope if needed"
    ]
  }
}
'@

# -------------------------
# PACK (public): capabilities-aware prompt gate
# -------------------------
Write-Json (Join-Path $bt "packs\pack.tools.prompt_gate.capabilities_aware.v1.json") @'
{
  "id": "pack.tools.prompt_gate.capabilities_aware.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 230 },
  "title": "Preflight Tool Prompt Gate (capabilities-aware)",
  "description": "Resolves capabilityRefs -> toolIds from active manifest, merges with explicit toolRefs, then enforces prompt coverage before execution.",
  "tags": ["tools", "capabilities", "prompts", "preflight", "gate"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "capabilityRefs": [
      { "capability": "workspace.add_file", "required": true, "reason": "Any plan that writes files must have add_file tool coverage" },
      { "capability": "workspace.apply_patch", "required": false, "reason": "Plans that patch code need patch coverage" }
    ],
    "uses": ["task.tools.prompt_gate.capabilities_aware.v1"],
    "inputs": ["planContext", "toolManifestSnapshot", "promptRegistrySnapshot", "runtimeEnv"]
  }
}
'@

# -------------------------
# GRAPH (public): capabilities-aware preflight router
# -------------------------
Write-Json (Join-Path $bt "graphs\graph.router.tools.capabilities_aware.v1.json") @'
{
  "id": "graph.router.tools.capabilities_aware.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: Tools preflight (capabilities-aware)",
  "description": "Routes through capabilities-aware tool prompt gate, then continues to execution or WAIT_USER if coverage is missing.",
  "tags": ["router", "tools", "capabilities", "prompts", "wait_user"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  A[Goal: run plan safely] --> T[pack.tools.prompt_gate.capabilities_aware.v1]\n  T -->|PASS| OK[Continue to orchestrator]\n  T -->|WAIT_USER missing coverage| W[WAIT_USER]\n  W --> T\n  T -->|FAIL hard| D[pack.diagnose.unstoppable.v1]\n  D --> End[End]"
  }
}
'@

# -------------------------
# PACK (public): unstoppable goal runner but capabilities-aware preflight first
# -------------------------
Write-Json (Join-Path $bt "packs\pack.goal_runner.unstoppable.capabilities_aware.v1.json") @'
{
  "id": "pack.goal_runner.unstoppable.capabilities_aware.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 240 },
  "title": "Goal Runner (unstoppable, capabilities-aware)",
  "description": "Runs detect+adapt goal runner with a capabilities-aware preflight tool prompt gate. Blocks with WAIT_USER when required coverage is missing.",
  "tags": ["goal", "runner", "unstoppable", "capabilities", "preflight"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "pack.tools.prompt_gate.capabilities_aware.v1",
      "pack.detect_and_adapt.goal_runner.v1"
    ],
    "capabilityRefs": [
      { "capability": "workspace.detect_ecosystem", "required": false, "reason": "Improve plan selection" },
      { "capability": "workspace.collect_logs", "required": false, "reason": "If diagnose path triggers, logs are needed" }
    ]
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 37 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

# Seed-Corpus-Batch40.ps1
# Adds 5 public "role default" BlueprintTemplates so each role can be set once and reused.
# Compatible with Windows PowerShell 5.1

$ErrorActionPreference = "Stop"

function Ensure-Dir([string]$p) {
  if (-not (Test-Path -LiteralPath $p)) {
    New-Item -ItemType Directory -Path $p | Out-Null
  }
}

function Write-Utf8([string]$path, [string]$content) {
  $dir = Split-Path -Parent $path
  Ensure-Dir $dir
  Set-Content -LiteralPath $path -Value $content -Encoding utf8
  Write-Host ("Wrote: " + $path)
}

# Resolve BlueprintTemplates root robustly (same pattern as Batch39)
$base = Split-Path -Parent $MyInvocation.MyCommand.Path
$hereName = Split-Path -Leaf $base
if ($hereName -ieq "BlueprintTemplates") {
  $bt = $base
} else {
  $bt = Join-Path $base "BlueprintTemplates"
}

if (-not (Test-Path -LiteralPath $bt)) {
  throw "BlueprintTemplates root not found: $bt"
}

$packs = Join-Path $bt "packs"
Ensure-Dir $packs

# Timestamp (fixed string so corpus is deterministic until you bump it)
# You can update this later if you want. Not required for function.
$utc = "2025-12-31T00:00:00Z"

# --------------------------------------------------------------------------------
# 1) ORCHESTRATOR DEFAULT (public pack)
# --------------------------------------------------------------------------------
Write-Utf8 (Join-Path $packs "pack.role.orchestrator.default.v1.json") @"
{
  "id": "pack.role.orchestrator.default.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 900 },
  "title": "Role Default: Orchestrator (universal)",
  "description": "Universal Orchestrator role purpose. Interprets user intent, selects appropriate BlueprintTemplates by manifest metadata, and routes work through Planner/Executor/Repair/Embed. Prefers public templates and honors the user-edited Mermaid workflow graph and environment constraints.",
  "tags": ["role", "orchestrator", "default", "universal", "routing", "intent", "manifest", "graph"],
  "updatedUtc": "$utc",
  "content": {
    "role": "Orchestrator",
    "purpose": [
      "Convert user intent into a safe, testable plan and route the work to the correct internal roles.",
      "Select BlueprintTemplates ONLY by IDs listed in BlueprintTemplates/manifest.json.",
      "Use template metadata (title/description/tags/kind/priority/visibility) to choose the best match."
    ],
    "selectionPolicy": {
      "manifestIsSourceOfTruth": true,
      "visibilityDefault": "public",
      "allowedKinds": ["pack", "recipe", "graph"],
      "whenNoPerfectMatch": [
        "prefer broad 'universal' packs",
        "prefer high priority packs",
        "ask WAIT_USER only when essential info is missing"
      ]
    },
    "routingRules": [
      "Honor the Mermaid workflow graph from Settings if present and valid. Do not fall back silently.",
      "Do not confuse BlueprintTemplates with tool prompts. Tool prompts gate tools separately.",
      "Never invent tool IDs. Tool selection must be validated by tool prompt coverage."
    ],
    "outputs": {
      "taskBoard": "Emit a TaskBoard with clear steps, owners (roles), and required tools.",
      "handoffs": "If a role is missing configuration or blocked, surface a WAIT_USER question."
    }
  }
}
"@

# --------------------------------------------------------------------------------
# 2) PLANNER DEFAULT (public pack)
# --------------------------------------------------------------------------------
Write-Utf8 (Join-Path $packs "pack.role.planner.default.v1.json") @"
{
  "id": "pack.role.planner.default.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 890 },
  "title": "Role Default: Planner (universal)",
  "description": "Universal Planner role purpose. Builds a TaskBoard by selecting BlueprintTemplates that match the goal. Produces minimal, executable steps. Explicitly separates BlueprintTemplates selection from tool prompt gating.",
  "tags": ["role", "planner", "default", "universal", "taskboard", "blueprints", "constraints"],
  "updatedUtc": "$utc",
  "content": {
    "role": "Planner",
    "purpose": [
      "Turn the goal into a TaskBoard with ordered tasks that can be executed and verified.",
      "Pick BlueprintTemplates by manifest metadata (title/description/tags), not folder names.",
      "Prefer reuse: select existing packs/recipes before inventing new ones."
    ],
    "planningRules": [
      "Keep tasks small and testable.",
      "Include build/run/test commands when applicable.",
      "If environment constraints block execution, add WAIT_USER tasks to resolve it."
    ],
    "selectionPolicy": {
      "manifestIsSourceOfTruth": true,
      "visibilityDefault": "public",
      "allowedKinds": ["pack", "recipe", "graph"],
      "bias": ["high priority", "broad applicability", "clear description"]
    }
  }
}
"@

# --------------------------------------------------------------------------------
# 3) EXECUTOR DEFAULT (public pack)
# --------------------------------------------------------------------------------
Write-Utf8 (Join-Path $packs "pack.role.executor.default.v1.json") @"
{
  "id": "pack.role.executor.default.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 880 },
  "title": "Role Default: Executor (universal)",
  "description": "Universal Executor role purpose. Executes TaskBoard steps safely, uses tools only when tool prompts exist, and validates results with builds/tests. Does not select tools by guessing; uses tool prompt coverage gates.",
  "tags": ["role", "executor", "default", "universal", "tools", "prompt_gate", "build", "test"],
  "updatedUtc": "$utc",
  "content": {
    "role": "Executor",
    "purpose": [
      "Carry out tasks exactly as planned and verify with observable outputs (build/test/run).",
      "Use tools ONLY when tool prompts are available for the tool IDs (tool prompts gate tools).",
      "Keep changes minimal and localized to the requested scope."
    ],
    "executionRules": [
      "Do not invent paths. Use settings-driven paths.",
      "Do not invent tool IDs. Validate tool availability and prompt coverage first.",
      "If blocked, stop and emit WAIT_USER with a short, specific question."
    ],
    "verification": [
      "Prefer 'dotnet build' / 'dotnet test' / 'dotnet run' for .NET tasks.",
      "Record what changed and what passed."
    ]
  }
}
"@

# --------------------------------------------------------------------------------
# 4) REPAIR DEFAULT (public pack)
# --------------------------------------------------------------------------------
Write-Utf8 (Join-Path $packs "pack.role.repair.default.v1.json") @"
{
  "id": "pack.role.repair.default.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 870 },
  "title": "Role Default: Repair (minimal + safe)",
  "description": "Universal Repair role purpose. Diagnoses failures (build/runtime/tooling) and produces minimal, surgical fixes. Avoids refactors and preserves existing architecture unless directly required to compile or pass tests.",
  "tags": ["role", "repair", "default", "minimal_fix", "diagnostics", "surgical", "no_refactor"],
  "updatedUtc": "$utc",
  "content": {
    "role": "Repair",
    "purpose": [
      "Identify root cause from logs and code, then apply the smallest fix that resolves it.",
      "Avoid architecture rewrites and unrelated cleanup.",
      "Preserve settings-driven behavior; remove hardcoded fallbacks."
    ],
    "repairRules": [
      "Prefer handling DataGridView DataError instead of crashing the UI.",
      "Prefer fixing invalid ComboBox values by normalizing/clearing stale IDs.",
      "If a referenced template ID no longer exists in manifest, set it to (unset) and warn."
    ],
    "outputs": {
      "patchStyle": "Full file replacement only when necessary; otherwise minimal edits."
    }
  }
}
"@

# --------------------------------------------------------------------------------
# 5) EMBED DEFAULT (public pack)
# --------------------------------------------------------------------------------
Write-Utf8 (Join-Path $packs "pack.role.embed.default.v1.json") @"
{
  "id": "pack.role.embed.default.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 860 },
  "title": "Role Default: Embed (memory/indexing support)",
  "description": "Universal Embed role purpose. Prepares text chunks and metadata for indexing and retrieval (e.g., Qdrant). Keeps payloads deterministic and uses BlueprintTemplates metadata to label content by kind/tags/visibility.",
  "tags": ["role", "embed", "default", "memory", "indexing", "qdrant", "metadata"],
  "updatedUtc": "$utc",
  "content": {
    "role": "Embed",
    "purpose": [
      "Create deterministic, high-signal embeddings and metadata for search/reuse.",
      "Use BlueprintTemplates metadata (id/kind/tags/visibility/title/description) for labeling.",
      "Do not mix tool prompts with BlueprintTemplates."
    ],
    "rules": [
      "Prefer public templates for normal reuse.",
      "Keep chunk sizes consistent and remove boilerplate.",
      "Never embed secrets (API keys, tokens)."
    ]
  }
}
"@

Write-Host ""
Write-Host "Done seeding batch 40 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

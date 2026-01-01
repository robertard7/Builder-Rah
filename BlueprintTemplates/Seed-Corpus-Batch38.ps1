# BlueprintTemplates/Seed-Corpus-Batch38.ps1
# Batch38: Ecosystem capability presets (dotnet/node/python/go/rust/java/cpp + generic)
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
# RECIPE (public): ecosystem capability presets
# -------------------------
Write-Json (Join-Path $bt "recipes\recipe.tools.capability_presets.ecosystems.v1.json") @'
{
  "id": "recipe.tools.capability_presets.ecosystems.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 88 },
  "title": "Tools: Ecosystem capability presets (neutral)",
  "description": "Defines capabilityRef presets for common ecosystems. These are resolved to toolIds at runtime (no tool IDs in templates).",
  "tags": ["tools", "capabilities", "presets", "ecosystem", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "presets": [
      {
        "name": "generic.base",
        "when": { "ecosystem": "generic" },
        "capabilityRefs": [
          { "capability": "workspace.add_file", "required": true, "reason": "Project creation needs file write capability" },
          { "capability": "workspace.replace_file", "required": true, "reason": "Edits must be possible" },
          { "capability": "workspace.apply_patch", "required": false, "reason": "Prefer patches for surgical changes when available" },
          { "capability": "workspace.format_lint", "required": false, "reason": "Optional formatting/lint" },
          { "capability": "workspace.build", "required": false, "reason": "If build step exists" },
          { "capability": "workspace.test", "required": false, "reason": "If tests exist" },
          { "capability": "workspace.run", "required": false, "reason": "If runnable target exists" }
        ]
      },

      {
        "name": "dotnet.service_or_app",
        "when": { "ecosystem": "dotnet" },
        "capabilityRefs": [
          { "capability": "workspace.create_project", "required": false, "reason": "Can scaffold via CLI if project missing" },
          { "capability": "workspace.add_file", "required": true, "reason": "Write csproj and sources" },
          { "capability": "workspace.replace_file", "required": true, "reason": "Edits are normal" },
          { "capability": "workspace.apply_patch", "required": false, "reason": "Surgical changes preferred" },
          { "capability": "workspace.format_lint", "required": false, "reason": "dotnet format / analyzers optional" },
          { "capability": "workspace.build", "required": true, "reason": "dotnet build as primary compile gate" },
          { "capability": "workspace.test", "required": false, "reason": "dotnet test if tests present" },
          { "capability": "workspace.run", "required": false, "reason": "dotnet run if app target exists" },
          { "capability": "artifact.manifest.generate", "required": false, "reason": "Optional publish outputs manifest" }
        ]
      },

      {
        "name": "node.app_or_service",
        "when": { "ecosystem": "node" },
        "capabilityRefs": [
          { "capability": "workspace.create_project", "required": false, "reason": "Can scaffold package.json if missing" },
          { "capability": "workspace.add_file", "required": true, "reason": "Write sources/config" },
          { "capability": "workspace.replace_file", "required": true, "reason": "Edits are normal" },
          { "capability": "workspace.apply_patch", "required": false, "reason": "Surgical changes preferred" },
          { "capability": "workspace.format_lint", "required": false, "reason": "eslint/prettier optional" },
          { "capability": "workspace.build", "required": false, "reason": "Some apps have build step" },
          { "capability": "workspace.test", "required": false, "reason": "jest/vitest if present" },
          { "capability": "workspace.run", "required": true, "reason": "node apps should run or start script" }
        ]
      },

      {
        "name": "python.cli_or_api",
        "when": { "ecosystem": "python" },
        "capabilityRefs": [
          { "capability": "workspace.create_project", "required": false, "reason": "Scaffold package layout if missing" },
          { "capability": "workspace.add_file", "required": true, "reason": "Write sources/config" },
          { "capability": "workspace.replace_file", "required": true, "reason": "Edits are normal" },
          { "capability": "workspace.apply_patch", "required": false, "reason": "Surgical changes preferred" },
          { "capability": "workspace.format_lint", "required": false, "reason": "ruff/black optional" },
          { "capability": "workspace.test", "required": false, "reason": "pytest if present" },
          { "capability": "workspace.run", "required": true, "reason": "python entrypoint should be runnable" }
        ]
      },

      {
        "name": "go.cli_or_service",
        "when": { "ecosystem": "go" },
        "capabilityRefs": [
          { "capability": "workspace.create_project", "required": false, "reason": "go mod init if missing" },
          { "capability": "workspace.add_file", "required": true, "reason": "Write sources" },
          { "capability": "workspace.replace_file", "required": true, "reason": "Edits are normal" },
          { "capability": "workspace.apply_patch", "required": false, "reason": "Surgical changes preferred" },
          { "capability": "workspace.format_lint", "required": true, "reason": "gofmt is baseline" },
          { "capability": "workspace.build", "required": true, "reason": "go build as compile gate" },
          { "capability": "workspace.test", "required": false, "reason": "go test if tests exist" },
          { "capability": "workspace.run", "required": false, "reason": "go run for simple apps" }
        ]
      },

      {
        "name": "rust.cli_or_service",
        "when": { "ecosystem": "rust" },
        "capabilityRefs": [
          { "capability": "workspace.create_project", "required": false, "reason": "cargo new if missing" },
          { "capability": "workspace.add_file", "required": true, "reason": "Write sources" },
          { "capability": "workspace.replace_file", "required": true, "reason": "Edits are normal" },
          { "capability": "workspace.apply_patch", "required": false, "reason": "Surgical changes preferred" },
          { "capability": "workspace.format_lint", "required": false, "reason": "rustfmt/clippy optional but common" },
          { "capability": "workspace.build", "required": true, "reason": "cargo build as compile gate" },
          { "capability": "workspace.test", "required": false, "reason": "cargo test if tests exist" },
          { "capability": "workspace.run", "required": false, "reason": "cargo run for apps" }
        ]
      },

      {
        "name": "java.cli_or_service",
        "when": { "ecosystem": "java" },
        "capabilityRefs": [
          { "capability": "workspace.create_project", "required": false, "reason": "maven/gradle scaffold if missing" },
          { "capability": "workspace.add_file", "required": true, "reason": "Write sources/pom/build files" },
          { "capability": "workspace.replace_file", "required": true, "reason": "Edits are normal" },
          { "capability": "workspace.apply_patch", "required": false, "reason": "Surgical changes preferred" },
          { "capability": "workspace.format_lint", "required": false, "reason": "Optional formatting checks" },
          { "capability": "workspace.build", "required": true, "reason": "mvn/gradle build as gate" },
          { "capability": "workspace.test", "required": false, "reason": "unit tests if present" },
          { "capability": "workspace.run", "required": false, "reason": "run if app target exists" }
        ]
      },

      {
        "name": "cpp.cmake_or_buildsystem",
        "when": { "ecosystem": "cpp" },
        "capabilityRefs": [
          { "capability": "workspace.create_project", "required": false, "reason": "cmake scaffold if missing" },
          { "capability": "workspace.add_file", "required": true, "reason": "Write sources/build files" },
          { "capability": "workspace.replace_file", "required": true, "reason": "Edits are normal" },
          { "capability": "workspace.apply_patch", "required": false, "reason": "Surgical changes preferred" },
          { "capability": "workspace.format_lint", "required": false, "reason": "clang-format optional" },
          { "capability": "workspace.build", "required": true, "reason": "build as compile gate" },
          { "capability": "workspace.test", "required": false, "reason": "ctest if present" },
          { "capability": "workspace.run", "required": false, "reason": "run if binary known" }
        ]
      }
    ],
    "notes": [
      "These presets are capability-only. Your runtime maps them to toolIds using the active manifest.",
      "Add more ecosystems by appending presets. No other template changes required."
    ]
  }
}
'@

# -------------------------
# ATOM (internal): choose preset based on detected ecosystem + goal intent
# -------------------------
Write-Json (Join-Path $bt "atoms\task.tools.select_capability_preset.v1.json") @'
{
  "id": "task.tools.select_capability_preset.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 10 },
  "title": "Tools: Select capability preset (by ecosystem)",
  "description": "Chooses a capability preset based on detected ecosystem. Outputs selectedPresetName + capabilityRefs to merge into planContext.",
  "tags": ["tools", "capabilities", "preset", "select"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "select_capability_preset",
    "inputs": ["detectedEcosystem", "goalIntent?", "presetCatalog"],
    "outputs": ["selectedPresetName", "selectedCapabilityRefs", "notes"],
    "rules": [
      "if detectedEcosystem matches a preset.when.ecosystem, choose that preset",
      "else choose generic.base",
      "do not remove any existing explicit planContext toolRefs",
      "stable output order as declared in preset"
    ]
  }
}
'@

# -------------------------
# ATOM (internal): merge selected capabilityRefs into planContext (non-destructive)
# -------------------------
Write-Json (Join-Path $bt "atoms\task.tools.inject_capabilityrefs_into_plancontext.v1.json") @'
{
  "id": "task.tools.inject_capabilityrefs_into_plancontext.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 9 },
  "title": "Tools: Inject capabilityRefs into planContext",
  "description": "Adds capabilityRefs to planContext.content.capabilityRefs if missing. Does not overwrite existing entries.",
  "tags": ["tools", "capabilities", "inject", "plancontext"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "inject_capabilityrefs",
    "inputs": ["planContext", "selectedCapabilityRefs"],
    "outputs": ["updatedPlanContext", "notes"],
    "rules": [
      "if planContext.content.capabilityRefs missing, create it",
      "append selectedCapabilityRefs that are not already present (match by capability string)",
      "do not change explicit toolRefs",
      "preserve stable ordering: existing first, then injected"
    ]
  }
}
'@

# -------------------------
# RECIPE (public): detect ecosystem intent (schema-only)
# -------------------------
Write-Json (Join-Path $bt "recipes\recipe.detect.ecosystem.intent_contract.v1.json") @'
{
  "id": "recipe.detect.ecosystem.intent_contract.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 70 },
  "title": "Detect: ecosystem + entrypoints intent contract",
  "description": "Schema for detection outputs used to select tool capability presets.",
  "tags": ["detect", "ecosystem", "intent", "contract"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "detectedEcosystem": "dotnet|node|python|go|rust|java|cpp|generic",
    "signals": ["fileMarkers", "lockfiles", "buildFiles", "entrypoints"],
    "confidence": "0..1",
    "notes": ["string"]
  }
}
'@

# -------------------------
# PACK (public): detect -> select preset -> inject -> capabilities-aware prompt gate
# -------------------------
Write-Json (Join-Path $bt "packs\pack.tools.capabilities_preset_preflight.v1.json") @'
{
  "id": "pack.tools.capabilities_preset_preflight.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 241 },
  "title": "Preflight: Detect ecosystem -> capability preset -> prompt gate",
  "description": "Detects ecosystem, selects capability preset, injects capabilityRefs into planContext, then runs capabilities-aware tool prompt gate.",
  "tags": ["tools", "capabilities", "preflight", "detect", "prompt_gate", "wait_user"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "inputs": ["planContext", "toolManifestSnapshot", "promptRegistrySnapshot", "runtimeEnv"],
    "steps": [
      { "use": "task.detect.ecosystem_and_entrypoints.v1", "as": "detectOut" },
      { "use": "task.tools.select_capability_preset.v1", "with": { "detectedEcosystem": "${detectOut.detectedEcosystem}", "presetCatalog": "recipe.tools.capability_presets.ecosystems.v1" }, "as": "presetOut" },
      { "use": "task.tools.inject_capabilityrefs_into_plancontext.v1", "with": { "planContext": "${planContext}", "selectedCapabilityRefs": "${presetOut.selectedCapabilityRefs}" }, "as": "plan2" },
      { "use": "task.tools.prompt_gate.capabilities_aware.v1", "with": { "planContext": "${plan2.updatedPlanContext}", "toolManifestSnapshot": "${toolManifestSnapshot}", "promptRegistrySnapshot": "${promptRegistrySnapshot}", "runtimeEnv": "${runtimeEnv}" }, "as": "gate" }
    ],
    "outputs": ["gate", "planContextOut"]
  }
}
'@

# -------------------------
# GRAPH (public): router for preset preflight
# -------------------------
Write-Json (Join-Path $bt "graphs\graph.router.tools.capability_presets_preflight.v1.json") @'
{
  "id": "graph.router.tools.capability_presets_preflight.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: Tools preflight (ecosystem presets)",
  "description": "Detect ecosystem -> inject capability presets -> capabilities-aware prompt gate -> continue / WAIT_USER.",
  "tags": ["router", "tools", "capabilities", "presets", "preflight", "wait_user"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  A[Goal: execute plan] --> P[pack.tools.capabilities_preset_preflight.v1]\n  P -->|PASS| OK[Continue to orchestrator]\n  P -->|WAIT_USER missing capability or prompt| W[WAIT_USER]\n  W --> P\n  P -->|FAIL hard| D[pack.diagnose.unstoppable.v1]\n  D --> End[End]"
  }
}
'@

# -------------------------
# PACK (public): unstoppable goal runner (now with ecosystem preset preflight)
# -------------------------
Write-Json (Join-Path $bt "packs\pack.goal_runner.unstoppable.ecosystem_presets.v1.json") @'
{
  "id": "pack.goal_runner.unstoppable.ecosystem_presets.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 242 },
  "title": "Goal Runner (unstoppable, ecosystem presets)",
  "description": "Runs ecosystem preset preflight then detect+adapt goal runner. Blocks with WAIT_USER when required coverage is missing.",
  "tags": ["goal", "runner", "unstoppable", "capabilities", "presets", "preflight"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "pack.tools.capabilities_preset_preflight.v1",
      "pack.detect_and_adapt.goal_runner.v1"
    ]
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 38 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

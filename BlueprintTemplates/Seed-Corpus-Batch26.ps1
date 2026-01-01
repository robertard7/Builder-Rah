# BlueprintTemplates/Seed-Corpus-Batch26.ps1
# Batch26: Desktop App Toolkit (neutral): config, settings UI contract, offline mode, updates, crash reporting, packaging posture.
# No provider/toolchain hardcoding. No raw shell commands. No hardcoded paths. PS 5.1 compatible.

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
# ATOMS (internal): desktop toolkit primitives (intent-only)
# -------------------------

Write-Json (Join-Path $bt "atoms\task.desktop.config.define_storage_contract.v1.json") @'
{
  "id": "task.desktop.config.define_storage_contract.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Desktop: Define configuration storage contract",
  "description": "Defines where configuration lives, how it is loaded/saved, schema versioning, and migration behavior (neutral to platform and library).",
  "tags": ["desktop", "config", "contract"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "desktop_define_config_storage_contract",
    "inputs": ["projectRoot", "appName?", "platformTargets?"],
    "outputs": ["configStorageContract"],
    "rules": [
      "no hardcoded absolute paths",
      "define per-user vs per-machine separation",
      "support schema versioning and migrations",
      "avoid storing secrets in plain config unless explicitly intended"
    ],
    "variables": {
      "projectRoot": "${projectRoot}",
      "appName": "${projectName}"
    }
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.desktop.settings.define_ui_contract.v1.json") @'
{
  "id": "task.desktop.settings.define_ui_contract.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Desktop: Define Settings UI contract",
  "description": "Defines a neutral settings UI contract: sections, fields, validation, apply/cancel behavior, and persistence linkage.",
  "tags": ["desktop", "settings", "ui", "contract"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "desktop_define_settings_ui_contract",
    "inputs": ["projectRoot", "configStorageContract", "uiFrameworkHint?"],
    "outputs": ["settingsUiContract"],
    "rules": [
      "settings must be grouped into sections with stable IDs",
      "validation must be explicit and user-facing",
      "support apply vs cancel vs restore defaults",
      "no provider/model/toolchain IDs in defaults"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.desktop.offline.define_behavior_contract.v1.json") @'
{
  "id": "task.desktop.offline.define_behavior_contract.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Desktop: Define offline mode contract",
  "description": "Defines what the app does when offline: caching rules, degraded behavior, user messaging, and retry strategy.",
  "tags": ["desktop", "offline", "resilience"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "desktop_define_offline_mode_contract",
    "inputs": ["projectRoot", "appType?", "networkFeatures?"],
    "outputs": ["offlineModeContract"],
    "rules": [
      "define what works offline and what does not",
      "cache must have limits and eviction policy",
      "retry behavior must avoid infinite loops",
      "user must be informed when in degraded mode"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.desktop.update.define_strategy_contract.v1.json") @'
{
  "id": "task.desktop.update.define_strategy_contract.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Desktop: Define update strategy contract",
  "description": "Defines neutral update behavior: channeling, staged rollout, signature policy, rollback strategy, and user prompts.",
  "tags": ["desktop", "updates", "release"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "desktop_define_update_strategy_contract",
    "inputs": ["projectRoot", "distributionHint?", "riskTolerance?"],
    "outputs": ["updateStrategyContract"],
    "rules": [
      "define update channels (stable/beta/dev) if applicable",
      "define signature/verification policy conceptually",
      "define rollback strategy and failure handling",
      "avoid auto-updating without a user-visible policy"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.desktop.crash.define_reporting_contract.v1.json") @'
{
  "id": "task.desktop.crash.define_reporting_contract.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Desktop: Define crash reporting contract",
  "description": "Defines what is captured on crashes, redaction rules, user consent, storage, and export path (neutral).",
  "tags": ["desktop", "crash", "telemetry", "privacy"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "desktop_define_crash_reporting_contract",
    "inputs": ["projectRoot", "privacyPosture?"],
    "outputs": ["crashReportingContract"],
    "rules": [
      "default must respect user privacy and consent",
      "redact secrets and tokens",
      "support user-exportable crash bundle",
      "define retention and deletion behavior"
    ],
    "defaults": {
      "privacyPosture": "consent_required"
    }
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.desktop.packaging.define_artifact_contract.v1.json") @'
{
  "id": "task.desktop.packaging.define_artifact_contract.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Desktop: Define packaging + artifact contract",
  "description": "Defines what artifacts exist (installer/portable), naming, version stamping, signing concept, and where artifacts land (variables only).",
  "tags": ["desktop", "packaging", "release", "artifacts"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "desktop_define_packaging_artifact_contract",
    "inputs": ["projectRoot", "appName?", "targets?"],
    "outputs": ["artifactContract"],
    "rules": [
      "no hardcoded output directories; use ${projectRoot} and variables",
      "define version stamping across binaries and metadata",
      "define signing conceptually (if applicable)",
      "define portable vs installer differences"
    ],
    "variables": {
      "projectRoot": "${projectRoot}",
      "appName": "${projectName}",
      "artifactRoot": "${artifactRoot?}"
    }
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.desktop.health.define_startup_shutdown_contract.v1.json") @'
{
  "id": "task.desktop.health.define_startup_shutdown_contract.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "Desktop: Define startup/shutdown health contract",
  "description": "Defines startup validation, safe mode triggers, and shutdown persistence guarantees (neutral).",
  "tags": ["desktop", "startup", "shutdown", "safe_mode"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "desktop_define_startup_shutdown_contract",
    "inputs": ["projectRoot", "configStorageContract"],
    "outputs": ["startupShutdownContract"],
    "rules": [
      "define what constitutes a 'bad startup' and safe mode entry",
      "ensure settings are flushed safely on exit",
      "avoid data loss on crash by using atomic write patterns conceptually"
    ]
  }
}
'@

# -------------------------
# RECIPES (public): desktop toolkit flows
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.desktop.toolkit.baseline.neutral.v1.json") @'
{
  "id": "recipe.desktop.toolkit.baseline.neutral.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 86 },
  "title": "Desktop toolkit baseline (neutral)",
  "description": "Establishes core desktop app contracts: config storage, settings UI, offline mode, updates, crash reporting, packaging artifacts, and startup health.",
  "tags": ["desktop", "toolkit", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.desktop.config.define_storage_contract.v1" },
    { "use": "task.desktop.settings.define_ui_contract.v1" },
    { "use": "task.desktop.offline.define_behavior_contract.v1" },
    { "use": "task.desktop.update.define_strategy_contract.v1" },
    { "use": "task.desktop.crash.define_reporting_contract.v1" },
    { "use": "task.desktop.packaging.define_artifact_contract.v1" },
    { "use": "task.desktop.health.define_startup_shutdown_contract.v1" }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.desktop.privacy.first.crash_only.neutral.v1.json") @'
{
  "id": "recipe.desktop.privacy.first.crash_only.neutral.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 74 },
  "title": "Privacy-first crash handling (neutral)",
  "description": "Defines crash reporting with strict consent and redaction, producing exportable local bundles only by default.",
  "tags": ["desktop", "privacy", "crash", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.desktop.crash.define_reporting_contract.v1" }
  ] }
}
'@

# -------------------------
# PACKS (public): desktop toolkit entrypoints
# -------------------------

Write-Json (Join-Path $bt "packs\pack.desktop.toolkit.baseline.neutral.v1.json") @'
{
  "id": "pack.desktop.toolkit.baseline.neutral.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 185 },
  "title": "Desktop app toolkit baseline (neutral)",
  "description": "Turns a desktop app into something shippable: settings, config, offline mode, updates policy, crash handling, packaging contract, startup health.",
  "tags": ["desktop", "toolkit", "release", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": [
    "recipe.desktop.toolkit.baseline.neutral.v1"
  ] }
}
'@

Write-Json (Join-Path $bt "packs\pack.desktop.privacy.first.crash_only.neutral.v1.json") @'
{
  "id": "pack.desktop.privacy.first.crash_only.neutral.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 150 },
  "title": "Privacy-first crash handling (neutral)",
  "description": "Defines crash handling with consent, redaction, retention, and exportable bundles. No background telemetry assumptions.",
  "tags": ["desktop", "privacy", "crash", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": [
    "recipe.desktop.privacy.first.crash_only.neutral.v1"
  ] }
}
'@

# -------------------------
# GRAPHS (public): router for desktop toolkit intents
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.desktop_toolkit.neutral.v1.json") @'
{
  "id": "graph.router.desktop_toolkit.neutral.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: Desktop toolkit (neutral)",
  "description": "Routes desktop-app hardening/shipping intents to neutral packs.",
  "tags": ["router", "desktop", "toolkit", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Input[Desktop app request] --> K{Need?}\n  K -->|toolkit baseline| B[pack.desktop.toolkit.baseline.neutral.v1]\n  K -->|privacy-first crash handling| P[pack.desktop.privacy.first.crash_only.neutral.v1]\n  B --> Done[Done]\n  P --> Done"
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 26 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

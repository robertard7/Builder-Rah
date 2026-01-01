# BlueprintTemplates/Seed-Corpus-Batch25.ps1
# Batch25: Internationalization + localization + accessibility (intent-only), neutral
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
# ATOMS (internal): i18n/l10n/accessibility primitives
# -------------------------

Write-Json (Join-Path $bt "atoms\task.i18n.detect_user_visible_strings.v1.json") @'
{
  "id": "task.i18n.detect_user_visible_strings.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "i18n: Detect user-visible strings",
  "description": "Identifies user-visible strings and surfaces a plan to extract them into a message catalog, without choosing a library.",
  "tags": ["i18n", "strings", "localization"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "i18n_detect_strings",
    "inputs": ["projectRoot", "ecosystem", "uiType?"],
    "outputs": ["stringInventory", "hotspots"],
    "rules": [
      "include UI labels, errors, logs shown to users, validation messages",
      "exclude internal-only debug logs unless they are user-facing",
      "must be reproducible and reviewable"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.i18n.define_message_catalog_contract.v1.json") @'
{
  "id": "task.i18n.define_message_catalog_contract.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "i18n: Define message catalog contract",
  "description": "Defines a neutral message catalog contract: keys, placeholders, plural rules, namespaces, fallback behavior.",
  "tags": ["i18n", "catalog", "contract"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "i18n_define_catalog_contract",
    "inputs": ["projectRoot", "languages?"],
    "outputs": ["catalogContract"],
    "defaults": { "languages": ["en"] },
    "rules": [
      "keys must be stable and deterministic",
      "placeholders must be named, not positional",
      "fallback language must be defined",
      "pluralization and gender rules must be explicit where needed"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.i18n.extract_strings_to_catalog.v1.json") @'
{
  "id": "task.i18n.extract_strings_to_catalog.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "i18n: Extract strings to catalog",
  "description": "Moves user-visible strings into the message catalog contract and wires lookup calls conceptually (no library selection).",
  "tags": ["i18n", "extract", "catalog"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "i18n_extract_strings",
    "inputs": ["projectRoot", "stringInventory", "catalogContract"],
    "outputs": ["catalogFiles", "codeTouchList"],
    "rules": [
      "preserve meaning and context notes for translators",
      "avoid concatenating strings in ways that break grammar",
      "ensure placeholders are consistent across locales"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.i18n.add_locale_selection.v1.json") @'
{
  "id": "task.i18n.add_locale_selection.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "i18n: Add locale selection + fallback",
  "description": "Defines how locale is selected (user setting/env/header) and applied, including fallback chain.",
  "tags": ["i18n", "locale", "fallback"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "i18n_add_locale_selection",
    "inputs": ["projectRoot", "appType?", "catalogContract"],
    "outputs": ["localeSelectionPlan", "fallbackChain"],
    "rules": [
      "must support explicit user choice",
      "must support default locale",
      "must define fallback when translation missing"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.l10n.formatting_rules.v1.json") @'
{
  "id": "task.l10n.formatting_rules.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "l10n: Locale formatting rules",
  "description": "Defines locale-aware formatting rules for dates, numbers, currency, units, sorting, and casing.",
  "tags": ["l10n", "formatting", "locale"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "l10n_define_formatting_rules",
    "inputs": ["projectRoot", "dataTypes?", "markets?"],
    "outputs": ["formattingContract"],
    "rules": [
      "never format dates/numbers via string concatenation",
      "sorting/collation must be locale-aware where user-visible",
      "units and currency must be explicit"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.i18n.rtl_and_layout_checks.v1.json") @'
{
  "id": "task.i18n.rtl_and_layout_checks.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "i18n: RTL and layout checks",
  "description": "Adds neutral checks/plans for RTL locales and UI layout resilience: truncation, wrapping, mirroring, icon direction.",
  "tags": ["i18n", "rtl", "ui"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "i18n_rtl_layout_checks",
    "inputs": ["projectRoot", "uiType?"],
    "outputs": ["rtlChecklist", "layoutRiskList"],
    "rules": [
      "design for text expansion (30-50%)",
      "avoid fixed-width label assumptions",
      "ensure icons with direction meaning can mirror"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.i18n.translation_workflow_contract.v1.json") @'
{
  "id": "task.i18n.translation_workflow_contract.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "i18n: Translation workflow contract",
  "description": "Defines translation workflow: source of truth, review steps, QA checks, and how updates are merged.",
  "tags": ["i18n", "workflow", "qa"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "i18n_define_translation_workflow",
    "inputs": ["projectRoot", "languages", "teamShape?"],
    "outputs": ["translationWorkflow"],
    "rules": [
      "translations must be reviewable via diff",
      "must include missing-key detection",
      "must include pseudo-locale testing strategy"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.a11y.baseline_checks.neutral.v1.json") @'
{
  "id": "task.a11y.baseline_checks.neutral.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Accessibility: Baseline checks (neutral)",
  "description": "Defines baseline accessibility checks: keyboard navigation, focus order, contrast, labels, ARIA where relevant, and screen-reader semantics.",
  "tags": ["a11y", "accessibility", "ui"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "a11y_define_baseline_checks",
    "inputs": ["projectRoot", "uiType?"],
    "outputs": ["a11yChecklist", "highRiskAreas"],
    "rules": [
      "keyboard-only use must be possible for core flows",
      "focus visible and logical",
      "errors must be announced and associated with fields",
      "avoid color-only meaning"
    ]
  }
}
'@

# -------------------------
# RECIPES (public): i18n/l10n/a11y flows
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.i18n.l10n.baseline.neutral.v1.json") @'
{
  "id": "recipe.i18n.l10n.baseline.neutral.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 82 },
  "title": "i18n + l10n baseline (neutral)",
  "description": "Extracts user-visible strings into a catalog contract, adds locale selection, and defines formatting rules.",
  "tags": ["i18n", "l10n", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.i18n.detect_user_visible_strings.v1" },
    { "use": "task.i18n.define_message_catalog_contract.v1" },
    { "use": "task.i18n.extract_strings_to_catalog.v1" },
    { "use": "task.i18n.add_locale_selection.v1" },
    { "use": "task.l10n.formatting_rules.v1" },
    { "use": "task.i18n.translation_workflow_contract.v1" },
    { "use": "task.i18n.rtl_and_layout_checks.v1" }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.a11y.baseline.neutral.v1.json") @'
{
  "id": "recipe.a11y.baseline.neutral.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 78 },
  "title": "Accessibility baseline (neutral)",
  "description": "Adds baseline accessibility checklist and risk scan without selecting specific linters or toolchains.",
  "tags": ["a11y", "accessibility", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.a11y.baseline_checks.neutral.v1" }
  ] }
}
'@

# -------------------------
# PACKS (public): i18n/l10n/a11y entrypoints
# -------------------------

Write-Json (Join-Path $bt "packs\pack.i18n.l10n.baseline.neutral.v1.json") @'
{
  "id": "pack.i18n.l10n.baseline.neutral.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 175 },
  "title": "i18n + l10n baseline (neutral)",
  "description": "Makes your app ready for multiple languages and locales: catalogs, locale selection, formatting rules, and translation workflow.",
  "tags": ["i18n", "l10n", "neutral", "localization"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.i18n.l10n.baseline.neutral.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.a11y.baseline.neutral.v1.json") @'
{
  "id": "pack.a11y.baseline.neutral.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 160 },
  "title": "Accessibility baseline (neutral)",
  "description": "Baseline accessibility checklist and remediation plan, neutral to UI framework and tooling.",
  "tags": ["a11y", "accessibility", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.a11y.baseline.neutral.v1"] }
}
'@

# -------------------------
# GRAPHS (public): router for i18n/a11y intents
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.i18n_l10n_a11y.neutral.v1.json") @'
{
  "id": "graph.router.i18n_l10n_a11y.neutral.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: i18n/l10n + accessibility (neutral)",
  "description": "Routes localization and accessibility intents to neutral packs.",
  "tags": ["router", "i18n", "l10n", "a11y", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Input[Localization/A11y request] --> Kind{need?}\n  Kind -->|i18n + l10n baseline| I[pack.i18n.l10n.baseline.neutral.v1]\n  Kind -->|accessibility baseline| A[pack.a11y.baseline.neutral.v1]\n  I --> Done[Done]\n  A --> Done"
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 25 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

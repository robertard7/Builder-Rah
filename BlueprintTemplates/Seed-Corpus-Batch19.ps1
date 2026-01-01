# BlueprintTemplates/Seed-Corpus-Batch19.ps1
# Batch19: Frontend + Desktop UX packs/recipes/atoms/graphs (React/Vite baseline, desktop wrappers, .NET UX modernization)
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
# ATOMS (internal): frontend scaffolds and desktop wrappers (abstract)
# -------------------------

Write-Json (Join-Path $bt "atoms\task.ui.scaffold.react_spa.v1.json") @'
{
  "id": "task.ui.scaffold.react_spa.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "UI: Scaffold React SPA (generic)",
  "description": "Creates a React SPA baseline with routing, layout shell, and env-config hooks. Abstract; orchestrator chooses actual scaffolding strategy.",
  "tags": ["ui", "frontend", "react", "spa"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "ui_scaffold_spa",
    "inputs": ["projectRoot", "appName", "options?"],
    "outputs": ["uiArtifacts"],
    "defaults": { "routing": true, "linting": true, "testSmoke": true }
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.ui.add_routes_and_layout.v1.json") @'
{
  "id": "task.ui.add_routes_and_layout.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "UI: Add routes + layout shell",
  "description": "Adds a simple navigation layout and a small set of routes/pages. Abstract.",
  "tags": ["ui", "routes", "layout"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "ui_add_routes_layout",
    "inputs": ["projectRoot", "options?"],
    "outputs": ["routesAdded"]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.ui.add_state_and_api_client.v1.json") @'
{
  "id": "task.ui.add_state_and_api_client.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "UI: Add state + API client seam",
  "description": "Adds a minimal state layer and a typed API client seam with config-driven base URL. Abstract.",
  "tags": ["ui", "state", "api_client"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "ui_add_state_api_client",
    "inputs": ["projectRoot", "options?"],
    "outputs": ["clientArtifacts"],
    "rules": [
      "base URL comes from config/env",
      "timeouts required",
      "no provider references"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.desktop.wrap_web_app.v1.json") @'
{
  "id": "task.desktop.wrap_web_app.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 4 },
  "title": "Desktop: Wrap web app into desktop shell (generic)",
  "description": "Creates a desktop wrapper for an existing web UI with app window config, menus, and auto-update hooks as placeholders. Abstract.",
  "tags": ["desktop", "wrapper"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "desktop_wrap_web_app",
    "inputs": ["projectRoot", "wrapperType", "options?"],
    "outputs": ["desktopArtifacts"],
    "wrapperType": "electron|tauri|webview"
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.dotnet.ux.modernize.winforms_patterns.v1.json") @'
{
  "id": "task.dotnet.ux.modernize.winforms_patterns.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "UX: Modernize WinForms patterns",
  "description": "Applies UX modernization patterns to WinForms apps: layout consistency, async UI rules, centralized theme/styles, navigation patterns. Abstract.",
  "tags": ["dotnet", "winforms", "ux", "modernize"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "dotnet_winforms_modernize_patterns",
    "inputs": ["projectRoot", "options?"],
    "outputs": ["changesSummary"],
    "rules": [
      "no toolkit hardcoding",
      "prefer consistent layout grid/panels",
      "avoid blocking UI thread",
      "add centralized theme settings file"
    ]
  }
}
'@

# -------------------------
# RECIPES (public): SPA baseline + desktop wrappers + WinForms modernization
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.ui.react_spa.baseline.v1.json") @'
{
  "id": "recipe.ui.react_spa.baseline.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 78 },
  "title": "UI Baseline: React SPA",
  "description": "Scaffolds a React SPA baseline: routing, layout, state + API client seam, and basic lint/test hooks (abstract).",
  "tags": ["ui", "react", "spa"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.ui.scaffold.react_spa.v1" },
      { "use": "task.ui.add_routes_and_layout.v1" },
      { "use": "task.ui.add_state_and_api_client.v1" }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.desktop.wrapper.electron.v1.json") @'
{
  "id": "recipe.desktop.wrapper.electron.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 68 },
  "title": "Desktop Wrapper: Electron (abstract)",
  "description": "Wraps a web UI in an Electron-style desktop shell (abstract, no commands).",
  "tags": ["desktop", "electron", "wrapper"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.desktop.wrap_web_app.v1", "with": { "wrapperType": "electron" } }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.desktop.wrapper.tauri.v1.json") @'
{
  "id": "recipe.desktop.wrapper.tauri.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 68 },
  "title": "Desktop Wrapper: Tauri (abstract)",
  "description": "Wraps a web UI in a Tauri-style desktop shell (abstract, no commands).",
  "tags": ["desktop", "tauri", "wrapper"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.desktop.wrap_web_app.v1", "with": { "wrapperType": "tauri" } }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.dotnet.winforms.ux.modernize.v1.json") @'
{
  "id": "recipe.dotnet.winforms.ux.modernize.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 72 },
  "title": "Modernize UX: WinForms baseline patterns",
  "description": "Applies UX modernization patterns to WinForms apps: async UI rules, centralized theme, consistent layout patterns (abstract).",
  "tags": ["dotnet", "winforms", "ux", "modernize"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.dotnet.ux.modernize.winforms_patterns.v1" }
    ]
  }
}
'@

# -------------------------
# PACKS (public): entrypoints for UI + desktop UX
# -------------------------

Write-Json (Join-Path $bt "packs\pack.ui.frontend.react_spa.v1.json") @'
{
  "id": "pack.ui.frontend.react_spa.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 165 },
  "title": "Create Frontend: React SPA baseline",
  "description": "Creates a React SPA UI baseline with routes/layout and API client seam (abstract).",
  "tags": ["ui", "frontend", "react", "spa"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.ui.react_spa.baseline.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.desktop.wrapper.electron.v1.json") @'
{
  "id": "pack.desktop.wrapper.electron.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 145 },
  "title": "Wrap UI as Desktop App (Electron-style)",
  "description": "Wraps an existing web UI into an Electron-style desktop shell (abstract).",
  "tags": ["desktop", "electron", "wrapper"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.desktop.wrapper.electron.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.desktop.wrapper.tauri.v1.json") @'
{
  "id": "pack.desktop.wrapper.tauri.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 145 },
  "title": "Wrap UI as Desktop App (Tauri-style)",
  "description": "Wraps an existing web UI into a Tauri-style desktop shell (abstract).",
  "tags": ["desktop", "tauri", "wrapper"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.desktop.wrapper.tauri.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.dotnet.winforms.ux.modernize.v1.json") @'
{
  "id": "pack.dotnet.winforms.ux.modernize.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 150 },
  "title": "Modernize WinForms UX (baseline patterns)",
  "description": "Applies a set of WinForms UX modernization patterns without hardcoding any UI library/toolkit.",
  "tags": ["dotnet", "winforms", "ux", "modernize"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.dotnet.winforms.ux.modernize.v1"] }
}
'@

# -------------------------
# GRAPHS (public): router for frontend + desktop UX
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.frontend_and_desktop_ux.v1.json") @'
{
  "id": "graph.router.frontend_and_desktop_ux.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: Frontend + Desktop UX",
  "description": "Routes UI and desktop-wrapper requests to the appropriate packs.",
  "tags": ["router", "ui", "desktop"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Input[UI/Desktop request] --> UI{frontend?}\n  UI -->|react spa| React[pack.ui.frontend.react_spa.v1]\n  UI -->|desktop wrapper| Wrap{which?}\n  Wrap -->|electron| E[pack.desktop.wrapper.electron.v1]\n  Wrap -->|tauri| T[pack.desktop.wrapper.tauri.v1]\n  Input -->|winforms ux| WF[pack.dotnet.winforms.ux.modernize.v1]\n  React --> Done[Done]\n  E --> Done\n  T --> Done\n  WF --> Done"
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 19 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

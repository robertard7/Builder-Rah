# BlueprintTemplates/Seed-Corpus-Batch12.ps1
# Batch12: container/dev environment scaffolds (Dockerfile, compose, devcontainer, env docs)
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
# ATOMS (internal)
# -------------------------

Write-Json (Join-Path $bt "atoms\task.env.scaffold.container_files.v1.json") @'
{
  "id": "task.env.scaffold.container_files.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Env: Scaffold container files",
  "description": "Adds container/dev scaffolding files (Dockerfile, compose, devcontainer) as templates only. No commands, no image pinning required.",
  "tags": ["env", "container", "docker", "devcontainer"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "env_scaffold_container_files",
    "inputs": ["projectRoot", "ecosystem", "profile"],
    "routes": {
      "generic": ["recipe.env.container.generic.v1"],
      "dotnet": ["recipe.env.container.dotnet.v1"],
      "node": ["recipe.env.container.node.v1"],
      "python": ["recipe.env.container.python.v1"],
      "go": ["recipe.env.container.go.v1"],
      "rust": ["recipe.env.container.rust.v1"],
      "java": ["recipe.env.container.java.v1"],
      "cpp": ["recipe.env.container.cpp.v1"]
    }
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.env.scaffold.devcontainer.v1.json") @'
{
  "id": "task.env.scaffold.devcontainer.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Env: Scaffold .devcontainer",
  "description": "Adds a minimal .devcontainer setup that points at the repo and explains customization. No hardcoded images or toolchains required.",
  "tags": ["env", "devcontainer", "vscode"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "env_scaffold_devcontainer",
    "inputs": ["projectRoot"],
    "route": "recipe.env.devcontainer.base.v1"
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.env.scaffold.compose.optional_services.v1.json") @'
{
  "id": "task.env.scaffold.compose.optional_services.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Env: Scaffold docker-compose optional services",
  "description": "Adds a docker-compose file that contains placeholders for optional services (db/cache/queue) without selecting a vendor.",
  "tags": ["env", "compose", "services"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "env_scaffold_compose_optional_services",
    "inputs": ["projectRoot"],
    "route": "recipe.env.compose.optional_services.v1"
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.env.add_local_env_docs.v1.json") @'
{
  "id": "task.env.add_local_env_docs.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "Env: Add local environment docs",
  "description": "Adds docs describing how to run locally vs container, referencing Settings by name only.",
  "tags": ["env", "docs", "settings"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "env_add_local_docs",
    "inputs": ["projectRoot", "ecosystem"],
    "route": "recipe.env.docs.local_and_container.v1"
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.env.apply_role_preset.v1.json") @'
{
  "id": "task.env.apply_role_preset.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "Env: Apply role preset by name",
  "description": "Applies a Settings role preset by name only (no provider/model ids).",
  "tags": ["preset", "settings", "role"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "apply_settings_preset",
    "inputs": ["rolePresetName"],
    "use": "preset.role.by_name.v1"
  }
}
'@

# -------------------------
# PACKS (public)
# -------------------------

Write-Json (Join-Path $bt "packs\pack.env.containerize.project.v1.json") @'
{
  "id": "pack.env.containerize.project.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 165 },
  "title": "Environment: Containerize Project (Scaffold)",
  "description": "Adds Dockerfile/compose/devcontainer scaffolds as templates only. No hardcoded toolchains or images required.",
  "tags": ["env", "container", "docker", "scaffold"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.detect.project_shape.v1",
      "task.env.scaffold.container_files.v1",
      "task.env.scaffold_devcontainer.v1",
      "task.env.scaffold.compose.optional_services.v1",
      "task.env.add_local_env_docs.v1"
    ],
    "defaults": { "ecosystem": "generic", "profile": "default", "rolePresetName": "fast" }
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.env.devcontainer.only.v1.json") @'
{
  "id": "pack.env.devcontainer.only.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 145 },
  "title": "Environment: DevContainer Only",
  "description": "Adds a minimal .devcontainer scaffolding with notes on customization.",
  "tags": ["env", "devcontainer"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.env.scaffold_devcontainer.v1",
      "task.env.add_local_env_docs.v1"
    ],
    "defaults": { "ecosystem": "generic", "rolePresetName": "fast" }
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.env.local_and_container.docs.v1.json") @'
{
  "id": "pack.env.local_and_container.docs.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 135 },
  "title": "Environment: Local + Container Docs",
  "description": "Adds documentation explaining local runs vs container runs, referencing Settings preset names only.",
  "tags": ["env", "docs"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.env.add_local_env_docs.v1"
    ],
    "defaults": { "ecosystem": "generic", "rolePresetName": "fast" }
  }
}
'@

# -------------------------
# GRAPHS (public): router
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.environment.v1.json") @'
{
  "id": "graph.router.environment.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: Environment",
  "description": "Routes environment/container/devcontainer requests to appropriate packs.",
  "tags": ["router", "env", "container"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Input[env request] --> Pick{intent?}\n  Pick -->|containerize| C[pack.env.containerize.project.v1]\n  Pick -->|devcontainer| D[pack.env.devcontainer.only.v1]\n  Pick -->|docs only| Docs[pack.env.local_and_container.docs.v1]\n  Pick -->|unsure| C"
  }
}
'@

# -------------------------
# RECIPES
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.env.docs.local_and_container.v1.json") @'
{
  "id": "recipe.env.docs.local_and_container.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 40 },
  "title": "Env Docs: Local vs Container",
  "description": "Adds docs that describe how to work locally vs container, referencing Settings by name only.",
  "tags": ["env", "docs"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.ensure_dir.v1", "with": { "path": "${projectRoot}/docs" } },
    { "use": "task.add_file.conflict_policy.v1", "with": {
      "path": "${projectRoot}/docs/environment.md",
      "onConflict": "skip",
      "text": "# Environment\n\nThis repo supports both local development and container-based development.\n\n## Local\n- Use your configured environment settings.\n- Use Settings presets by name (example: rolePreset: \"fast\").\n\n## Container\n- Container scaffolding in this repo is a template.\n- Select base images and tool installs according to your environment policy.\n\n## Optional services\n- docker-compose.yml contains placeholders for DB/cache/queue.\n- Choose the vendor and credentials via Settings and runtime configuration.\n"
    } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.env.devcontainer.base.v1.json") @'
{
  "id": "recipe.env.devcontainer.base.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 42 },
  "title": "DevContainer: Base",
  "description": "Adds a minimal .devcontainer config. Intentionally does not pin a particular image/toolchain.",
  "tags": ["env", "devcontainer"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.ensure_dir.v1", "with": { "path": "${projectRoot}/.devcontainer" } },
    { "use": "task.add_file.conflict_policy.v1", "with": {
      "path": "${projectRoot}/.devcontainer/devcontainer.json",
      "onConflict": "skip",
      "text": "{\n  \"name\": \"${projectName}\",\n  \"image\": \"<choose-a-base-image>\",\n  \"workspaceFolder\": \"/workspaces/${projectName}\",\n  \"customizations\": {\n    \"vscode\": {\n      \"settings\": {},\n      \"extensions\": []\n    }\n  },\n  \"remoteUser\": \"vscode\"\n}\n"
    } },
    { "use": "task.add_file.conflict_policy.v1", "with": {
      "path": "${projectRoot}/.devcontainer/README.md",
      "onConflict": "skip",
      "text": "# DevContainer\n\nThis DevContainer config is intentionally minimal.\n\n- Choose an appropriate base image for your environment.\n- Install tools according to your policy.\n- Do not hardcode provider/model/tool ids here.\n"
    } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.env.compose.optional_services.v1.json") @'
{
  "id": "recipe.env.compose.optional_services.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 38 },
  "title": "Compose: Optional services placeholders",
  "description": "Adds docker-compose.yml with commented placeholder services (db/cache/queue) without selecting a vendor.",
  "tags": ["env", "compose", "services"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": {
      "path": "${projectRoot}/docker-compose.yml",
      "onConflict": "skip",
      "text": "version: '3.9'\nservices:\n  app:\n    build:\n      context: .\n      dockerfile: Dockerfile\n    environment:\n      # configure via Settings/runtime\n      - APP_ENV=development\n    ports:\n      - \"${APP_PORT:-8080}:8080\"\n\n  # Optional database (choose vendor + settings)\n  # db:\n  #   image: <choose-db-image>\n  #   environment:\n  #     - DB_USER=<set>\n  #     - DB_PASS=<set>\n\n  # Optional cache (choose vendor + settings)\n  # cache:\n  #   image: <choose-cache-image>\n\n  # Optional queue (choose vendor + settings)\n  # queue:\n  #   image: <choose-queue-image>\n"
    } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.env.container.generic.v1.json") @'
{
  "id": "recipe.env.container.generic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 45 },
  "title": "Container: Generic scaffold",
  "description": "Adds a generic Dockerfile scaffold without pinning a base image or hardcoding tool installs.",
  "tags": ["env", "container", "dockerfile"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": {
      "path": "${projectRoot}/Dockerfile",
      "onConflict": "skip",
      "text": "# Choose a base image according to your environment policy\nFROM <choose-base-image>\n\nWORKDIR /app\n\n# Copy source\nCOPY . /app\n\n# Build/run steps are intentionally not hardcoded here.\n# Use your environment settings to decide tool installs and commands.\n\nEXPOSE 8080\nCMD [\"<choose-entrypoint>\"]\n"
    } }
  ] }
}
'@

# Ecosystem container recipes: thin wrappers that just add notes (no commands)
Write-Json (Join-Path $bt "recipes\recipe.env.container.dotnet.v1.json") @'
{
  "id": "recipe.env.container.dotnet.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 44 },
  "title": "Container: .NET scaffold notes",
  "description": "Generic Dockerfile scaffold plus dotnet notes, no pinned images/commands.",
  "tags": ["env", "container", "dotnet"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "recipe.env.container.generic.v1" },
    { "use": "task.add_file.conflict_policy.v1", "with": {
      "path": "${projectRoot}/docs/container_dotnet.md",
      "onConflict": "skip",
      "text": "# Container (.NET)\n\n- Choose SDK/runtime images per policy.\n- Use Settings for build/test/run decisions.\n- Avoid hardcoding provider/model/tool ids.\n"
    } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.env.container.node.v1.json") @'
{
  "id": "recipe.env.container.node.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 44 },
  "title": "Container: Node scaffold notes",
  "description": "Generic Dockerfile scaffold plus node notes.",
  "tags": ["env", "container", "node"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "recipe.env.container.generic.v1" },
    { "use": "task.add_file.conflict_policy.v1", "with": {
      "path": "${projectRoot}/docs/container_node.md",
      "onConflict": "skip",
      "text": "# Container (Node)\n\n- Choose node base image per policy.\n- Decide install/build/run steps via Settings.\n- Keep runtime configuration external.\n"
    } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.env.container.python.v1.json") @'
{
  "id": "recipe.env.container.python.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 44 },
  "title": "Container: Python scaffold notes",
  "description": "Generic Dockerfile scaffold plus python notes.",
  "tags": ["env", "container", "python"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "recipe.env.container.generic.v1" },
    { "use": "task.add_file.conflict_policy.v1", "with": {
      "path": "${projectRoot}/docs/container_python.md",
      "onConflict": "skip",
      "text": "# Container (Python)\n\n- Choose python base image per policy.\n- Dependency resolution and entrypoint are environment-defined.\n- Keep secrets/config outside the image.\n"
    } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.env.container.go.v1.json") @'
{
  "id": "recipe.env.container.go.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 44 },
  "title": "Container: Go scaffold notes",
  "description": "Generic Dockerfile scaffold plus go notes.",
  "tags": ["env", "container", "go"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "recipe.env.container.generic.v1" },
    { "use": "task.add_file.conflict_policy.v1", "with": {
      "path": "${projectRoot}/docs/container_go.md",
      "onConflict": "skip",
      "text": "# Container (Go)\n\n- Choose builder/runtime approach per policy.\n- Build/run decisions belong in Settings.\n- Keep images minimal.\n"
    } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.env.container.rust.v1.json") @'
{
  "id": "recipe.env.container.rust.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 44 },
  "title": "Container: Rust scaffold notes",
  "description": "Generic Dockerfile scaffold plus rust notes.",
  "tags": ["env", "container", "rust"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "recipe.env.container.generic.v1" },
    { "use": "task.add_file.conflict_policy.v1", "with": {
      "path": "${projectRoot}/docs/container_rust.md",
      "onConflict": "skip",
      "text": "# Container (Rust)\n\n- Decide toolchain and build strategy via Settings.\n- Prefer minimal runtime images where practical.\n"
    } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.env.container.java.v1.json") @'
{
  "id": "recipe.env.container.java.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 44 },
  "title": "Container: Java scaffold notes",
  "description": "Generic Dockerfile scaffold plus java notes.",
  "tags": ["env", "container", "java"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "recipe.env.container.generic.v1" },
    { "use": "task.add_file.conflict_policy.v1", "with": {
      "path": "${projectRoot}/docs/container_java.md",
      "onConflict": "skip",
      "text": "# Container (Java)\n\n- Choose JDK base image per policy.\n- Build/run is Settings-driven.\n- Keep config external.\n"
    } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.env.container.cpp.v1.json") @'
{
  "id": "recipe.env.container.cpp.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 44 },
  "title": "Container: C++ scaffold notes",
  "description": "Generic Dockerfile scaffold plus CMake notes.",
  "tags": ["env", "container", "cpp"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "recipe.env.container.generic.v1" },
    { "use": "task.add_file.conflict_policy.v1", "with": {
      "path": "${projectRoot}/docs/container_cpp.md",
      "onConflict": "skip",
      "text": "# Container (C++)\n\n- Choose compiler/toolchain per policy.\n- Keep build strategy in Settings.\n- Prefer clean build directories.\n"
    } }
  ] }
}
'@

Write-Host ""
Write-Host "Done seeding batch 12 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

# BlueprintTemplates/Seed-Corpus-Batch32.ps1
# Batch32: Artifact Output Contracts (neutral)
# Internal atoms define artifact manifests/receipts/contracts; recipes/packs wire them to publish/deploy flows.
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
# ATOMS (internal): artifact manifest + receipts
# -------------------------

Write-Json (Join-Path $bt "atoms\task.artifacts.define_manifest_contract.v1.json") @'
{
  "id": "task.artifacts.define_manifest_contract.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Artifacts: Define artifact manifest contract",
  "description": "Defines a neutral, provider-agnostic artifact manifest schema (what was built, where it is, how to verify).",
  "tags": ["artifacts", "manifest", "publish", "deploy", "contract"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "define_artifact_manifest_contract",
    "inputs": ["ecosystem?", "projectRoot", "entrypoints?", "buildOutputs?"],
    "outputs": ["artifactManifestContract"],
    "schema": {
      "fields": [
        "projectName",
        "version",
        "ecosystem",
        "profiles",
        "artifacts[]: { kind, path, platform?, arch?, hash?, sizeBytes?, producedByStep?, notes? }",
        "checks[]: { name, verb, target?, expected?, notes? }",
        "provenance: { updatedUtc, inputsHash?, receiptsRef? }"
      ]
    },
    "rules": [
      "paths must be relative to ${projectRoot} when possible",
      "never include secrets in manifests",
      "hashes are optional; include only if already computed by engine"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.artifacts.collect_outputs_and_receipts.v1.json") @'
{
  "id": "task.artifacts.collect_outputs_and_receipts.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Artifacts: Collect outputs and receipts",
  "description": "Collects build/test/publish receipts and enumerates produced outputs for manifest generation.",
  "tags": ["artifacts", "receipts", "build", "publish"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "collect_artifact_outputs_and_receipts",
    "inputs": ["projectRoot", "goalVerb", "validationResult?", "patchReceipts?"],
    "outputs": ["artifactEvidenceBundle"],
    "rules": [
      "prefer deterministic evidence (paths, sizes, hashes if present)",
      "include links to prior receipts when available",
      "do not guess outputs; only report what exists or was reported"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.artifacts.generate_manifest.v1.json") @'
{
  "id": "task.artifacts.generate_manifest.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 8 },
  "title": "Artifacts: Generate artifact manifest",
  "description": "Generates an artifact manifest instance from the contract and evidence bundle.",
  "tags": ["artifacts", "manifest", "generate"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "generate_artifact_manifest",
    "inputs": ["artifactManifestContract", "artifactEvidenceBundle", "projectName?", "version?"],
    "outputs": ["artifactManifest"],
    "rules": [
      "include verification checks appropriate to goalVerb",
      "include platform/arch only if detected",
      "manifest must be stable across reruns when outputs unchanged"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.artifacts.write_manifest_file.v1.json") @'
{
  "id": "task.artifacts.write_manifest_file.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Artifacts: Write manifest file",
  "description": "Writes artifact manifest JSON to a deterministic location under the repo (no hardcoded paths).",
  "tags": ["artifacts", "manifest", "write"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "write_artifact_manifest_file",
    "inputs": ["projectRoot", "artifactManifest", "relativeOutPath?"],
    "outputs": ["manifestPath"],
    "defaults": { "relativeOutPath": ".rah/artifacts/manifest.json" },
    "rules": [
      "ensure directory exists before writing",
      "never write outside ${projectRoot}",
      "if file exists, replace only if content differs"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.artifacts.define_verification_gates.v1.json") @'
{
  "id": "task.artifacts.define_verification_gates.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 8 },
  "title": "Artifacts: Define verification gates",
  "description": "Defines minimum verification gates for packaging/publishing/deploy: build, test, smoke run, and lint/format where applicable.",
  "tags": ["artifacts", "verify", "gates", "publish", "deploy"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "define_verification_gates",
    "inputs": ["ecosystem?", "profile?", "riskProfile?"],
    "outputs": ["verificationGates"],
    "defaults": {
      "riskProfile": "standard",
      "requireBuild": true,
      "requireTests": true,
      "requireFormatLint": false,
      "requireSmokeRun": true
    },
    "rules": [
      "for libraries, smoke run may be skipped if tests cover usage",
      "for services, require health check or basic request smoke if available",
      "if gates cannot be satisfied, route to WAIT_USER with options"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.artifacts.validate_gates_or_wait_user.v1.json") @'
{
  "id": "task.artifacts.validate_gates_or_wait_user.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Artifacts: Validate gates or WAIT_USER",
  "description": "Ensures verification gates are satisfied. If missing (no tests, no run target), asks user to approve alternate gate set.",
  "tags": ["artifacts", "gates", "wait_user", "publish"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "validate_gates_or_wait_user",
    "inputs": ["projectRoot", "verificationGates", "detectionReport?", "evidenceBundle?"],
    "outputs": ["gateDecision", "questions?", "waitUser?"],
    "waitUser": false,
    "questionRules": [
      "ask only when a required gate cannot be run due to missing targets",
      "offer options: scaffold tests, relax gate, provide custom smoke command hint (as intent, not command)",
      "never ask for provider/model/toolchain ids"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.artifacts.publish_plan_contract.neutral.v1.json") @'
{
  "id": "task.artifacts.publish_plan_contract.neutral.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Publish: Define publish plan contract (neutral)",
  "description": "Defines a neutral publish plan describing what will be published, where, and required inputs, without hardcoding registries or tools.",
  "tags": ["publish", "contract", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "define_publish_plan_contract",
    "inputs": ["ecosystem?", "artifactManifest", "versioningStrategy?"],
    "outputs": ["publishPlanContract"],
    "schema": {
      "fields": [
        "targetType: registry|store|internal",
        "packageId",
        "version",
        "artifactsRef",
        "requiredInputs[]",
        "verificationGatesRef",
        "notes"
      ]
    },
    "rules": [
      "do not assume a specific registry (nuget/npm/pypi/etc.)",
      "do not include credentials",
      "allow user or settings to map targetType -> concrete destination"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.artifacts.emit_publish_plan_wait_user.v1.json") @'
{
  "id": "task.artifacts.emit_publish_plan_wait_user.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Publish: Emit publish plan for approval (WAIT_USER)",
  "description": "Produces a publish plan summary for user approval when destination details are unknown or risk is high.",
  "tags": ["publish", "wait_user", "plan"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "emit_wait_user_questions",
    "inputs": ["publishPlanContract", "verificationGates", "artifactManifest"],
    "outputs": ["questions", "waitUser"],
    "waitUser": true,
    "questionRules": [
      "ask which destination targetType should map to (by settings name, not ids)",
      "ask if this is a dry-run plan only or execute publish",
      "confirm version and packageId if ambiguous"
    ]
  }
}
'@

# -------------------------
# RECIPES (public): publish/deploy artifact contracts
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.artifacts.manifest_and_gates.baseline.v1.json") @'
{
  "id": "recipe.artifacts.manifest_and_gates.baseline.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 85 },
  "title": "Artifacts: Manifest + verification gates (baseline)",
  "description": "Defines artifact manifest contract, collects evidence, generates/writes manifest, and defines/validates verification gates.",
  "tags": ["artifacts", "manifest", "verify", "gates"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.detect.ecosystem_and_entrypoints.v1" },
    { "use": "task.artifacts.define_manifest_contract.v1" },
    { "use": "task.artifacts.collect_outputs_and_receipts.v1" },
    { "use": "task.artifacts.generate_manifest.v1" },
    { "use": "task.artifacts.write_manifest_file.v1" },
    { "use": "task.artifacts.define_verification_gates.v1" },
    { "use": "task.artifacts.validate_gates_or_wait_user.v1" }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.publish.neutral.plan_manifest_gates.v1.json") @'
{
  "id": "recipe.publish.neutral.plan_manifest_gates.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 84 },
  "title": "Publish (neutral): Plan + manifest + gates",
  "description": "Produces an artifact manifest and a publish plan contract; routes to WAIT_USER to approve destination mapping when needed.",
  "tags": ["publish", "neutral", "manifest", "gates"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "recipe.artifacts.manifest_and_gates.baseline.v1" },
    { "use": "task.artifacts.publish_plan_contract.neutral.v1" },
    { "use": "task.artifacts.emit_publish_plan_wait_user.v1" }
  ] }
}
'@

# -------------------------
# PACKS (public)
# -------------------------

Write-Json (Join-Path $bt "packs\pack.artifacts.manifest.baseline.v1.json") @'
{
  "id": "pack.artifacts.manifest.baseline.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 160 },
  "title": "Artifacts: Generate manifest + verification gates",
  "description": "Generates a deterministic artifact manifest and defines verification gates. Asks user only if a required gate cannot run.",
  "tags": ["artifacts", "manifest", "verify", "gates"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.artifacts.manifest_and_gates.baseline.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.package.publish.contract_first.neutral.v1.json") @'
{
  "id": "pack.package.publish.contract_first.neutral.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 175 },
  "title": "Publish: Contract-first (neutral) with manifest + gates",
  "description": "Creates artifact manifest + verification gates and produces a publish plan contract for approval. Does not hardcode registry/tooling.",
  "tags": ["publish", "neutral", "contract", "manifest", "gates"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.publish.neutral.plan_manifest_gates.v1"] }
}
'@

# -------------------------
# GRAPH (public): router for artifacts/publish contracts
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.artifacts_and_publish_contracts.v1.json") @'
{
  "id": "graph.router.artifacts_and_publish_contracts.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: Artifacts + publish contracts",
  "description": "Routes packaging/publishing intents through artifact manifests and verification gates, then emits publish plan for approval.",
  "tags": ["router", "artifacts", "publish", "contracts"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  U[User intent: package/publish] --> A[pack.artifacts.manifest.baseline.v1]\n  A --> G{Gates satisfied?}\n  G -->|yes| P[pack.package.publish.contract_first.neutral.v1]\n  G -->|WAIT_USER| W[WAIT_USER]\n  P --> Done[Done]\n  W --> P\n  P -->|fail| D[pack.diagnose.unstoppable.v1]\n  D --> Done"
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 32 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

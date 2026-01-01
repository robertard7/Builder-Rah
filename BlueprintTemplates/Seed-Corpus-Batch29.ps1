# BlueprintTemplates/Seed-Corpus-Batch29.ps1
# Batch29: Security hardening (neutral): threat model, input validation, dependency policy, secrets policy, least privilege,
# secure defaults, security docs, and validation gates.
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
# ATOMS (internal): security primitives (neutral)
# -------------------------

Write-Json (Join-Path $bt "atoms\task.sec.wait_user.risk_profile_and_data.v1.json") @'
{
  "id": "task.sec.wait_user.risk_profile_and_data.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Security: Ask for risk profile + data categories (WAIT_USER)",
  "description": "If security requirements are unclear, asks for minimal risk profile, auth needs, and sensitive data categories.",
  "tags": ["security", "wait_user", "risk"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "emit_wait_user_questions",
    "inputs": ["projectRoot", "appType?", "exposedToInternet?"],
    "outputs": ["questions", "waitUser"],
    "waitUser": true,
    "questionRules": [
      "ask risk profile: low/standard/high",
      "ask if app is internet-facing and if it has auth",
      "ask sensitive data categories: none/pii/payment/health/keys",
      "ask deployment environment constraints (basic, enterprise, regulated)",
      "never ask for provider/toolchain/model ids"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.sec.threat_model.baseline.v1.json") @'
{
  "id": "task.sec.threat_model.baseline.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 8 },
  "title": "Security: Threat model baseline",
  "description": "Defines assets, entry points, trust boundaries, threat categories, and mitigations at a high level.",
  "tags": ["security", "threat_model", "baseline"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "sec_threat_model_baseline",
    "inputs": ["projectRoot", "appType?", "riskProfile", "dataCategories?"],
    "outputs": ["threatModel"],
    "rules": [
      "identify assets and attackers relevant to app type",
      "define trust boundaries and entry points",
      "list top threats and mitigations",
      "avoid vendor-specific references"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.sec.input_validation_contract.v1.json") @'
{
  "id": "task.sec.input_validation_contract.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 9 },
  "title": "Security: Input validation contract",
  "description": "Defines validation rules for inputs: parsing, bounds, allowlists, encoding, and error behavior.",
  "tags": ["security", "validation", "contract"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "sec_define_input_validation_contract",
    "inputs": ["projectRoot", "interfaces", "riskProfile"],
    "outputs": ["validationContract"],
    "rules": [
      "prefer allowlists over blocklists",
      "validate length, type, format, and ranges",
      "normalize encoding and reject ambiguous inputs",
      "errors must not leak secrets or internals"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.sec.output_encoding_contract.v1.json") @'
{
  "id": "task.sec.output_encoding_contract.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 8 },
  "title": "Security: Output encoding + error model contract",
  "description": "Defines safe output encoding rules and a consistent error model that avoids leaking sensitive details.",
  "tags": ["security", "encoding", "errors"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "sec_define_output_encoding_contract",
    "inputs": ["projectRoot", "interfaces", "riskProfile"],
    "outputs": ["outputContract", "errorModel"],
    "rules": [
      "encode outputs appropriate to context (html/json/cli)",
      "ensure errors are stable, safe, and non-sensitive",
      "provide correlation id for support without leaking internals"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.sec.secrets_policy.v1.json") @'
{
  "id": "task.sec.secrets_policy.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 10 },
  "title": "Security: Secrets policy contract",
  "description": "Defines how secrets are stored, injected, rotated, and never committed. Neutral on specific secret managers.",
  "tags": ["security", "secrets", "policy"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "sec_define_secrets_policy",
    "inputs": ["projectRoot", "riskProfile", "deploymentConstraints?"],
    "outputs": ["secretsPolicy"],
    "rules": [
      "no secrets in repo, logs, traces, or error messages",
      "define local dev secret injection approach (env/config) abstractly",
      "define rotation and revoke expectations conceptually",
      "document required secret names and formats"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.sec.dependency_policy.v1.json") @'
{
  "id": "task.sec.dependency_policy.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 9 },
  "title": "Security: Dependency policy + upgrade strategy",
  "description": "Defines dependency constraints, upgrade cadence, and minimal triage steps for vulnerable dependencies.",
  "tags": ["security", "dependencies", "supply_chain"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "sec_define_dependency_policy",
    "inputs": ["projectRoot", "ecosystem?", "riskProfile"],
    "outputs": ["dependencyPolicy"],
    "rules": [
      "pin or constrain versions where appropriate",
      "avoid unmaintained dependencies where possible",
      "define an upgrade cadence and emergency patch flow",
      "document how to review transitive dependencies"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.sec.least_privilege_contract.v1.json") @'
{
  "id": "task.sec.least_privilege_contract.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 9 },
  "title": "Security: Least privilege + permissions contract",
  "description": "Defines minimal permissions required for runtime operations and restricts access to files/network/actions.",
  "tags": ["security", "least_privilege", "permissions"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "sec_define_least_privilege_contract",
    "inputs": ["projectRoot", "runtimeActions", "riskProfile"],
    "outputs": ["privilegeContract"],
    "rules": [
      "default deny, explicitly allow required actions",
      "separate admin operations from user paths where relevant",
      "avoid running with elevated privileges unless required"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.sec.secure_defaults_checklist.v1.json") @'
{
  "id": "task.sec.secure_defaults_checklist.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 8 },
  "title": "Security: Secure defaults checklist",
  "description": "Creates a checklist for secure defaults (timeouts, limits, safe parsing, safe headers, safe file handling).",
  "tags": ["security", "defaults", "checklist"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "sec_define_secure_defaults_checklist",
    "inputs": ["projectRoot", "appType?", "riskProfile"],
    "outputs": ["secureDefaultsChecklist"],
    "rules": [
      "set sane timeouts and size limits",
      "avoid unsafe deserialization patterns",
      "restrict file path handling to projectRoot or configured safe dirs",
      "ensure logging/tracing redaction policy is compatible"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.sec.scaffold_security_docs.v1.json") @'
{
  "id": "task.sec.scaffold_security_docs.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Security: Scaffold SECURITY.md + reporting policy",
  "description": "Adds neutral security docs: reporting policy, supported versions, and hardening notes.",
  "tags": ["security", "docs", "policy"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "sec_scaffold_security_docs",
    "inputs": ["projectRoot", "riskProfile", "contactMethod?"],
    "outputs": ["docsAdded"],
    "rules": [
      "do not include private emails unless provided",
      "include disclosure expectations and response timeline placeholders",
      "include dependency policy and release policy references"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.sec.scaffold_security_gates_in_ci.v1.json") @'
{
  "id": "task.sec.scaffold_security_gates_in_ci.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Security: Scaffold CI security gates (neutral)",
  "description": "Adds neutral CI gates: dependency review step placeholder, secret leak detection placeholder, and baseline checks.",
  "tags": ["security", "ci", "gates"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "sec_scaffold_ci_security_gates",
    "inputs": ["projectRoot", "ciSystem?", "dependencyPolicy", "secretsPolicy"],
    "outputs": ["changedFiles", "gatesAdded"],
    "rules": [
      "do not hardcode a CI vendor; keep as docs/config placeholders",
      "ensure gates fail clearly when violations are detected",
      "avoid adding toolchain assumptions"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.sec.validate_security_baseline.v1.json") @'
{
  "id": "task.sec.validate_security_baseline.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Security: Validate baseline coverage",
  "description": "Checks that threat model, validation contract, secrets policy, dependency policy, least privilege, and secure defaults exist.",
  "tags": ["security", "validate", "baseline"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "sec_validate_baseline",
    "inputs": ["projectRoot"],
    "outputs": ["coverageReport", "gaps"],
    "rules": [
      "report missing items as actionable to-dos",
      "do not recommend provider/toolchain/model changes",
      "prefer minimal changes and documentation if implementation is out of scope"
    ]
  }
}
'@

# -------------------------
# RECIPES (public): security bundles
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.security.baseline.neutral.v1.json") @'
{
  "id": "recipe.security.baseline.neutral.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 92 },
  "title": "Security baseline (neutral)",
  "description": "Threat model + validation/encoding contracts + secrets + dependency policy + least privilege + secure defaults + docs + validation.",
  "tags": ["security", "baseline", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.sec.wait_user.risk_profile_and_data.v1" },
    { "use": "task.sec.threat_model.baseline.v1" },
    { "use": "task.sec.input_validation_contract.v1" },
    { "use": "task.sec.output_encoding_contract.v1" },
    { "use": "task.sec.secrets_policy.v1" },
    { "use": "task.sec.dependency_policy.v1" },
    { "use": "task.sec.least_privilege_contract.v1" },
    { "use": "task.sec.secure_defaults_checklist.v1" },
    { "use": "task.sec.scaffold_security_docs.v1" },
    { "use": "task.sec.scaffold_security_gates_in_ci.v1" },
    { "use": "task.sec.validate_security_baseline.v1" }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.security.quick_hardening_docs_first.v1.json") @'
{
  "id": "recipe.security.quick_hardening_docs_first.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 78 },
  "title": "Security quick hardening (docs-first)",
  "description": "Lightweight path: define policies/contracts and add docs/checklists first, with validation. Implementation can follow later.",
  "tags": ["security", "hardening", "docs", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.sec.wait_user.risk_profile_and_data.v1" },
    { "use": "task.sec.threat_model.baseline.v1" },
    { "use": "task.sec.secrets_policy.v1" },
    { "use": "task.sec.dependency_policy.v1" },
    { "use": "task.sec.secure_defaults_checklist.v1" },
    { "use": "task.sec.scaffold_security_docs.v1" },
    { "use": "task.sec.validate_security_baseline.v1" }
  ] }
}
'@

# -------------------------
# PACKS (public): entrypoints
# -------------------------

Write-Json (Join-Path $bt "packs\pack.security.baseline.neutral.v1.json") @'
{
  "id": "pack.security.baseline.neutral.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 190 },
  "title": "Security baseline (neutral)",
  "description": "Baseline security for any repo: threat model, validation/encoding, secrets, dependencies, least privilege, secure defaults, docs, CI gates (placeholders), and validation.",
  "tags": ["security", "baseline", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.security.baseline.neutral.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.security.quick_hardening_docs_first.v1.json") @'
{
  "id": "pack.security.quick_hardening_docs_first.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 160 },
  "title": "Security quick hardening (docs-first)",
  "description": "Fast path: define security policies/contracts and docs/checklists first. Great when code changes need review.",
  "tags": ["security", "hardening", "docs", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.security.quick_hardening_docs_first.v1"] }
}
'@

# -------------------------
# GRAPHS (public): router for security intents
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.security_hardening.neutral.v1.json") @'
{
  "id": "graph.router.security_hardening.neutral.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: Security hardening (neutral)",
  "description": "Routes security-related intents to baseline hardening or docs-first quick path.",
  "tags": ["router", "security", "hardening", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Input[Security request] --> Q{Scope?}\n  Q -->|baseline hardening| B[pack.security.baseline.neutral.v1]\n  Q -->|docs-first quick| D[pack.security.quick_hardening_docs_first.v1]\n  B --> Done[Done]\n  D --> Done"
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 29 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

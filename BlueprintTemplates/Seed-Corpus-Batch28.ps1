# BlueprintTemplates/Seed-Corpus-Batch28.ps1
# Batch28: Observability deep (logs/traces/metrics), correlation IDs, redaction rules, incident playbook, SLOs/alerts (neutral).
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
# ATOMS (internal): observability primitives
# -------------------------

Write-Json (Join-Path $bt "atoms\task.obs.define_log_schema.v1.json") @'
{
  "id": "task.obs.define_log_schema.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Observability: Define structured log schema",
  "description": "Defines a structured log schema: levels, event ids, required fields, correlation ids, and redaction guidance.",
  "tags": ["observability", "logging", "schema", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "obs_define_log_schema",
    "inputs": ["projectRoot", "appType?", "riskProfile?"],
    "outputs": ["logSchema"],
    "defaults": { "riskProfile": "standard" },
    "rules": [
      "logs must be parseable (structured) and stable",
      "include correlationId and requestId when relevant",
      "never log secrets; define redaction rules",
      "define event naming and eventId strategy"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.obs.add_correlation_id_contract.v1.json") @'
{
  "id": "task.obs.add_correlation_id_contract.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Observability: Correlation ID contract",
  "description": "Defines how correlation IDs are generated, propagated across boundaries, and included in logs/traces/metrics.",
  "tags": ["observability", "correlation", "trace", "logging"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "obs_define_correlation_id_contract",
    "inputs": ["projectRoot", "boundaries", "protocols?"],
    "outputs": ["correlationContract"],
    "rules": [
      "must propagate inbound to outbound calls",
      "must be included in error responses where safe",
      "must be stable per request unit of work"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.obs.define_trace_spans.v1.json") @'
{
  "id": "task.obs.define_trace_spans.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Observability: Define trace spans and attributes",
  "description": "Defines trace spans, boundaries, and attributes to capture latency and failures without over-collecting.",
  "tags": ["observability", "tracing", "spans"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "obs_define_trace_spans",
    "inputs": ["projectRoot", "criticalFlows", "riskProfile?"],
    "outputs": ["tracePlan"],
    "defaults": { "riskProfile": "standard" },
    "rules": [
      "trace only key boundaries and critical flows",
      "attributes must avoid PII/secrets",
      "include correlationId link to logs"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.obs.define_metrics_contract.v1.json") @'
{
  "id": "task.obs.define_metrics_contract.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Observability: Define metrics contract",
  "description": "Defines metrics: naming, labels/tags, cardinality limits, and standard counters/histograms for services.",
  "tags": ["observability", "metrics", "contract"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "obs_define_metrics_contract",
    "inputs": ["projectRoot", "appType?", "riskProfile?"],
    "outputs": ["metricsContract"],
    "defaults": { "riskProfile": "standard" },
    "rules": [
      "avoid high-cardinality labels",
      "include request duration and error rate metrics for services",
      "keep names stable and documented",
      "metrics must support SLO calculations"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.obs.define_redaction_policy.v1.json") @'
{
  "id": "task.obs.define_redaction_policy.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 8 },
  "title": "Observability: Define redaction + PII policy",
  "description": "Defines what is sensitive (secrets/PII), what must be redacted, and safe logging/tracing guidelines.",
  "tags": ["observability", "security", "privacy", "redaction"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "obs_define_redaction_policy",
    "inputs": ["projectRoot", "dataCategories?", "regulatoryContext?"],
    "outputs": ["redactionPolicy"],
    "rules": [
      "never log secrets, tokens, keys, passwords, auth headers",
      "define safe allowlist fields",
      "mask/omit PII by default unless explicitly needed and justified",
      "document retention expectations conceptually"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.obs.scaffold_logging_integration.v1.json") @'
{
  "id": "task.obs.scaffold_logging_integration.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Observability: Scaffold logging integration",
  "description": "Adds structured logging hooks following the schema and correlation contract, without selecting a specific logging library by name.",
  "tags": ["observability", "logging", "scaffold"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "obs_scaffold_logging_integration",
    "inputs": ["projectRoot", "logSchema", "correlationContract", "redactionPolicy"],
    "outputs": ["changedFiles", "loggingEntryPoints"],
    "rules": [
      "do not change public behavior except adding logs",
      "ensure logs are structured and consistent",
      "apply redaction everywhere logs are emitted"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.obs.scaffold_tracing_integration.v1.json") @'
{
  "id": "task.obs.scaffold_tracing_integration.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Observability: Scaffold tracing integration",
  "description": "Adds tracing hooks for defined spans and attributes, without selecting an instrumentation vendor by name.",
  "tags": ["observability", "tracing", "scaffold"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "obs_scaffold_tracing_integration",
    "inputs": ["projectRoot", "tracePlan", "correlationContract", "redactionPolicy"],
    "outputs": ["changedFiles", "instrumentedFlows"],
    "rules": [
      "instrument boundaries, not every function",
      "avoid PII in attributes",
      "ensure correlation linkages exist"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.obs.scaffold_metrics_integration.v1.json") @'
{
  "id": "task.obs.scaffold_metrics_integration.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Observability: Scaffold metrics integration",
  "description": "Adds metrics collection hooks based on the metrics contract without selecting a specific metrics backend by name.",
  "tags": ["observability", "metrics", "scaffold"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "obs_scaffold_metrics_integration",
    "inputs": ["projectRoot", "metricsContract"],
    "outputs": ["changedFiles", "metricsPoints"],
    "rules": [
      "avoid high cardinality labels",
      "ensure metrics can be consumed in CI smoke checks",
      "document metric names and meanings"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.obs.define_slos_and_alerts.v1.json") @'
{
  "id": "task.obs.define_slos_and_alerts.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Observability: Define SLOs and alert policy",
  "description": "Defines SLOs (availability, latency, error rate) and alert policy (paging vs ticket) based on business impact.",
  "tags": ["observability", "slo", "alerts", "reliability"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "obs_define_slos_and_alerts",
    "inputs": ["projectRoot", "criticalFlows", "metricsContract", "riskProfile?"],
    "outputs": ["sloPlan", "alertPolicy"],
    "defaults": { "riskProfile": "standard" },
    "rules": [
      "alerts must be actionable and tied to SLO burn or clear symptoms",
      "avoid noisy alerts; define thresholds with time windows",
      "define owner and response expectations"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.obs.scaffold_incident_playbook.v1.json") @'
{
  "id": "task.obs.scaffold_incident_playbook.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Observability: Scaffold incident playbook",
  "description": "Creates an incident playbook: triage checklist, dashboards/queries placeholders, escalation rules, and postmortem template.",
  "tags": ["observability", "incident", "runbook", "postmortem"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "obs_scaffold_incident_playbook",
    "inputs": ["projectRoot", "appType?", "ownership?"],
    "outputs": ["docsAdded"],
    "rules": [
      "include steps: detect, assess impact, mitigate, recover, verify, communicate",
      "include where logs/traces/metrics should be checked (as placeholders, not vendor-specific)",
      "include postmortem learning checklist"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.obs.validate_observability_coverage.v1.json") @'
{
  "id": "task.obs.validate_observability_coverage.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Observability: Validate coverage",
  "description": "Validates that critical flows have logs/traces/metrics coverage and redaction policy is applied.",
  "tags": ["observability", "validate", "coverage"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "obs_validate_coverage",
    "inputs": ["projectRoot", "criticalFlows", "logSchema", "tracePlan", "metricsContract", "redactionPolicy"],
    "outputs": ["coverageReport", "gaps"],
    "rules": [
      "report must list missing instrumentation by file/area",
      "do not recommend provider/model/toolchain changes",
      "prefer minimal changes to close gaps"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.obs.wait_user.select_risk_profile.v1.json") @'
{
  "id": "task.obs.wait_user.select_risk_profile.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "Observability: Ask user for risk profile (WAIT_USER)",
  "description": "When requirements are unclear, asks for minimal risk profile and critical flows list to avoid over-instrumenting.",
  "tags": ["observability", "wait_user"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "emit_wait_user_questions",
    "inputs": ["projectRoot", "appType?"],
    "outputs": ["questions", "waitUser"],
    "waitUser": true,
    "questionRules": [
      "ask for risk profile: low/standard/high",
      "ask for 1-3 critical user flows",
      "ask about sensitive data categories (PII/secrets)",
      "do not ask for provider/toolchain/model ids"
    ]
  }
}
'@

# -------------------------
# RECIPES (public): observability bundles
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.obs.baseline.neutral.v1.json") @'
{
  "id": "recipe.obs.baseline.neutral.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 90 },
  "title": "Observability baseline (neutral)",
  "description": "Defines log schema, correlation contract, trace/metrics plans, redaction policy, and scaffolds integrations.",
  "tags": ["observability", "baseline", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.obs.wait_user.select_risk_profile.v1" },
    { "use": "task.obs.define_redaction_policy.v1" },
    { "use": "task.obs.define_log_schema.v1" },
    { "use": "task.obs.add_correlation_id_contract.v1" },
    { "use": "task.obs.define_trace_spans.v1" },
    { "use": "task.obs.define_metrics_contract.v1" },
    { "use": "task.obs.scaffold_logging_integration.v1" },
    { "use": "task.obs.scaffold_tracing_integration.v1" },
    { "use": "task.obs.scaffold_metrics_integration.v1" },
    { "use": "task.obs.validate_observability_coverage.v1" }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.obs.slos_and_incidents.neutral.v1.json") @'
{
  "id": "recipe.obs.slos_and_incidents.neutral.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 86 },
  "title": "SLOs + incident playbook (neutral)",
  "description": "Defines SLOs and alert policy, and scaffolds an incident playbook/runbook and postmortem template.",
  "tags": ["observability", "slo", "incident", "runbook", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.obs.define_slos_and_alerts.v1" },
    { "use": "task.obs.scaffold_incident_playbook.v1" }
  ] }
}
'@

# -------------------------
# PACKS (public): entrypoints
# -------------------------

Write-Json (Join-Path $bt "packs\pack.obs.baseline.neutral.v1.json") @'
{
  "id": "pack.obs.baseline.neutral.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 185 },
  "title": "Observability baseline (neutral)",
  "description": "Adds structured logs, correlation IDs, tracing and metrics plans, redaction policy, and scaffolding across critical flows.",
  "tags": ["observability", "logging", "tracing", "metrics", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.obs.baseline.neutral.v1"] }
}
'@

Write-Json (Join-Path $bt "packs\pack.obs.slos_and_incidents.neutral.v1.json") @'
{
  "id": "pack.obs.slos_and_incidents.neutral.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 170 },
  "title": "SLOs + incident playbook (neutral)",
  "description": "Adds SLOs/alerts policy and an incident playbook/runbook plus postmortem template.",
  "tags": ["observability", "slo", "alerts", "incident", "runbook", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "uses": ["recipe.obs.slos_and_incidents.neutral.v1"] }
}
'@

# -------------------------
# GRAPHS (public): router for observability intents
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.observability.deep.neutral.v1.json") @'
{
  "id": "graph.router.observability.deep.neutral.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: Observability deep (neutral)",
  "description": "Routes observability intents: baseline instrumentation and SLO/incident readiness.",
  "tags": ["router", "observability", "neutral"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Input[Observability request] --> Q{Goal?}\n  Q -->|baseline logs+traces+metrics| B[pack.obs.baseline.neutral.v1]\n  Q -->|SLOs + incident playbook| S[pack.obs.slos_and_incidents.neutral.v1]\n  B --> Done[Done]\n  S --> Done"
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 28 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

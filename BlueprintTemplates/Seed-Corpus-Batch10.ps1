# BlueprintTemplates/Seed-Corpus-Batch10.ps1
# Batch10: observability + health checks + resilience + productionize pack + router graph
# Windows PowerShell 5.1 compatible. No provider/toolchain hardcoding. No shell commands.

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
# ATOMS (internal): features
# -------------------------

Write-Json (Join-Path $bt "atoms\task.feature.add_observability.v1.json") @'
{
  "id": "task.feature.add_observability.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Add Feature: Observability",
  "description": "Adds structured logging + correlation id scaffolding and observability docs. No toolchain assumptions.",
  "tags": ["feature", "observability", "logging", "tracing"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "add_feature_observability",
    "inputs": ["projectRoot", "mode"],
    "modes": ["basic"],
    "routes": {
      "basic": {
        "dotnet": ["recipe.dotnet.observability.basic.v1"],
        "node": ["recipe.node.observability.basic.v1"],
        "python": ["recipe.python.observability.basic.v1"],
        "go": ["recipe.go.observability.basic.v1"],
        "rust": ["recipe.rust.observability.basic.v1"],
        "java": ["recipe.java.observability.basic.v1"],
        "cpp": ["recipe.cpp.observability.basic.v1"]
      }
    }
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.feature.add_health_checks.v1.json") @'
{
  "id": "task.feature.add_health_checks.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Add Feature: Health Checks",
  "description": "Adds health endpoint and readiness/liveness notes.",
  "tags": ["feature", "health", "readiness", "liveness"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "add_feature_health_checks",
    "inputs": ["projectRoot"],
    "routes": {
      "dotnet": ["recipe.dotnet.health.basic.v1"],
      "node": ["recipe.node.health.basic.v1"],
      "python": ["recipe.python.health.basic.v1"],
      "go": ["recipe.go.health.basic.v1"],
      "rust": ["recipe.rust.health.basic.v1"],
      "java": ["recipe.java.health.basic.v1"],
      "cpp": ["recipe.cpp.health.basic.v1"]
    }
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.feature.add_resilience.v1.json") @'
{
  "id": "task.feature.add_resilience.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Add Feature: Resilience",
  "description": "Adds retry/backoff/circuit-breaker guidance + lightweight wrappers where sensible. No dependency assumptions.",
  "tags": ["feature", "resilience", "retry", "backoff", "circuitbreaker"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "add_feature_resilience",
    "inputs": ["projectRoot", "strategy"],
    "strategies": ["retry_backoff", "retry_backoff_circuit"],
    "routes": {
      "retry_backoff": {
        "dotnet": ["recipe.dotnet.resilience.retry_backoff.v1"],
        "node": ["recipe.node.resilience.retry_backoff.v1"],
        "python": ["recipe.python.resilience.retry_backoff.v1"],
        "go": ["recipe.go.resilience.retry_backoff.v1"],
        "rust": ["recipe.rust.resilience.retry_backoff.v1"],
        "java": ["recipe.java.resilience.retry_backoff.v1"],
        "cpp": ["recipe.cpp.resilience.retry_backoff.v1"]
      },
      "retry_backoff_circuit": {
        "dotnet": ["recipe.dotnet.resilience.retry_backoff_circuit.v1"],
        "node": ["recipe.node.resilience.retry_backoff_circuit.v1"],
        "python": ["recipe.python.resilience.retry_backoff_circuit.v1"],
        "go": ["recipe.go.resilience.retry_backoff_circuit.v1"],
        "rust": ["recipe.rust.resilience.retry_backoff_circuit.v1"],
        "java": ["recipe.java.resilience.retry_backoff_circuit.v1"],
        "cpp": ["recipe.cpp.resilience.retry_backoff_circuit.v1"]
      }
    }
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.productionize.service_bundle.v1.json") @'
{
  "id": "task.productionize.service_bundle.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 7 },
  "title": "Productionize: Service Bundle",
  "description": "Adds observability + health checks + resilience + release hygiene docs. Leaves toolchain and infra decisions to Settings.",
  "tags": ["productionize", "service", "observability", "health", "resilience"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "productionize_service_bundle",
    "inputs": ["projectRoot", "strategy"],
    "defaults": { "strategy": "retry_backoff" },
    "uses": [
      "task.feature.add_observability.v1",
      "task.feature.add_health_checks.v1",
      "task.feature.add_resilience.v1",
      "task.release.update_changelog.v1",
      "task.release.ensure_license.v1"
    ]
  }
}
'@

# -------------------------
# PACKS (public)
# -------------------------

Write-Json (Join-Path $bt "packs\pack.feature.observability.v1.json") @'
{
  "id": "pack.feature.observability.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 165 },
  "title": "Feature: Observability",
  "description": "Structured logging + correlation id scaffolding. No provider/toolchain assumptions.",
  "tags": ["feature", "observability", "logging"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.detect.project_shape.v1",
      "task.feature.add_observability.v1"
    ],
    "defaults": { "mode": "basic" }
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.feature.health_checks.v1.json") @'
{
  "id": "pack.feature.health_checks.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 160 },
  "title": "Feature: Health Checks",
  "description": "Adds /health (and readiness notes) scaffolding where applicable.",
  "tags": ["feature", "health", "readiness", "liveness"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.detect.project_shape.v1",
      "task.feature.add_health_checks.v1"
    ]
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.feature.resilience.v1.json") @'
{
  "id": "pack.feature.resilience.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 158 },
  "title": "Feature: Resilience",
  "description": "Retry/backoff/circuit-breaker guidance and lightweight wrappers (no dependency assumptions).",
  "tags": ["feature", "resilience", "retry", "backoff"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.detect.project_shape.v1",
      "task.feature.add_resilience.v1"
    ],
    "defaults": { "strategy": "retry_backoff" }
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.productionize.service.v1.json") @'
{
  "id": "pack.productionize.service.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 190 },
  "title": "Productionize: Service",
  "description": "Adds core production scaffolding: observability + health checks + resilience + release hygiene docs. Toolchain left to Settings.",
  "tags": ["productionize", "service", "release"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.detect.project_shape.v1",
      "task.productionize.service_bundle.v1"
    ],
    "defaults": { "strategy": "retry_backoff" }
  }
}
'@

# -------------------------
# GRAPHS (public): router
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.productionize.v1.json") @'
{
  "id": "graph.router.productionize.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: Productionize",
  "description": "Routes productionization requests to observability/health/resilience or full service productionize pack.",
  "tags": ["router", "productionize"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Input[productionize] --> Pick{scope?}\n  Pick -->|full service| Full[pack.productionize.service.v1]\n  Pick -->|observability| Obs[pack.feature.observability.v1]\n  Pick -->|health checks| Health[pack.feature.health_checks.v1]\n  Pick -->|resilience| Res[pack.feature.resilience.v1]\n  Pick -->|unsure| Full"
  }
}
'@

# -------------------------
# RECIPES (public): minimal scaffolds per ecosystem
# Everything is file-only; no commands.
# -------------------------

# DOTNET
Write-Json (Join-Path $bt "recipes\recipe.dotnet.observability.basic.v1.json") @'
{
  "id": "recipe.dotnet.observability.basic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 55 },
  "title": ".NET Observability: Basic",
  "description": "Adds minimal logger abstraction + correlation context.",
  "tags": ["dotnet", "observability", "logging"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/Obs/ILog.cs", "onConflict": "skip", "text": "namespace Obs;\n\npublic interface ILog\n{\n  void Info(string message);\n  void Warn(string message);\n  void Error(string message);\n}\n" } },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/Obs/Correlation.cs", "onConflict": "skip", "text": "using System;\nusing System.Threading;\n\nnamespace Obs;\n\npublic static class Correlation\n{\n  private static readonly AsyncLocal<string?> _id = new();\n  public static string Id => _id.Value ?? \"\";\n  public static IDisposable Push(string id)\n  {\n    var prev = _id.Value;\n    _id.Value = id;\n    return new Restore(prev);\n  }\n  private sealed class Restore : IDisposable\n  {\n    private readonly string? _prev;\n    public Restore(string? prev) { _prev = prev; }\n    public void Dispose() { _id.Value = _prev; }\n  }\n}\n" } },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/observability.md", "onConflict": "skip", "text": "# Observability\n\nGoals:\n- structured logs (level, timestamp, correlationId)\n- request/operation correlation\n\nWire concrete logging sinks via your environment/settings.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.dotnet.health.basic.v1.json") @'
{
  "id": "recipe.dotnet.health.basic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 55 },
  "title": ".NET Health: Basic",
  "description": "Adds health notes and a simple health response helper.",
  "tags": ["dotnet", "health"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/Health/HealthReport.cs", "onConflict": "skip", "text": "namespace Health;\n\npublic sealed record HealthReport(string Status, string? Detail = null);\n" } },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/health.md", "onConflict": "skip", "text": "# Health\n\nDefine endpoints:\n- /health (liveness)\n- /ready (readiness)\n\nReadiness should validate DB/cache connectivity if present.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.dotnet.resilience.retry_backoff.v1.json") @'
{
  "id": "recipe.dotnet.resilience.retry_backoff.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 50 },
  "title": ".NET Resilience: Retry + Backoff",
  "description": "Adds a lightweight retry helper (no external dependencies).",
  "tags": ["dotnet", "resilience", "retry", "backoff"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/Resilience/Retry.cs", "onConflict": "skip", "text": "using System;\nusing System.Threading;\nusing System.Threading.Tasks;\n\nnamespace Resilience;\n\npublic static class Retry\n{\n  public static async Task<T> WithBackoff<T>(Func<Task<T>> op, int attempts = 3, int baseDelayMs = 150, CancellationToken ct = default)\n  {\n    Exception? last = null;\n    for (int i = 0; i < attempts; i++)\n    {\n      ct.ThrowIfCancellationRequested();\n      try { return await op().ConfigureAwait(false); }\n      catch (Exception ex)\n      {\n        last = ex;\n        var delay = baseDelayMs * (int)Math.Pow(2, i);\n        await Task.Delay(delay, ct).ConfigureAwait(false);\n      }\n    }\n    throw last ?? new InvalidOperationException(\"Retry failed\");\n  }\n}\n" } },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/resilience.md", "onConflict": "skip", "text": "# Resilience\n\nDefault strategy:\n- retries with exponential backoff\n- timeouts\n- idempotency considerations\n\nChoose advanced policies via your environment/settings.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.dotnet.resilience.retry_backoff_circuit.v1.json") @'
{
  "id": "recipe.dotnet.resilience.retry_backoff_circuit.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 45 },
  "title": ".NET Resilience: Retry + Backoff + Circuit Notes",
  "description": "Adds circuit-breaker notes (implementation left to chosen libs/settings).",
  "tags": ["dotnet", "resilience", "circuitbreaker"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/circuit_breaker.md", "onConflict": "skip", "text": "# Circuit Breaker\n\nWhen repeated failures occur:\n- open circuit for a cooldown\n- allow limited probes\n- close on recovery\n\nImplement via library selection in environment/settings.\n" } }
  ] }
}
'@

# NODE
Write-Json (Join-Path $bt "recipes\recipe.node.observability.basic.v1.json") @'
{
  "id": "recipe.node.observability.basic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 45 },
  "title": "Node Observability: Basic",
  "description": "Adds logger stub + correlation id helper.",
  "tags": ["node", "observability", "logging"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/obs/logger.js", "onConflict": "skip", "text": "function log(level, message, meta){\n  const entry = { ts: new Date().toISOString(), level, message, ...(meta||{}) };\n  console.log(JSON.stringify(entry));\n}\nmodule.exports = {\n  info:(m,meta)=>log('info',m,meta),\n  warn:(m,meta)=>log('warn',m,meta),\n  error:(m,meta)=>log('error',m,meta)\n};\n" } },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/obs/correlation.js", "onConflict": "skip", "text": "let currentId = '';\nfunction get(){ return currentId; }\nfunction withId(id, fn){ const prev=currentId; currentId=id; try{ return fn(); } finally { currentId=prev; } }\nmodule.exports = { get, withId };\n" } },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/observability.md", "onConflict": "skip", "text": "# Observability\n\n- structured logs (JSON)\n- correlation id per request\n\nIntegrate log sinks/tracing via environment/settings.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.node.health.basic.v1.json") @'
{
  "id": "recipe.node.health.basic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 40 },
  "title": "Node Health: Basic",
  "description": "Adds health handler stub.",
  "tags": ["node", "health"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/health/health.js", "onConflict": "skip", "text": "function health(){ return { status:'ok' }; }\nfunction readiness(){ return { status:'ok' }; }\nmodule.exports = { health, readiness };\n" } },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/health.md", "onConflict": "skip", "text": "# Health\n\nExpose:\n- /health\n- /ready\n\nReadiness should validate DB/cache if present.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.node.resilience.retry_backoff.v1.json") @'
{
  "id": "recipe.node.resilience.retry_backoff.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 35 },
  "title": "Node Resilience: Retry + Backoff",
  "description": "Adds retry helper (no dependencies).",
  "tags": ["node", "resilience", "retry"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/resilience/retry.js", "onConflict": "skip", "text": "async function withBackoff(op, attempts=3, baseDelayMs=150){\n  let last;\n  for(let i=0;i<attempts;i++){\n    try { return await op(); }\n    catch(e){ last=e; const d=baseDelayMs*Math.pow(2,i); await new Promise(r=>setTimeout(r,d)); }\n  }\n  throw last || new Error('Retry failed');\n}\nmodule.exports = { withBackoff };\n" } },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/resilience.md", "onConflict": "skip", "text": "# Resilience\n\nDefault:\n- retry + exponential backoff\n- timeouts\n- idempotency\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.node.resilience.retry_backoff_circuit.v1.json") @'
{
  "id": "recipe.node.resilience.retry_backoff_circuit.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 25 },
  "title": "Node Resilience: Circuit Notes",
  "description": "Adds circuit breaker notes stub.",
  "tags": ["node", "resilience", "circuitbreaker"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/circuit_breaker.md", "onConflict": "skip", "text": "# Circuit Breaker\n\nOpen on repeated failures, cool down, probe, then close.\n\nImplementation via environment/settings.\n" } }
  ] }
}
'@

# PYTHON
Write-Json (Join-Path $bt "recipes\recipe.python.observability.basic.v1.json") @'
{
  "id": "recipe.python.observability.basic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 40 },
  "title": "Python Observability: Basic",
  "description": "Adds JSON logger helper + correlation id context.",
  "tags": ["python", "observability", "logging"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/obs.py", "onConflict": "skip", "text": "import json, time\n\n_correlation = ''\n\ndef set_correlation_id(cid: str):\n    global _correlation\n    _correlation = cid\n\ndef log(level: str, message: str, **meta):\n    entry = { 'ts': time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()), 'level': level, 'message': message, 'correlationId': _correlation }\n    entry.update(meta)\n    print(json.dumps(entry, separators=(',',':')))\n" } },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/observability.md", "onConflict": "skip", "text": "# Observability\n\n- JSON logs\n- correlation id per request\n\nWire log sinks/tracing via environment/settings.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.python.health.basic.v1.json") @'
{
  "id": "recipe.python.health.basic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 35 },
  "title": "Python Health: Basic",
  "description": "Adds health docs stub.",
  "tags": ["python", "health"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/health.md", "onConflict": "skip", "text": "# Health\n\nExpose:\n- /health\n- /ready\n\nReadiness should validate DB/cache if present.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.python.resilience.retry_backoff.v1.json") @'
{
  "id": "recipe.python.resilience.retry_backoff.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 30 },
  "title": "Python Resilience: Retry + Backoff",
  "description": "Adds retry helper (no deps).",
  "tags": ["python", "resilience", "retry"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/retry.py", "onConflict": "skip", "text": "import time\n\ndef with_backoff(op, attempts=3, base_delay_ms=150):\n    last = None\n    for i in range(attempts):\n        try:\n            return op()\n        except Exception as e:\n            last = e\n            delay = base_delay_ms * (2 ** i) / 1000.0\n            time.sleep(delay)\n    raise last if last else RuntimeError('Retry failed')\n" } },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/resilience.md", "onConflict": "skip", "text": "# Resilience\n\nDefault:\n- retry + exponential backoff\n- timeouts\n- idempotency\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.python.resilience.retry_backoff_circuit.v1.json") @'
{
  "id": "recipe.python.resilience.retry_backoff_circuit.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 20 },
  "title": "Python Resilience: Circuit Notes",
  "description": "Adds circuit breaker notes stub.",
  "tags": ["python", "resilience", "circuitbreaker"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/circuit_breaker.md", "onConflict": "skip", "text": "# Circuit Breaker\n\nOpen on repeated failures, cool down, probe, then close.\n\nImplementation via environment/settings.\n" } }
  ] }
}
'@

# GO/RUST/JAVA/CPP: docs-focused but valid IDs (keeps routing satisfied)
Write-Json (Join-Path $bt "recipes\recipe.go.observability.basic.v1.json") @'
{
  "id": "recipe.go.observability.basic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 20 },
  "title": "Go Observability: Basic Notes",
  "description": "Adds observability docs stub.",
  "tags": ["go", "observability"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/observability.md", "onConflict": "skip", "text": "# Observability\n\n- structured logs\n- correlation id\n\nIntegrate via environment/settings.\n" } } ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.go.health.basic.v1.json") @'
{
  "id": "recipe.go.health.basic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 15 },
  "title": "Go Health: Notes",
  "description": "Adds health docs stub.",
  "tags": ["go", "health"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/health.md", "onConflict": "skip", "text": "# Health\n\nExpose /health and /ready.\n" } } ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.go.resilience.retry_backoff.v1.json") @'
{
  "id": "recipe.go.resilience.retry_backoff.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 15 },
  "title": "Go Resilience: Notes",
  "description": "Adds resilience docs stub.",
  "tags": ["go", "resilience"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/resilience.md", "onConflict": "skip", "text": "# Resilience\n\nRetry/backoff/timeouts. Implement via chosen libs.\n" } } ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.go.resilience.retry_backoff_circuit.v1.json") @'
{
  "id": "recipe.go.resilience.retry_backoff_circuit.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 10 },
  "title": "Go Circuit: Notes",
  "description": "Adds circuit breaker docs stub.",
  "tags": ["go", "circuitbreaker"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/circuit_breaker.md", "onConflict": "skip", "text": "# Circuit Breaker\n\nOpen/cooldown/probe/close.\n" } } ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.rust.observability.basic.v1.json") @'
{
  "id": "recipe.rust.observability.basic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 10 },
  "title": "Rust Observability: Notes",
  "description": "Adds observability docs stub.",
  "tags": ["rust", "observability"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/observability.md", "onConflict": "skip", "text": "# Observability\n\nUse structured logs + correlation id. Integrate via crate selection.\n" } } ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.rust.health.basic.v1.json") @'
{
  "id": "recipe.rust.health.basic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 10 },
  "title": "Rust Health: Notes",
  "description": "Adds health docs stub.",
  "tags": ["rust", "health"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/health.md", "onConflict": "skip", "text": "# Health\n\nExpose /health and /ready.\n" } } ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.rust.resilience.retry_backoff.v1.json") @'
{
  "id": "recipe.rust.resilience.retry_backoff.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 10 },
  "title": "Rust Resilience: Notes",
  "description": "Adds resilience docs stub.",
  "tags": ["rust", "resilience"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/resilience.md", "onConflict": "skip", "text": "# Resilience\n\nRetry/backoff/timeouts.\n" } } ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.rust.resilience.retry_backoff_circuit.v1.json") @'
{
  "id": "recipe.rust.resilience.retry_backoff_circuit.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 10 },
  "title": "Rust Circuit: Notes",
  "description": "Adds circuit breaker docs stub.",
  "tags": ["rust", "circuitbreaker"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/circuit_breaker.md", "onConflict": "skip", "text": "# Circuit Breaker\n\nOpen/cooldown/probe/close.\n" } } ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.java.observability.basic.v1.json") @'
{
  "id": "recipe.java.observability.basic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 10 },
  "title": "Java Observability: Notes",
  "description": "Adds observability docs stub.",
  "tags": ["java", "observability"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/observability.md", "onConflict": "skip", "text": "# Observability\n\nStructured logs + correlation id.\n" } } ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.java.health.basic.v1.json") @'
{
  "id": "recipe.java.health.basic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 10 },
  "title": "Java Health: Notes",
  "description": "Adds health docs stub.",
  "tags": ["java", "health"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/health.md", "onConflict": "skip", "text": "# Health\n\nExpose /health and /ready.\n" } } ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.java.resilience.retry_backoff.v1.json") @'
{
  "id": "recipe.java.resilience.retry_backoff.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 10 },
  "title": "Java Resilience: Notes",
  "description": "Adds resilience docs stub.",
  "tags": ["java", "resilience"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/resilience.md", "onConflict": "skip", "text": "# Resilience\n\nRetry/backoff/timeouts.\n" } } ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.java.resilience.retry_backoff_circuit.v1.json") @'
{
  "id": "recipe.java.resilience.retry_backoff_circuit.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 10 },
  "title": "Java Circuit: Notes",
  "description": "Adds circuit breaker docs stub.",
  "tags": ["java", "circuitbreaker"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/circuit_breaker.md", "onConflict": "skip", "text": "# Circuit Breaker\n\nOpen/cooldown/probe/close.\n" } } ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.cpp.observability.basic.v1.json") @'
{
  "id": "recipe.cpp.observability.basic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 10 },
  "title": "C++ Observability: Notes",
  "description": "Adds observability docs stub.",
  "tags": ["cpp", "observability"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/observability.md", "onConflict": "skip", "text": "# Observability\n\nStructured logs + correlation id.\n" } } ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.cpp.health.basic.v1.json") @'
{
  "id": "recipe.cpp.health.basic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 10 },
  "title": "C++ Health: Notes",
  "description": "Adds health docs stub.",
  "tags": ["cpp", "health"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/health.md", "onConflict": "skip", "text": "# Health\n\nExpose /health and /ready.\n" } } ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.cpp.resilience.retry_backoff.v1.json") @'
{
  "id": "recipe.cpp.resilience.retry_backoff.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 10 },
  "title": "C++ Resilience: Notes",
  "description": "Adds resilience docs stub.",
  "tags": ["cpp", "resilience"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/resilience.md", "onConflict": "skip", "text": "# Resilience\n\nRetry/backoff/timeouts.\n" } } ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.cpp.resilience.retry_backoff_circuit.v1.json") @'
{
  "id": "recipe.cpp.resilience.retry_backoff_circuit.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 10 },
  "title": "C++ Circuit: Notes",
  "description": "Adds circuit breaker docs stub.",
  "tags": ["cpp", "circuitbreaker"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/circuit_breaker.md", "onConflict": "skip", "text": "# Circuit Breaker\n\nOpen/cooldown/probe/close.\n" } } ] }
}
'@

Write-Host ""
Write-Host "Done seeding batch 10 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

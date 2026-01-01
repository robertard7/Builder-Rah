# BlueprintTemplates/Seed-Corpus-Batch9.ps1
# Batch9: service feature packs (db/auth/cache/api-docs) + shared atoms + per-ecosystem recipes + feature router.
# Windows PowerShell 5.1 compatible. No provider/toolchain IDs. No hardcoded paths.

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
# ATOMS: feature hooks (internal)
# -------------------------

Write-Json (Join-Path $bt "atoms\task.feature.add_database.v1.json") @'
{
  "id": "task.feature.add_database.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Add Feature: Database",
  "description": "Add a database integration by selecting recipes based on ecosystem and db kind.",
  "tags": ["feature", "database", "db"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "add_feature_database",
    "inputs": ["projectRoot", "dbKind"],
    "dbKinds": ["sqlite", "postgres", "generic"],
    "routes": {
      "sqlite": {
        "dotnet": ["recipe.dotnet.db.sqlite.v1"],
        "node": ["recipe.node.db.sqlite.v1"],
        "python": ["recipe.python.db.sqlite.v1"],
        "go": ["recipe.go.db.sqlite.v1"],
        "rust": ["recipe.rust.db.sqlite.v1"],
        "java": ["recipe.java.db.sqlite.v1"],
        "cpp": ["recipe.cpp.db.sqlite.v1"]
      },
      "postgres": {
        "dotnet": ["recipe.dotnet.db.postgres.v1"],
        "node": ["recipe.node.db.postgres.v1"],
        "python": ["recipe.python.db.postgres.v1"],
        "go": ["recipe.go.db.postgres.v1"],
        "rust": ["recipe.rust.db.postgres.v1"],
        "java": ["recipe.java.db.postgres.v1"],
        "cpp": ["recipe.cpp.db.postgres.v1"]
      },
      "generic": {
        "dotnet": ["recipe.dotnet.db.generic.v1"],
        "node": ["recipe.node.db.generic.v1"],
        "python": ["recipe.python.db.generic.v1"],
        "go": ["recipe.go.db.generic.v1"],
        "rust": ["recipe.rust.db.generic.v1"],
        "java": ["recipe.java.db.generic.v1"],
        "cpp": ["recipe.cpp.db.generic.v1"]
      }
    }
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.feature.add_auth.v1.json") @'
{
  "id": "task.feature.add_auth.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Add Feature: Auth",
  "description": "Add an authentication skeleton (JWT/session abstraction) without binding to providers.",
  "tags": ["feature", "auth"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "add_feature_auth",
    "inputs": ["projectRoot", "authMode"],
    "authModes": ["jwt", "session", "generic"],
    "routes": {
      "jwt": {
        "dotnet": ["recipe.dotnet.auth.jwt_skeleton.v1"],
        "node": ["recipe.node.auth.jwt_skeleton.v1"],
        "python": ["recipe.python.auth.jwt_skeleton.v1"],
        "go": ["recipe.go.auth.jwt_skeleton.v1"],
        "rust": ["recipe.rust.auth.jwt_skeleton.v1"],
        "java": ["recipe.java.auth.jwt_skeleton.v1"],
        "cpp": ["recipe.cpp.auth.jwt_skeleton.v1"]
      },
      "session": {
        "dotnet": ["recipe.dotnet.auth.session_skeleton.v1"],
        "node": ["recipe.node.auth.session_skeleton.v1"],
        "python": ["recipe.python.auth.session_skeleton.v1"],
        "go": ["recipe.go.auth.session_skeleton.v1"],
        "rust": ["recipe.rust.auth.session_skeleton.v1"],
        "java": ["recipe.java.auth.session_skeleton.v1"],
        "cpp": ["recipe.cpp.auth.session_skeleton.v1"]
      },
      "generic": {
        "dotnet": ["recipe.dotnet.auth.generic.v1"],
        "node": ["recipe.node.auth.generic.v1"],
        "python": ["recipe.python.auth.generic.v1"],
        "go": ["recipe.go.auth.generic.v1"],
        "rust": ["recipe.rust.auth.generic.v1"],
        "java": ["recipe.java.auth.generic.v1"],
        "cpp": ["recipe.cpp.auth.generic.v1"]
      }
    }
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.feature.add_cache.v1.json") @'
{
  "id": "task.feature.add_cache.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Add Feature: Cache",
  "description": "Add caching (in-memory + external hook placeholder).",
  "tags": ["feature", "cache"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "add_feature_cache",
    "inputs": ["projectRoot", "cacheMode"],
    "cacheModes": ["memory", "external_hook"],
    "routes": {
      "memory": {
        "dotnet": ["recipe.dotnet.cache.memory.v1"],
        "node": ["recipe.node.cache.memory.v1"],
        "python": ["recipe.python.cache.memory.v1"],
        "go": ["recipe.go.cache.memory.v1"],
        "rust": ["recipe.rust.cache.memory.v1"],
        "java": ["recipe.java.cache.memory.v1"],
        "cpp": ["recipe.cpp.cache.memory.v1"]
      },
      "external_hook": {
        "dotnet": ["recipe.dotnet.cache.external_hook.v1"],
        "node": ["recipe.node.cache.external_hook.v1"],
        "python": ["recipe.python.cache.external_hook.v1"],
        "go": ["recipe.go.cache.external_hook.v1"],
        "rust": ["recipe.rust.cache.external_hook.v1"],
        "java": ["recipe.java.cache.external_hook.v1"],
        "cpp": ["recipe.cpp.cache.external_hook.v1"]
      }
    }
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.feature.add_api_docs.v1.json") @'
{
  "id": "task.feature.add_api_docs.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Add Feature: API Docs",
  "description": "Add API documentation scaffolding (OpenAPI stub + README routes).",
  "tags": ["feature", "docs", "openapi"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "add_feature_api_docs",
    "inputs": ["projectRoot"],
    "routes": {
      "dotnet": ["recipe.dotnet.docs.api.v1"],
      "node": ["recipe.node.docs.api.v1"],
      "python": ["recipe.python.docs.api.v1"],
      "go": ["recipe.go.docs.api.v1"],
      "rust": ["recipe.rust.docs.api.v1"],
      "java": ["recipe.java.docs.api.v1"],
      "cpp": ["recipe.cpp.docs.api.v1"]
    }
  }
}
'@

# -------------------------
# PACKS: feature packs (public)
# -------------------------

Write-Json (Join-Path $bt "packs\pack.feature.database.v1.json") @'
{
  "id": "pack.feature.database.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 160 },
  "title": "Feature: Database",
  "description": "Adds a database integration skeleton (sqlite/postgres/generic) without hardcoding providers.",
  "tags": ["feature", "database"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.detect.project_shape.v1",
      "task.feature.add_database.v1"
    ],
    "defaults": { "dbKind": "sqlite" }
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.feature.auth.v1.json") @'
{
  "id": "pack.feature.auth.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 160 },
  "title": "Feature: Auth",
  "description": "Adds an authentication skeleton (jwt/session) without binding to vendors.",
  "tags": ["feature", "auth"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.detect.project_shape.v1",
      "task.feature.add_auth.v1"
    ],
    "defaults": { "authMode": "jwt" }
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.feature.cache.v1.json") @'
{
  "id": "pack.feature.cache.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 155 },
  "title": "Feature: Cache",
  "description": "Adds caching scaffolding (memory or external hook).",
  "tags": ["feature", "cache"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.detect.project_shape.v1",
      "task.feature.add_cache.v1"
    ],
    "defaults": { "cacheMode": "memory" }
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.feature.api_docs.v1.json") @'
{
  "id": "pack.feature.api_docs.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 150 },
  "title": "Feature: API Docs",
  "description": "Adds API documentation scaffolding (OpenAPI stub + route docs).",
  "tags": ["feature", "docs"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.detect.project_shape.v1",
      "task.feature.add_api_docs.v1"
    ]
  }
}
'@

# -------------------------
# GRAPHS: router for common service features
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.feature_service_common.v1.json") @'
{
  "id": "graph.router.feature_service_common.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: Service Features (DB/Auth/Cache/Docs)",
  "description": "Routes common service feature requests to the correct pack.",
  "tags": ["router", "feature", "service"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Input[add feature] --> Pick{which?}\n  Pick -->|database| DB[pack.feature.database.v1]\n  Pick -->|auth| Auth[pack.feature.auth.v1]\n  Pick -->|cache| Cache[pack.feature.cache.v1]\n  Pick -->|api docs| Docs[pack.feature.api_docs.v1]\n  Pick -->|other| Fallback[graph.router.feature_by_shape.v1]"
  }
}
'@

# -------------------------
# RECIPES: minimal skeletons (file-only) for each ecosystem
# Keep them intentionally simple; real wiring is settings/toolchain-controlled.
# -------------------------

# DOTNET
Write-Json (Join-Path $bt "recipes\recipe.dotnet.db.sqlite.v1.json") @'
{
  "id": "recipe.dotnet.db.sqlite.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 55 },
  "title": ".NET DB: SQLite Skeleton",
  "description": "Adds a minimal data access interface and a placeholder SQLite implementation.",
  "tags": ["dotnet", "db", "sqlite"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/Data/IDb.cs", "onConflict": "skip", "text": "using System.Threading.Tasks;\n\nnamespace Data;\n\npublic interface IDb\n{\n  Task<string> PingAsync();\n}\n" } },
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/Data/SqliteDb.cs", "onConflict": "skip", "text": "using System.Threading.Tasks;\n\nnamespace Data;\n\n// Placeholder: wire a SQLite provider via your toolchain/settings.\npublic sealed class SqliteDb : IDb\n{\n  public Task<string> PingAsync() => Task.FromResult(\"sqlite:ok\");\n}\n" } }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.dotnet.db.postgres.v1.json") @'
{
  "id": "recipe.dotnet.db.postgres.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 55 },
  "title": ".NET DB: Postgres Skeleton",
  "description": "Adds a minimal data access interface and a placeholder Postgres implementation.",
  "tags": ["dotnet", "db", "postgres"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/Data/PostgresDb.cs", "onConflict": "skip", "text": "using System.Threading.Tasks;\n\nnamespace Data;\n\n// Placeholder: wire a Postgres provider via your toolchain/settings.\npublic sealed class PostgresDb : IDb\n{\n  public Task<string> PingAsync() => Task.FromResult(\"postgres:ok\");\n}\n" } }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.dotnet.db.generic.v1.json") @'
{
  "id": "recipe.dotnet.db.generic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 50 },
  "title": ".NET DB: Generic Skeleton",
  "description": "Adds an IDb abstraction and a no-op implementation.",
  "tags": ["dotnet", "db", "generic"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/Data/NoopDb.cs", "onConflict": "skip", "text": "using System.Threading.Tasks;\n\nnamespace Data;\n\npublic sealed class NoopDb : IDb\n{\n  public Task<string> PingAsync() => Task.FromResult(\"noop:ok\");\n}\n" } }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.dotnet.auth.jwt_skeleton.v1.json") @'
{
  "id": "recipe.dotnet.auth.jwt_skeleton.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 55 },
  "title": ".NET Auth: JWT Skeleton",
  "description": "Adds token interface and placeholder signer/validator.",
  "tags": ["dotnet", "auth", "jwt"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/Auth/ITokenService.cs", "onConflict": "skip", "text": "namespace Auth;\n\npublic interface ITokenService\n{\n  string Issue(string subject);\n  bool TryValidate(string token, out string subject);\n}\n" } },
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/Auth/TokenService.cs", "onConflict": "skip", "text": "namespace Auth;\n\n// Placeholder: implement signing/validation via your chosen library.\npublic sealed class TokenService : ITokenService\n{\n  public string Issue(string subject) => \"token:\" + subject;\n\n  public bool TryValidate(string token, out string subject)\n  {\n    subject = \"\";\n    if (!token.StartsWith(\"token:\")) return false;\n    subject = token.Substring(\"token:\".Length);\n    return true;\n  }\n}\n" } }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.dotnet.auth.session_skeleton.v1.json") @'
{
  "id": "recipe.dotnet.auth.session_skeleton.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 50 },
  "title": ".NET Auth: Session Skeleton",
  "description": "Adds a simple session store abstraction.",
  "tags": ["dotnet", "auth", "session"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/Auth/ISessionStore.cs", "onConflict": "skip", "text": "namespace Auth;\n\npublic interface ISessionStore\n{\n  string Create(string subject);\n  bool TryGet(string sessionId, out string subject);\n}\n" } }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.dotnet.auth.generic.v1.json") @'
{
  "id": "recipe.dotnet.auth.generic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 40 },
  "title": ".NET Auth: Generic Notes",
  "description": "Adds a README section for auth integration points.",
  "tags": ["dotnet", "auth", "generic"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/auth.md", "onConflict": "skip", "text": "# Auth\n\nIntegration points:\n- token issuance\n- request authentication middleware\n- user store\n\nChoose implementation via your environment/settings.\n" } }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.dotnet.cache.memory.v1.json") @'
{
  "id": "recipe.dotnet.cache.memory.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 50 },
  "title": ".NET Cache: In-Memory",
  "description": "Adds a minimal in-memory cache interface and implementation.",
  "tags": ["dotnet", "cache", "memory"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/Cache/ICache.cs", "onConflict": "skip", "text": "namespace Cache;\n\npublic interface ICache\n{\n  bool TryGet(string key, out string value);\n  void Set(string key, string value);\n}\n" } },
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/Cache/MemoryCache.cs", "onConflict": "skip", "text": "using System.Collections.Concurrent;\n\nnamespace Cache;\n\npublic sealed class MemoryCache : ICache\n{\n  private readonly ConcurrentDictionary<string,string> _map = new();\n  public bool TryGet(string key, out string value) => _map.TryGetValue(key, out value!);\n  public void Set(string key, string value) => _map[key] = value;\n}\n" } }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.dotnet.cache.external_hook.v1.json") @'
{
  "id": "recipe.dotnet.cache.external_hook.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 40 },
  "title": ".NET Cache: External Hook",
  "description": "Adds a placeholder external cache adapter.",
  "tags": ["dotnet", "cache", "external"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/Cache/ExternalCacheAdapter.cs", "onConflict": "skip", "text": "namespace Cache;\n\n// Placeholder: integrate external cache via your toolchain/settings.\npublic sealed class ExternalCacheAdapter : ICache\n{\n  public bool TryGet(string key, out string value) { value = \"\"; return false; }\n  public void Set(string key, string value) { }\n}\n" } }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.dotnet.docs.api.v1.json") @'
{
  "id": "recipe.dotnet.docs.api.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 45 },
  "title": ".NET Docs: API (OpenAPI Stub)",
  "description": "Adds an OpenAPI stub and a routes README.",
  "tags": ["dotnet", "docs", "openapi"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/openapi.yaml", "onConflict": "skip", "text": "openapi: 3.0.0\ninfo:\n  title: ${projectName} API\n  version: 0.1.0\npaths: {}\n" } },
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/routes.md", "onConflict": "skip", "text": "# Routes\n\nDocument routes here.\n" } }
    ]
  }
}
'@

# For non-dotnet ecosystems, keep the templates intentionally compact and consistent:
# Node/Python/Go/Rust/Java/C++: db/auth/cache/docs skeletons (mostly README + placeholder code)
# NODE
Write-Json (Join-Path $bt "recipes\recipe.node.db.sqlite.v1.json") @'
{
  "id": "recipe.node.db.sqlite.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 45 },
  "title": "Node DB: SQLite Skeleton",
  "description": "Adds a db module stub for SQLite integration.",
  "tags": ["node", "db", "sqlite"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/db/sqliteDb.js", "onConflict": "skip", "text": "// Placeholder: wire SQLite driver via toolchain/settings.\nfunction ping() { return 'sqlite:ok'; }\nmodule.exports = { ping };\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.node.db.postgres.v1.json") @'
{
  "id": "recipe.node.db.postgres.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 45 },
  "title": "Node DB: Postgres Skeleton",
  "description": "Adds a db module stub for Postgres integration.",
  "tags": ["node", "db", "postgres"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/db/postgresDb.js", "onConflict": "skip", "text": "// Placeholder: wire Postgres driver via toolchain/settings.\nfunction ping() { return 'postgres:ok'; }\nmodule.exports = { ping };\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.node.db.generic.v1.json") @'
{
  "id": "recipe.node.db.generic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 35 },
  "title": "Node DB: Generic Notes",
  "description": "Adds a docs note for DB integration points.",
  "tags": ["node", "db", "generic"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/db.md", "onConflict": "skip", "text": "# Database\n\nProvide:\n- connect()\n- query()\n- migrations\n\nImplementation via environment/settings.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.node.auth.jwt_skeleton.v1.json") @'
{
  "id": "recipe.node.auth.jwt_skeleton.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 45 },
  "title": "Node Auth: JWT Skeleton",
  "description": "Adds a tiny token service stub.",
  "tags": ["node", "auth", "jwt"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/auth/tokenService.js", "onConflict": "skip", "text": "// Placeholder: implement signing/verification with your chosen library.\nfunction issue(subject){ return `token:${subject}`; }\nfunction tryValidate(token){\n  if (!token.startsWith('token:')) return { ok:false };\n  return { ok:true, subject: token.slice('token:'.length) };\n}\nmodule.exports = { issue, tryValidate };\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.node.auth.session_skeleton.v1.json") @'
{
  "id": "recipe.node.auth.session_skeleton.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 35 },
  "title": "Node Auth: Session Skeleton",
  "description": "Adds a session store stub.",
  "tags": ["node", "auth", "session"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/auth/sessionStore.js", "onConflict": "skip", "text": "const map = new Map();\nfunction create(subject){ const id = `s_${Math.random().toString(16).slice(2)}`; map.set(id, subject); return id; }\nfunction tryGet(id){ return map.has(id) ? { ok:true, subject: map.get(id) } : { ok:false }; }\nmodule.exports = { create, tryGet };\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.node.auth.generic.v1.json") @'
{
  "id": "recipe.node.auth.generic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 25 },
  "title": "Node Auth: Generic Notes",
  "description": "Adds auth docs stub.",
  "tags": ["node", "auth", "generic"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/auth.md", "onConflict": "skip", "text": "# Auth\n\nDocument:\n- token/session strategy\n- middleware points\n- user store\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.node.cache.memory.v1.json") @'
{
  "id": "recipe.node.cache.memory.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 35 },
  "title": "Node Cache: In-Memory",
  "description": "Adds a simple in-memory cache helper.",
  "tags": ["node", "cache", "memory"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/cache/memoryCache.js", "onConflict": "skip", "text": "const map = new Map();\nfunction get(k){ return map.has(k) ? map.get(k) : null; }\nfunction set(k,v){ map.set(k,v); }\nmodule.exports = { get, set };\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.node.cache.external_hook.v1.json") @'
{
  "id": "recipe.node.cache.external_hook.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 25 },
  "title": "Node Cache: External Hook",
  "description": "Adds an adapter placeholder for an external cache.",
  "tags": ["node", "cache", "external"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/cache/externalCache.js", "onConflict": "skip", "text": "// Placeholder: integrate external cache via environment/settings.\nfunction get(k){ return null; }\nfunction set(k,v){ }\nmodule.exports = { get, set };\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.node.docs.api.v1.json") @'
{
  "id": "recipe.node.docs.api.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 35 },
  "title": "Node Docs: API (OpenAPI Stub)",
  "description": "Adds OpenAPI stub and routes doc.",
  "tags": ["node", "docs", "openapi"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/openapi.yaml", "onConflict": "skip", "text": "openapi: 3.0.0\ninfo:\n  title: ${projectName} API\n  version: 0.1.0\npaths: {}\n" } },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/routes.md", "onConflict": "skip", "text": "# Routes\n\nDocument routes here.\n" } }
  ] }
}
'@

# PYTHON/GO/RUST/JAVA/CPP: keep minimal doc+stub recipes (compact but complete IDs)
# PYTHON
Write-Json (Join-Path $bt "recipes\recipe.python.db.sqlite.v1.json") @'
{
  "id": "recipe.python.db.sqlite.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 40 },
  "title": "Python DB: SQLite Skeleton",
  "description": "Adds a sqlite db helper stub (stdlib sqlite3).",
  "tags": ["python", "db", "sqlite"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/db_sqlite.py", "onConflict": "skip", "text": "import sqlite3\n\ndef ping(path=':memory:'):\n    con = sqlite3.connect(path)\n    con.execute('select 1')\n    con.close()\n    return 'sqlite:ok'\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.python.db.postgres.v1.json") @'
{
  "id": "recipe.python.db.postgres.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 35 },
  "title": "Python DB: Postgres Skeleton",
  "description": "Adds a placeholder postgres helper (no driver assumed).",
  "tags": ["python", "db", "postgres"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/db_postgres.py", "onConflict": "skip", "text": "def ping():\n    # Placeholder: wire a postgres driver via environment/settings.\n    return 'postgres:ok'\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.python.db.generic.v1.json") @'
{
  "id": "recipe.python.db.generic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 25 },
  "title": "Python DB: Generic Notes",
  "description": "Adds DB integration notes.",
  "tags": ["python", "db", "generic"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/db.md", "onConflict": "skip", "text": "# Database\n\nDefine:\n- connect\n- query\n- migrations\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.python.auth.jwt_skeleton.v1.json") @'
{
  "id": "recipe.python.auth.jwt_skeleton.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 35 },
  "title": "Python Auth: JWT Skeleton",
  "description": "Adds token service placeholder.",
  "tags": ["python", "auth", "jwt"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/auth_tokens.py", "onConflict": "skip", "text": "def issue(subject: str) -> str:\n    return f\"token:{subject}\"\n\ndef try_validate(token: str):\n    if not token.startswith('token:'):\n        return (False, None)\n    return (True, token[len('token:'):])\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.python.auth.session_skeleton.v1.json") @'
{
  "id": "recipe.python.auth.session_skeleton.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 25 },
  "title": "Python Auth: Session Skeleton",
  "description": "Adds in-memory session store stub.",
  "tags": ["python", "auth", "session"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/auth_sessions.py", "onConflict": "skip", "text": "import secrets\n_store = {}\n\ndef create(subject: str) -> str:\n    sid = 's_' + secrets.token_hex(8)\n    _store[sid] = subject\n    return sid\n\ndef try_get(sid: str):\n    return _store.get(sid)\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.python.auth.generic.v1.json") @'
{
  "id": "recipe.python.auth.generic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 20 },
  "title": "Python Auth: Generic Notes",
  "description": "Adds auth docs stub.",
  "tags": ["python", "auth", "generic"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/auth.md", "onConflict": "skip", "text": "# Auth\n\nDocument auth strategy here.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.python.cache.memory.v1.json") @'
{
  "id": "recipe.python.cache.memory.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 25 },
  "title": "Python Cache: In-Memory",
  "description": "Adds a simple dict cache helper.",
  "tags": ["python", "cache", "memory"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/cache_memory.py", "onConflict": "skip", "text": "_cache = {}\n\ndef get(k):\n    return _cache.get(k)\n\ndef set(k,v):\n    _cache[k]=v\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.python.cache.external_hook.v1.json") @'
{
  "id": "recipe.python.cache.external_hook.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 15 },
  "title": "Python Cache: External Hook",
  "description": "Adds placeholder external cache adapter.",
  "tags": ["python", "cache", "external"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/cache_external.py", "onConflict": "skip", "text": "def get(k):\n    return None\n\ndef set(k,v):\n    pass\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.python.docs.api.v1.json") @'
{
  "id": "recipe.python.docs.api.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 25 },
  "title": "Python Docs: API (OpenAPI Stub)",
  "description": "Adds OpenAPI stub and routes doc.",
  "tags": ["python", "docs", "openapi"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/openapi.yaml", "onConflict": "skip", "text": "openapi: 3.0.0\ninfo:\n  title: ${projectName} API\n  version: 0.1.0\npaths: {}\n" } },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/routes.md", "onConflict": "skip", "text": "# Routes\n\nDocument routes here.\n" } }
  ] }
}
'@

# GO
Write-Json (Join-Path $bt "recipes\recipe.go.db.sqlite.v1.json") @'
{
  "id": "recipe.go.db.sqlite.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 25 },
  "title": "Go DB: SQLite Notes",
  "description": "Adds DB notes; actual driver selection via environment/settings.",
  "tags": ["go", "db", "sqlite"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/db.md", "onConflict": "skip", "text": "# Database (SQLite)\n\nImplement via a driver chosen in your environment/settings.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.go.db.postgres.v1.json") @'
{
  "id": "recipe.go.db.postgres.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 25 },
  "title": "Go DB: Postgres Notes",
  "description": "Adds DB notes; driver selection via environment/settings.",
  "tags": ["go", "db", "postgres"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/db_postgres.md", "onConflict": "skip", "text": "# Database (Postgres)\n\nImplement via a driver chosen in your environment/settings.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.go.db.generic.v1.json") @'
{
  "id": "recipe.go.db.generic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 15 },
  "title": "Go DB: Generic Notes",
  "description": "Adds DB integration notes.",
  "tags": ["go", "db", "generic"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/db_generic.md", "onConflict": "skip", "text": "# Database\n\nDefine connection + query patterns.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.go.auth.jwt_skeleton.v1.json") @'
{
  "id": "recipe.go.auth.jwt_skeleton.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 20 },
  "title": "Go Auth: JWT Notes",
  "description": "Adds auth notes stub; implementation via chosen libs.",
  "tags": ["go", "auth", "jwt"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/auth_jwt.md", "onConflict": "skip", "text": "# Auth (JWT)\n\nImplement token issue/verify using a library chosen in your environment/settings.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.go.auth.session_skeleton.v1.json") @'
{
  "id": "recipe.go.auth.session_skeleton.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 15 },
  "title": "Go Auth: Session Notes",
  "description": "Adds session auth notes stub.",
  "tags": ["go", "auth", "session"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/auth_session.md", "onConflict": "skip", "text": "# Auth (Session)\n\nDocument session strategy here.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.go.auth.generic.v1.json") @'
{
  "id": "recipe.go.auth.generic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 10 },
  "title": "Go Auth: Generic Notes",
  "description": "Adds auth notes stub.",
  "tags": ["go", "auth", "generic"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/auth.md", "onConflict": "skip", "text": "# Auth\n\nDocument auth strategy here.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.go.cache.memory.v1.json") @'
{
  "id": "recipe.go.cache.memory.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 15 },
  "title": "Go Cache: In-Memory Notes",
  "description": "Adds cache notes stub.",
  "tags": ["go", "cache", "memory"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/cache.md", "onConflict": "skip", "text": "# Cache\n\nIn-memory cache can be a map with TTL if needed.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.go.cache.external_hook.v1.json") @'
{
  "id": "recipe.go.cache.external_hook.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 10 },
  "title": "Go Cache: External Hook Notes",
  "description": "Adds external cache notes stub.",
  "tags": ["go", "cache", "external"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/cache_external.md", "onConflict": "skip", "text": "# External Cache\n\nIntegrate via environment/settings.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.go.docs.api.v1.json") @'
{
  "id": "recipe.go.docs.api.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 15 },
  "title": "Go Docs: API",
  "description": "Adds OpenAPI stub and routes doc.",
  "tags": ["go", "docs", "openapi"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/openapi.yaml", "onConflict": "skip", "text": "openapi: 3.0.0\ninfo:\n  title: ${projectName} API\n  version: 0.1.0\npaths: {}\n" } },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/routes.md", "onConflict": "skip", "text": "# Routes\n\nDocument routes here.\n" } }
  ] }
}
'@

# RUST/JAVA/CPP docs-only stubs to keep IDs satisfied without pretending to implement deps.
Write-Json (Join-Path $bt "recipes\recipe.rust.db.sqlite.v1.json") @'
{
  "id": "recipe.rust.db.sqlite.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 10 },
  "title": "Rust DB: SQLite Notes",
  "description": "Adds db notes stub.",
  "tags": ["rust", "db", "sqlite"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/db_sqlite.md", "onConflict": "skip", "text": "# DB (SQLite)\n\nIntegrate via crate selection in your environment/settings.\n" } }
  ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.rust.db.postgres.v1.json") @'
{
  "id": "recipe.rust.db.postgres.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 10 },
  "title": "Rust DB: Postgres Notes",
  "description": "Adds db notes stub.",
  "tags": ["rust", "db", "postgres"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/db_postgres.md", "onConflict": "skip", "text": "# DB (Postgres)\n\nIntegrate via crate selection in your environment/settings.\n" } }
  ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.rust.db.generic.v1.json") @'
{
  "id": "recipe.rust.db.generic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 5 },
  "title": "Rust DB: Generic Notes",
  "description": "Adds db notes stub.",
  "tags": ["rust", "db", "generic"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/db.md", "onConflict": "skip", "text": "# DB\n\nDocument db integration here.\n" } }
  ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.rust.auth.jwt_skeleton.v1.json") @'
{
  "id": "recipe.rust.auth.jwt_skeleton.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 5 },
  "title": "Rust Auth: JWT Notes",
  "description": "Adds auth notes stub.",
  "tags": ["rust", "auth", "jwt"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/auth_jwt.md", "onConflict": "skip", "text": "# Auth (JWT)\n\nImplement via crate selection.\n" } }
  ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.rust.auth.session_skeleton.v1.json") @'
{
  "id": "recipe.rust.auth.session_skeleton.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 5 },
  "title": "Rust Auth: Session Notes",
  "description": "Adds auth notes stub.",
  "tags": ["rust", "auth", "session"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/auth_session.md", "onConflict": "skip", "text": "# Auth (Session)\n\nDocument session strategy.\n" } }
  ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.rust.auth.generic.v1.json") @'
{
  "id": "recipe.rust.auth.generic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 5 },
  "title": "Rust Auth: Generic Notes",
  "description": "Adds auth notes stub.",
  "tags": ["rust", "auth", "generic"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/auth.md", "onConflict": "skip", "text": "# Auth\n\nDocument auth strategy.\n" } }
  ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.rust.cache.memory.v1.json") @'
{
  "id": "recipe.rust.cache.memory.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 5 },
  "title": "Rust Cache: Memory Notes",
  "description": "Adds cache notes stub.",
  "tags": ["rust", "cache", "memory"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/cache.md", "onConflict": "skip", "text": "# Cache\n\nIn-memory cache strategy.\n" } }
  ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.rust.cache.external_hook.v1.json") @'
{
  "id": "recipe.rust.cache.external_hook.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 5 },
  "title": "Rust Cache: External Hook Notes",
  "description": "Adds cache notes stub.",
  "tags": ["rust", "cache", "external"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/cache_external.md", "onConflict": "skip", "text": "# External Cache\n\nIntegrate via settings.\n" } }
  ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.rust.docs.api.v1.json") @'
{
  "id": "recipe.rust.docs.api.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 5 },
  "title": "Rust Docs: API",
  "description": "Adds OpenAPI stub and routes doc.",
  "tags": ["rust", "docs", "openapi"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/openapi.yaml", "onConflict": "skip", "text": "openapi: 3.0.0\ninfo:\n  title: ${projectName} API\n  version: 0.1.0\npaths: {}\n" } },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/routes.md", "onConflict": "skip", "text": "# Routes\n\nDocument routes.\n" } }
  ] }
}
'@

# JAVA/CPP: docs-only stubs
Write-Json (Join-Path $bt "recipes\recipe.java.db.sqlite.v1.json") @'
{
  "id": "recipe.java.db.sqlite.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 5 },
  "title": "Java DB: SQLite Notes",
  "description": "Adds db notes stub.",
  "tags": ["java", "db", "sqlite"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/db_sqlite.md", "onConflict": "skip", "text": "# DB (SQLite)\n\nIntegrate via library choice.\n" } } ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.java.db.postgres.v1.json") @'
{
  "id": "recipe.java.db.postgres.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 5 },
  "title": "Java DB: Postgres Notes",
  "description": "Adds db notes stub.",
  "tags": ["java", "db", "postgres"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/db_postgres.md", "onConflict": "skip", "text": "# DB (Postgres)\n\nIntegrate via library choice.\n" } } ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.java.db.generic.v1.json") @'
{
  "id": "recipe.java.db.generic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 5 },
  "title": "Java DB: Generic Notes",
  "description": "Adds db notes stub.",
  "tags": ["java", "db", "generic"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/db.md", "onConflict": "skip", "text": "# DB\n\nDocument db integration.\n" } } ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.java.auth.jwt_skeleton.v1.json") @'
{
  "id": "recipe.java.auth.jwt_skeleton.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 5 },
  "title": "Java Auth: JWT Notes",
  "description": "Adds auth notes stub.",
  "tags": ["java", "auth", "jwt"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/auth_jwt.md", "onConflict": "skip", "text": "# Auth (JWT)\n\nImplement via library choice.\n" } } ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.java.auth.session_skeleton.v1.json") @'
{
  "id": "recipe.java.auth.session_skeleton.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 5 },
  "title": "Java Auth: Session Notes",
  "description": "Adds auth notes stub.",
  "tags": ["java", "auth", "session"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/auth_session.md", "onConflict": "skip", "text": "# Auth (Session)\n\nDocument session strategy.\n" } } ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.java.auth.generic.v1.json") @'
{
  "id": "recipe.java.auth.generic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 5 },
  "title": "Java Auth: Generic Notes",
  "description": "Adds auth notes stub.",
  "tags": ["java", "auth", "generic"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/auth.md", "onConflict": "skip", "text": "# Auth\n\nDocument auth strategy.\n" } } ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.java.cache.memory.v1.json") @'
{
  "id": "recipe.java.cache.memory.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 5 },
  "title": "Java Cache: Memory Notes",
  "description": "Adds cache notes stub.",
  "tags": ["java", "cache", "memory"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/cache.md", "onConflict": "skip", "text": "# Cache\n\nIn-memory cache strategy.\n" } } ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.java.cache.external_hook.v1.json") @'
{
  "id": "recipe.java.cache.external_hook.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 5 },
  "title": "Java Cache: External Hook Notes",
  "description": "Adds cache notes stub.",
  "tags": ["java", "cache", "external"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/cache_external.md", "onConflict": "skip", "text": "# External Cache\n\nIntegrate via settings.\n" } } ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.java.docs.api.v1.json") @'
{
  "id": "recipe.java.docs.api.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 5 },
  "title": "Java Docs: API",
  "description": "Adds OpenAPI stub and routes doc.",
  "tags": ["java", "docs", "openapi"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/openapi.yaml", "onConflict": "skip", "text": "openapi: 3.0.0\ninfo:\n  title: ${projectName} API\n  version: 0.1.0\npaths: {}\n" } },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/routes.md", "onConflict": "skip", "text": "# Routes\n\nDocument routes.\n" } }
  ] }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.cpp.db.sqlite.v1.json") @'
{
  "id": "recipe.cpp.db.sqlite.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 5 },
  "title": "C++ DB: SQLite Notes",
  "description": "Adds db notes stub.",
  "tags": ["cpp", "db", "sqlite"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/db_sqlite.md", "onConflict": "skip", "text": "# DB (SQLite)\n\nIntegrate via library choice.\n" } } ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.cpp.db.postgres.v1.json") @'
{
  "id": "recipe.cpp.db.postgres.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 5 },
  "title": "C++ DB: Postgres Notes",
  "description": "Adds db notes stub.",
  "tags": ["cpp", "db", "postgres"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/db_postgres.md", "onConflict": "skip", "text": "# DB (Postgres)\n\nIntegrate via library choice.\n" } } ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.cpp.db.generic.v1.json") @'
{
  "id": "recipe.cpp.db.generic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 5 },
  "title": "C++ DB: Generic Notes",
  "description": "Adds db notes stub.",
  "tags": ["cpp", "db", "generic"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/db.md", "onConflict": "skip", "text": "# DB\n\nDocument db integration.\n" } } ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.cpp.auth.jwt_skeleton.v1.json") @'
{
  "id": "recipe.cpp.auth.jwt_skeleton.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 5 },
  "title": "C++ Auth: JWT Notes",
  "description": "Adds auth notes stub.",
  "tags": ["cpp", "auth", "jwt"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/auth_jwt.md", "onConflict": "skip", "text": "# Auth (JWT)\n\nIntegrate via library choice.\n" } } ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.cpp.auth.session_skeleton.v1.json") @'
{
  "id": "recipe.cpp.auth.session_skeleton.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 5 },
  "title": "C++ Auth: Session Notes",
  "description": "Adds auth notes stub.",
  "tags": ["cpp", "auth", "session"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/auth_session.md", "onConflict": "skip", "text": "# Auth (Session)\n\nDocument session strategy.\n" } } ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.cpp.auth.generic.v1.json") @'
{
  "id": "recipe.cpp.auth.generic.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 5 },
  "title": "C++ Auth: Generic Notes",
  "description": "Adds auth notes stub.",
  "tags": ["cpp", "auth", "generic"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/auth.md", "onConflict": "skip", "text": "# Auth\n\nDocument auth strategy.\n" } } ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.cpp.cache.memory.v1.json") @'
{
  "id": "recipe.cpp.cache.memory.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 5 },
  "title": "C++ Cache: Memory Notes",
  "description": "Adds cache notes stub.",
  "tags": ["cpp", "cache", "memory"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/cache.md", "onConflict": "skip", "text": "# Cache\n\nIn-memory cache strategy.\n" } } ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.cpp.cache.external_hook.v1.json") @'
{
  "id": "recipe.cpp.cache.external_hook.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 5 },
  "title": "C++ Cache: External Hook Notes",
  "description": "Adds cache notes stub.",
  "tags": ["cpp", "cache", "external"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [ { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/cache_external.md", "onConflict": "skip", "text": "# External Cache\n\nIntegrate via settings.\n" } } ] }
}
'@
Write-Json (Join-Path $bt "recipes\recipe.cpp.docs.api.v1.json") @'
{
  "id": "recipe.cpp.docs.api.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 5 },
  "title": "C++ Docs: API",
  "description": "Adds OpenAPI stub and routes doc.",
  "tags": ["cpp", "docs", "openapi"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "steps": [
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/openapi.yaml", "onConflict": "skip", "text": "openapi: 3.0.0\ninfo:\n  title: ${projectName} API\n  version: 0.1.0\npaths: {}\n" } },
    { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/docs/routes.md", "onConflict": "skip", "text": "# Routes\n\nDocument routes.\n" } }
  ] }
}
'@

Write-Host ""
Write-Host "Done seeding batch 9 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

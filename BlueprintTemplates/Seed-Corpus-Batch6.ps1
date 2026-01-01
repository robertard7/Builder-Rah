# BlueprintTemplates/Seed-Corpus-Batch6.ps1
# Batch6: CI pipeline + convert-folder flow + feature bundles (config/logging/http).
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
Ensure-Dir (Join-Path $bt "recipes")
Ensure-Dir (Join-Path $bt "graphs")

# -------------------------
# ATOMS: pipeline + conventions + feature bundle
# -------------------------

Write-Json (Join-Path $bt "atoms\task.ci.pipeline.generic.v1.json") @'
{
  "id": "task.ci.pipeline.generic.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "CI Pipeline (Generic)",
  "description": "Run a generic CI pipeline sequence (detect -> lint -> build -> test) using configured tasks.",
  "tags": ["ci", "pipeline", "generic"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "pipeline",
    "inputs": ["projectRoot"],
    "steps": [
      "task.detect.project_shape.v1",
      "task.format_lint.${ecosystem}.v1",
      "task.build.${ecosystem}.v1",
      "task.test.${ecosystem}.v1"
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.ensure_project_conventions.v1.json") @'
{
  "id": "task.ensure_project_conventions.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 5 },
  "title": "Ensure Project Conventions",
  "description": "Ensure conventional folders and documentation exist (src/, tests/, README) without overwriting existing files.",
  "tags": ["conventions", "structure", "safe"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "ensure_conventions",
    "inputs": ["projectRoot", "projectName"],
    "creates": [
      { "dir": "${projectRoot}/src" },
      { "dir": "${projectRoot}/tests" },
      { "file": "${projectRoot}/README.md", "onConflict": "skip" }
    ]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.add_feature.bundle.v1.json") @'
{
  "id": "task.add_feature.bundle.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 6 },
  "title": "Add Feature Bundle",
  "description": "Apply a known feature bundle by selecting recipes based on ecosystem.",
  "tags": ["feature", "bundle"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "add_feature_bundle",
    "inputs": ["projectRoot", "bundleName"],
    "bundles": {
      "logging_config_http": {
        "dotnet": ["recipe.dotnet.feature.logging_config_http.v1"],
        "node": ["recipe.node.feature.logging_config_http.v1"],
        "python": ["recipe.python.feature.logging_config_http.v1"],
        "go": ["recipe.go.feature.logging_config_http.v1"],
        "rust": ["recipe.rust.feature.logging_config_http.v1"],
        "java": ["recipe.java.feature.logging_config_http.v1"],
        "cpp": ["recipe.cpp.feature.logging_config_http.v1"]
      }
    }
  }
}
'@

# -------------------------
# PACKS: CI + Convert Folder + Feature Bundle
# -------------------------

Write-Json (Join-Path $bt "packs\pack.ci.pipeline.v1.json") @'
{
  "id": "pack.ci.pipeline.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 165 },
  "title": "CI Pipeline",
  "description": "Detect project shape then run lint/build/test pipeline.",
  "tags": ["ci", "pipeline", "build", "test"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.ci.pipeline.generic.v1"
    ]
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.project.convert.from_folder.v1.json") @'
{
  "id": "pack.project.convert.from_folder.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 160 },
  "title": "Convert Folder to Project",
  "description": "Turn an arbitrary folder into a recognized project: detect shape, add conventions, add a default feature bundle, validate.",
  "tags": ["convert", "project", "scaffold"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.detect.project_shape.v1",
      "task.ensure_project_conventions.v1",
      "task.add_feature.bundle.v1",
      "task.format_lint.${ecosystem}.v1",
      "task.build.${ecosystem}.v1",
      "task.test.${ecosystem}.v1"
    ],
    "defaults": { "bundleName": "logging_config_http" }
  }
}
'@

Write-Json (Join-Path $bt "packs\pack.feature.logging_config_http.v1.json") @'
{
  "id": "pack.feature.logging_config_http.v1",
  "kind": "pack",
  "version": 1,
  "meta": { "visibility": "public", "priority": 150 },
  "title": "Feature: Logging + Config + HTTP Client",
  "description": "Adds a minimal logging/config pattern and an HTTP client example appropriate for the detected ecosystem.",
  "tags": ["feature", "logging", "config", "http"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "uses": [
      "task.detect.project_shape.v1",
      "task.add_feature.bundle.v1"
    ],
    "defaults": { "bundleName": "logging_config_http" }
  }
}
'@

# -------------------------
# GRAPHS: route intents to new packs
# -------------------------

Write-Json (Join-Path $bt "graphs\graph.router.ci_and_convert.v1.json") @'
{
  "id": "graph.router.ci_and_convert.v1",
  "kind": "graph",
  "version": 1,
  "meta": { "visibility": "public" },
  "title": "Router: CI + Convert Folder",
  "description": "Routes intents: run pipeline, convert folder, add logging/config/http feature.",
  "tags": ["router", "ci", "convert", "feature"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "mermaid": "flowchart TD\n  Input --> Intent{intent?}\n  Intent -->|ci pipeline| CI[pack.ci.pipeline.v1]\n  Intent -->|convert folder| Convert[pack.project.convert.from_folder.v1]\n  Intent -->|add logging/config/http| Feature[pack.feature.logging_config_http.v1]\n  Intent -->|other| Fallback[graph.router.default_unstoppable.v1]"
  }
}
'@

# -------------------------
# RECIPES: feature bundle implementations (file-only)
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.dotnet.feature.logging_config_http.v1.json") @'
{
  "id": "recipe.dotnet.feature.logging_config_http.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 60 },
  "title": ".NET Feature: Logging + Config + HTTP",
  "description": "Adds appsettings.json and a small HttpClient sample with console logging.",
  "tags": ["dotnet", "logging", "config", "http"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/appsettings.json", "onConflict": "skip", "text": "{\n  \"App\": {\n    \"BaseUrl\": \"https://example.com\"\n  }\n}\n" } },
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/HttpSample.cs", "onConflict": "skip", "text": "using System;\nusing System.Net.Http;\nusing System.Threading.Tasks;\n\npublic static class HttpSample\n{\n  public static async Task RunAsync(string baseUrl)\n  {\n    using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };\n    Console.WriteLine($\"GET {client.BaseAddress}\");\n    using var resp = await client.GetAsync(\"/\");\n    Console.WriteLine($\"Status: {(int)resp.StatusCode}\");\n  }\n}\n" } }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.node.feature.logging_config_http.v1.json") @'
{
  "id": "recipe.node.feature.logging_config_http.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 60 },
  "title": "Node Feature: Logging + Config + HTTP",
  "description": "Adds config.json and a small HTTP fetch sample with console logging.",
  "tags": ["node", "logging", "config", "http"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/config.json", "onConflict": "skip", "text": "{\n  \"baseUrl\": \"https://example.com\"\n}\n" } },
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/httpSample.js", "onConflict": "skip", "text": "async function run(baseUrl) {\n  console.log(`GET ${baseUrl}/`);\n  const res = await fetch(`${baseUrl}/`);\n  console.log(`Status: ${res.status}`);\n}\n\nmodule.exports = { run };\n" } }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.python.feature.logging_config_http.v1.json") @'
{
  "id": "recipe.python.feature.logging_config_http.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 60 },
  "title": "Python Feature: Logging + Config + HTTP",
  "description": "Adds config.json and a tiny requests-based HTTP sample with logging.",
  "tags": ["python", "logging", "config", "http"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/config.json", "onConflict": "skip", "text": "{\n  \"baseUrl\": \"https://example.com\"\n}\n" } },
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/http_sample.py", "onConflict": "skip", "text": "import json\nimport logging\n\ntry:\n    import requests\nexcept Exception:\n    requests = None\n\nlogging.basicConfig(level=logging.INFO)\n\ndef run():\n    with open('config.json', 'r', encoding='utf-8') as f:\n        cfg = json.load(f)\n    base = cfg.get('baseUrl', 'https://example.com')\n    logging.info('GET %s/', base)\n    if requests is None:\n        logging.warning('requests not installed')\n        return\n    r = requests.get(base + '/')\n    logging.info('Status: %s', r.status_code)\n\nif __name__ == '__main__':\n    run()\n" } }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.go.feature.logging_config_http.v1.json") @'
{
  "id": "recipe.go.feature.logging_config_http.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 60 },
  "title": "Go Feature: Logging + Config + HTTP",
  "description": "Adds config.json and a minimal net/http client sample with logging.",
  "tags": ["go", "logging", "config", "http"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/config.json", "onConflict": "skip", "text": "{\n  \"baseUrl\": \"https://example.com\"\n}\n" } },
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/http_sample.go", "onConflict": "skip", "text": "package main\n\nimport (\n  \"encoding/json\"\n  \"io\"\n  \"log\"\n  \"net/http\"\n  \"os\"\n)\n\ntype Config struct { BaseUrl string `json:\"baseUrl\"` }\n\nfunc main() {\n  f, err := os.Open(\"config.json\")\n  if err != nil { log.Fatal(err) }\n  defer f.Close()\n\n  var cfg Config\n  if err := json.NewDecoder(f).Decode(&cfg); err != nil { log.Fatal(err) }\n  if cfg.BaseUrl == \"\" { cfg.BaseUrl = \"https://example.com\" }\n\n  log.Printf(\"GET %s/\", cfg.BaseUrl)\n  resp, err := http.Get(cfg.BaseUrl + \"/\")\n  if err != nil { log.Fatal(err) }\n  defer resp.Body.Close()\n  io.Copy(io.Discard, resp.Body)\n  log.Printf(\"Status: %d\", resp.StatusCode)\n}\n" } }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.rust.feature.logging_config_http.v1.json") @'
{
  "id": "recipe.rust.feature.logging_config_http.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 60 },
  "title": "Rust Feature: Logging + Config + HTTP",
  "description": "Adds config.json and a minimal HTTP example placeholder (no deps assumed).",
  "tags": ["rust", "logging", "config", "http"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/config.json", "onConflict": "skip", "text": "{\n  \"baseUrl\": \"https://example.com\"\n}\n" } },
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/http_sample.rs", "onConflict": "skip", "text": "use std::fs;\n\npub fn run() {\n    let cfg = fs::read_to_string(\"config.json\").unwrap_or_else(|_| \"{\\\"baseUrl\\\":\\\"https://example.com\\\"}\".to_string());\n    println!(\"config: {}\", cfg);\n    println!(\"HTTP sample: add an HTTP client crate per your settings if desired\");\n}\n" } }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.java.feature.logging_config_http.v1.json") @'
{
  "id": "recipe.java.feature.logging_config_http.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 60 },
  "title": "Java Feature: Logging + Config + HTTP",
  "description": "Adds config.properties and a minimal java.net.http client example.",
  "tags": ["java", "logging", "config", "http"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/config.properties", "onConflict": "skip", "text": "baseUrl=https://example.com\n" } },
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/main/java/${groupPath}/HttpSample.java", "onConflict": "skip", "text": "package ${groupId};\n\nimport java.net.URI;\nimport java.net.http.HttpClient;\nimport java.net.http.HttpRequest;\nimport java.net.http.HttpResponse;\nimport java.util.Properties;\nimport java.io.FileInputStream;\n\npublic final class HttpSample {\n  public static void run() throws Exception {\n    Properties p = new Properties();\n    try (FileInputStream in = new FileInputStream(\"config.properties\")) {\n      p.load(in);\n    }\n    String base = p.getProperty(\"baseUrl\", \"https://example.com\");\n    System.out.println(\"GET \" + base + \"/\");\n    HttpClient c = HttpClient.newHttpClient();\n    HttpRequest r = HttpRequest.newBuilder().uri(new URI(base + \"/\")).GET().build();\n    HttpResponse<String> resp = c.send(r, HttpResponse.BodyHandlers.ofString());\n    System.out.println(\"Status: \" + resp.statusCode());\n  }\n}\n" } }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.cpp.feature.logging_config_http.v1.json") @'
{
  "id": "recipe.cpp.feature.logging_config_http.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 60 },
  "title": "C++ Feature: Logging + Config + HTTP",
  "description": "Adds config.json and a placeholder HTTP sample (no library assumed).",
  "tags": ["cpp", "logging", "config", "http"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/config.json", "onConflict": "skip", "text": "{\n  \"baseUrl\": \"https://example.com\"\n}\n" } },
      { "use": "task.add_file.conflict_policy.v1", "with": { "path": "${projectRoot}/src/http_sample.cpp", "onConflict": "skip", "text": "#include <iostream>\n#include <fstream>\n#include <sstream>\n\nint main() {\n  std::ifstream f(\"config.json\");\n  std::stringstream ss;\n  ss << f.rdbuf();\n  std::cout << \"config: \" << ss.str() << std::endl;\n  std::cout << \"HTTP sample: wire an HTTP library via your settings/toolchain if desired\" << std::endl;\n  return 0;\n}\n" } }
    ]
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 6 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

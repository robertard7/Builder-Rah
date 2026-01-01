# BlueprintTemplates/Seed-Corpus-Batch4.ps1
# Batch4: Create-project atoms for missing ecosystems + build atoms + real recipes (file ops).
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
Ensure-Dir (Join-Path $bt "recipes")

# -------------------------
# ATOMS: create_project (missing coverage)
# Content is ABSTRACT. It declares intent + inputs + outputs, not commands.
# -------------------------

Write-Json (Join-Path $bt "atoms\task.create_project.node.cli.v1.json") @'
{
  "id": "task.create_project.node.cli.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 10 },
  "title": "Create Node CLI Project",
  "description": "Create a Node.js CLI project scaffold.",
  "tags": ["node", "cli", "create_project"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "create_project",
    "ecosystem": "node",
    "profile": "cli",
    "inputs": ["projectRoot", "projectName", "folderName"],
    "outputs": ["projectRoot", "entryFile"]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.create_project.node.web.v1.json") @'
{
  "id": "task.create_project.node.web.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 10 },
  "title": "Create Node Web Project",
  "description": "Create a basic Node.js web app scaffold.",
  "tags": ["node", "web", "create_project"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "create_project",
    "ecosystem": "node",
    "profile": "web",
    "inputs": ["projectRoot", "projectName", "folderName"],
    "outputs": ["projectRoot", "serverEntryFile"]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.create_project.python.cli.v1.json") @'
{
  "id": "task.create_project.python.cli.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 10 },
  "title": "Create Python CLI Project",
  "description": "Create a Python CLI scaffold (entry module, package layout).",
  "tags": ["python", "cli", "create_project"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "create_project",
    "ecosystem": "python",
    "profile": "cli",
    "inputs": ["projectRoot", "projectName", "folderName"],
    "outputs": ["projectRoot", "entryFile"]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.create_project.python.api.v1.json") @'
{
  "id": "task.create_project.python.api.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 10 },
  "title": "Create Python API Project",
  "description": "Create a Python API scaffold (app module, routing skeleton).",
  "tags": ["python", "api", "create_project"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "create_project",
    "ecosystem": "python",
    "profile": "api",
    "inputs": ["projectRoot", "projectName", "folderName"],
    "outputs": ["projectRoot", "appEntryFile"]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.create_project.go.cli.v1.json") @'
{
  "id": "task.create_project.go.cli.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 10 },
  "title": "Create Go CLI Project",
  "description": "Create a Go CLI scaffold (module + main entry).",
  "tags": ["go", "cli", "create_project"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "create_project",
    "ecosystem": "go",
    "profile": "cli",
    "inputs": ["projectRoot", "projectName", "folderName", "modulePath"],
    "outputs": ["projectRoot", "mainFile"]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.create_project.rust.cli.v1.json") @'
{
  "id": "task.create_project.rust.cli.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 10 },
  "title": "Create Rust CLI Project",
  "description": "Create a Rust CLI scaffold (crate + main entry).",
  "tags": ["rust", "cli", "create_project"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "create_project",
    "ecosystem": "rust",
    "profile": "cli",
    "inputs": ["projectRoot", "projectName", "folderName"],
    "outputs": ["projectRoot", "mainFile"]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.create_project.java.cli.v1.json") @'
{
  "id": "task.create_project.java.cli.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 10 },
  "title": "Create Java CLI Project (Maven)",
  "description": "Create a Java CLI scaffold using Maven conventions.",
  "tags": ["java", "cli", "maven", "create_project"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "create_project",
    "ecosystem": "java",
    "profile": "cli",
    "inputs": ["projectRoot", "projectName", "folderName", "groupId", "artifactId"],
    "outputs": ["projectRoot", "pomFile", "mainClassFile"]
  }
}
'@

Write-Json (Join-Path $bt "atoms\task.create_project.cpp.cmake.v1.json") @'
{
  "id": "task.create_project.cpp.cmake.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 10 },
  "title": "Create C++ Project (CMake)",
  "description": "Create a C++ scaffold using CMake (src/, include/, CMakeLists.txt).",
  "tags": ["cpp", "cmake", "create_project"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "action": "create_project",
    "ecosystem": "cpp",
    "profile": "cmake",
    "inputs": ["projectRoot", "projectName", "folderName"],
    "outputs": ["projectRoot", "cmakeFile", "mainFile"]
  }
}
'@

# -------------------------
# ATOMS: build (missing symmetry coverage)
# -------------------------

Write-Json (Join-Path $bt "atoms\task.build.go.v1.json") @'
{
  "id": "task.build.go.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 11 },
  "title": "Build Go",
  "description": "Build a Go project using configured build settings.",
  "tags": ["go", "build"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "build", "ecosystem": "go", "inputs": ["projectRoot"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.build.rust.v1.json") @'
{
  "id": "task.build.rust.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 11 },
  "title": "Build Rust",
  "description": "Build a Rust project using configured build settings.",
  "tags": ["rust", "build"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "build", "ecosystem": "rust", "inputs": ["projectRoot"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.build.java.v1.json") @'
{
  "id": "task.build.java.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 11 },
  "title": "Build Java (Maven)",
  "description": "Build a Java project using Maven configuration.",
  "tags": ["java", "maven", "build"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "build", "ecosystem": "java", "inputs": ["projectRoot"] }
}
'@

Write-Json (Join-Path $bt "atoms\task.build.cpp.v1.json") @'
{
  "id": "task.build.cpp.v1",
  "kind": "atom",
  "version": 1,
  "meta": { "visibility": "internal", "priority": 11 },
  "title": "Build C++ (CMake)",
  "description": "Build a C++ project using configured CMake settings.",
  "tags": ["cpp", "cmake", "build"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": { "action": "build", "ecosystem": "cpp", "inputs": ["projectRoot", "buildFolderName"] }
}
'@

# -------------------------
# RECIPES: real file scaffolds (using generic file ops)
# Note: recipes reference atoms by id with {"use": "<atomId>"} and provide file payloads.
# -------------------------

Write-Json (Join-Path $bt "recipes\recipe.node.web.basic_routes.v1.json") @'
{
  "id": "recipe.node.web.basic_routes.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 60 },
  "title": "Node Web: Basic Routes",
  "description": "Adds minimal web server routes: /health and /",
  "tags": ["node", "web", "routes"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.generic.v1", "with": { "path": "${projectRoot}/src/server.js", "text": "const http = require('http');\n\nconst port = process.env.PORT || 3000;\n\nfunction handler(req, res) {\n  if (req.url === '/health') {\n    res.writeHead(200, { 'Content-Type': 'application/json' });\n    res.end(JSON.stringify({ ok: true }));\n    return;\n  }\n  if (req.url === '/') {\n    res.writeHead(200, { 'Content-Type': 'text/plain' });\n    res.end('Hello from Node web app');\n    return;\n  }\n  res.writeHead(404, { 'Content-Type': 'text/plain' });\n  res.end('Not Found');\n}\n\nhttp.createServer(handler).listen(port, () => {\n  console.log(`listening on ${port}`);\n});\n" } }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.python.api.basic_endpoints.v1.json") @'
{
  "id": "recipe.python.api.basic_endpoints.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 60 },
  "title": "Python API: Basic Endpoints",
  "description": "Adds minimal API endpoints: /health and /",
  "tags": ["python", "api", "endpoints"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.generic.v1", "with": { "path": "${projectRoot}/app.py", "text": "from flask import Flask, jsonify\n\napp = Flask(__name__)\n\n@app.get('/health')\ndef health():\n    return jsonify({\"ok\": True})\n\n@app.get('/')\ndef root():\n    return 'Hello from Python API'\n\nif __name__ == '__main__':\n    app.run(host='0.0.0.0', port=5000)\n" } }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.go.cli.flags.v1.json") @'
{
  "id": "recipe.go.cli.flags.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 55 },
  "title": "Go CLI: Flags + Logging",
  "description": "Adds a minimal flag parser and basic logging output.",
  "tags": ["go", "cli", "flags"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.generic.v1", "with": { "path": "${projectRoot}/main.go", "text": "package main\n\nimport (\n  \"flag\"\n  \"fmt\"\n  \"log\"\n  \"os\"\n)\n\nfunc main() {\n  name := flag.String(\"name\", \"world\", \"name to greet\")\n  verbose := flag.Bool(\"v\", false, \"verbose logging\")\n  flag.Parse()\n\n  log.SetOutput(os.Stdout)\n  if *verbose {\n    log.Printf(\"verbose enabled\")\n  }\n\n  fmt.Printf(\"Hello, %s\\n\", *name)\n}\n" } }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.rust.cli.args_logging.v1.json") @'
{
  "id": "recipe.rust.cli.args_logging.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 55 },
  "title": "Rust CLI: Args + Logging",
  "description": "Adds minimal args handling and stdout logging.",
  "tags": ["rust", "cli", "args"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.generic.v1", "with": { "path": "${projectRoot}/src/main.rs", "text": "use std::env;\n\nfn main() {\n    let args: Vec<String> = env::args().collect();\n    let name = args.get(1).map(|s| s.as_str()).unwrap_or(\"world\");\n    println!(\"Hello, {}\", name);\n}\n" } }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.java.cli.maven.hello.v1.json") @'
{
  "id": "recipe.java.cli.maven.hello.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 55 },
  "title": "Java CLI (Maven): Hello",
  "description": "Adds a minimal Main class output.",
  "tags": ["java", "cli", "maven"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.generic.v1", "with": { "path": "${projectRoot}/src/main/java/${groupPath}/Main.java", "text": "package ${groupId};\n\npublic final class Main {\n  public static void main(String[] args) {\n    String name = (args.length > 0) ? args[0] : \"world\";\n    System.out.println(\"Hello, \" + name);\n  }\n}\n" } }
    ]
  }
}
'@

Write-Json (Join-Path $bt "recipes\recipe.cpp.cmake.hello_app.v1.json") @'
{
  "id": "recipe.cpp.cmake.hello_app.v1",
  "kind": "recipe",
  "version": 1,
  "meta": { "visibility": "public", "priority": 55 },
  "title": "C++ (CMake): Hello App",
  "description": "Adds minimal main.cpp and CMakeLists.txt for an executable.",
  "tags": ["cpp", "cmake", "hello"],
  "updatedUtc": "2025-12-28T00:00:00Z",
  "content": {
    "steps": [
      { "use": "task.add_file.generic.v1", "with": { "path": "${projectRoot}/src/main.cpp", "text": "#include <iostream>\n\nint main(int argc, char** argv) {\n  const char* name = (argc > 1) ? argv[1] : \"world\";\n  std::cout << \"Hello, \" << name << std::endl;\n  return 0;\n}\n" } },
      { "use": "task.add_file.generic.v1", "with": { "path": "${projectRoot}/CMakeLists.txt", "text": "cmake_minimum_required(VERSION 3.20)\nproject(${projectName} LANGUAGES CXX)\n\nset(CMAKE_CXX_STANDARD 20)\nset(CMAKE_CXX_STANDARD_REQUIRED ON)\n\nadd_executable(${projectName}\n  src/main.cpp\n)\n" } }
    ]
  }
}
'@

Write-Host ""
Write-Host "Done seeding batch 4 into: $bt"
Write-Host "Next: run Update-BlueprintManifest.ps1 from repo root."

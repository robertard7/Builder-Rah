$ErrorActionPreference = "Stop"

$repoRoot = Join-Path $PSScriptRoot ".."
$openApi = Join-Path $repoRoot "openapi.yaml"
$outputDir = Join-Path $repoRoot "tools" "generated-sdks"

if (!(Test-Path $openApi)) {
  Write-Error "openapi.yaml missing"
}

if (Test-Path $outputDir) {
  Remove-Item -Recurse -Force $outputDir
}

New-Item -ItemType Directory -Path $outputDir | Out-Null

$generator = "@openapitools/openapi-generator-cli"

npx $generator generate -i $openApi -g python -o (Join-Path $outputDir "python")
npx $generator generate -i $openApi -g csharp-netcore -o (Join-Path $outputDir "dotnet")
npx $generator generate -i $openApi -g rust -o (Join-Path $outputDir "rust")

Write-Host "Generated SDKs in $outputDir"

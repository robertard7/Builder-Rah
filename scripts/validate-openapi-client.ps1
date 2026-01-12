$ErrorActionPreference = "Stop"

$toolsDir = Join-Path $PSScriptRoot ".." "tools"
if (!(Test-Path $toolsDir)) {
  Write-Error "tools directory missing"
}

Push-Location $toolsDir
try {
  npm install
  npm run generate:openapi-types
  npm run compare:openapi
} finally {
  Pop-Location
}

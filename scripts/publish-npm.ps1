$ErrorActionPreference = "Stop"

$toolsDir = Join-Path $PSScriptRoot ".." "tools"

Push-Location $toolsDir
try {
  npm publish --access public
} finally {
  Pop-Location
}

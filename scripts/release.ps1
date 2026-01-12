param(
  [Parameter(Mandatory = $true)]
  [string]$Version
)

$ErrorActionPreference = "Stop"

$toolsDir = Join-Path $PSScriptRoot ".." "tools"

Push-Location $toolsDir
try {
  npm version $Version --no-git-tag-version
} finally {
  Pop-Location
}

git tag "v$Version"
Write-Host "Tagged release v$Version"

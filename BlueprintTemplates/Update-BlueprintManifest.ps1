# Update-BlueprintManifest.ps1
# Minimal manifest updater: appends missing entries for BlueprintTemplates/*.json files.
# Does NOT regenerate everything. Keeps existing entries. Safe for incremental drops.

$ErrorActionPreference = "Stop"

function Read-Utf8NoBom([string]$path) {
  $bytes = [System.IO.File]::ReadAllBytes($path)
  if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
    return [System.Text.Encoding]::UTF8.GetString($bytes, 3, $bytes.Length - 3)
  }
  return [System.Text.Encoding]::UTF8.GetString($bytes)
}

function Write-Utf8([string]$path, [string]$text) {
  [System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
}

function Get-Json([string]$path) {
  $raw = Read-Utf8NoBom $path
  return $raw | ConvertFrom-Json
}

# Root is the folder containing this script (BlueprintTemplates/)
$btRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$manifestPath = Join-Path $btRoot "manifest.json"

if (-not (Test-Path -LiteralPath $manifestPath)) {
  throw "manifest.json not found at: $manifestPath"
}

$manifest = Get-Json $manifestPath

if ($null -eq $manifest.entries) { $manifest | Add-Member -NotePropertyName entries -NotePropertyValue @() }
$existing = @{}
foreach ($e in $manifest.entries) {
  if ($null -ne $e.id -and $e.id.Trim().Length -gt 0) { $existing[$e.id] = $true }
}

# Scan these folders for templates
$folders = @("packs","recipes","atoms","graphs")
$added = 0

foreach ($f in $folders) {
  $dir = Join-Path $btRoot $f
  if (-not (Test-Path -LiteralPath $dir)) { continue }

  Get-ChildItem -LiteralPath $dir -Filter "*.json" -File | ForEach-Object {
    $path = $_.FullName
    try {
      $tpl = Get-Json $path
      $id = ($tpl.id | ForEach-Object { "$_" }).Trim()
      if ($id.Length -eq 0) { return }

      if ($existing.ContainsKey($id)) { return }

      $kind = (($tpl.kind | ForEach-Object { "$_" }).Trim())
      if ($kind.Length -eq 0) { $kind = $f.TrimEnd('s') } # fallback: packs->pack etc

      $metaVis = ""
      $metaPri = $null
      if ($null -ne $tpl.meta) {
        if ($null -ne $tpl.meta.visibility) { $metaVis = "$($tpl.meta.visibility)" }
        if ($null -ne $tpl.meta.priority) { $metaPri = [int]$tpl.meta.priority }
      }

      # Relative file path stored like: BlueprintTemplates/packs/filename.json
      $relFile = "BlueprintTemplates/$f/$($_.Name)"

      $entry = [ordered]@{
        id   = $id
        kind = $kind
        file = $relFile
      }

      # Carry tags if present
      if ($null -ne $tpl.tags) { $entry.tags = @($tpl.tags) }

      # Carry meta only if meaningful
      $meta = [ordered]@{}
      if ($metaVis.Trim().Length -gt 0) { $meta.visibility = $metaVis.Trim() }
      if ($null -ne $metaPri) { $meta.priority = $metaPri }
      if ($meta.Count -gt 0) { $entry.meta = $meta }

      $manifest.entries += (New-Object PSObject -Property $entry)
      $existing[$id] = $true
      $added++
    }
    catch {
      Write-Host ("Skip invalid template: " + $path) -ForegroundColor Yellow
    }
  }
}

# Sort entries for stable UI (kind then id)
$manifest.entries = @($manifest.entries | Sort-Object kind, id)

# Write back pretty JSON
$jsonOut = $manifest | ConvertTo-Json -Depth 64
Write-Utf8 $manifestPath $jsonOut

Write-Host ("Manifest updated: " + $manifestPath)
Write-Host ("Added entries: " + $added)
Write-Host ("Total entries: " + @($manifest.entries).Count)

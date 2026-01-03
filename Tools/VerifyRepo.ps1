# Ensure errors bubble up unless explicitly handled
$ErrorActionPreference = 'Stop'

function Write-Block {
    param(
        [string]$Title
    )
    Write-Host "=== $Title ==="
}

function TryGitDiffStat {
    try {
        git rev-parse HEAD~1 *> $null
        git diff --stat HEAD~1..HEAD
    }
    catch {
        git diff --stat HEAD
    }
}

function Run-Step {
    param(
        [string]$Title,
        [scriptblock]$Action
    )

    Write-Block $Title
    try {
        & $Action
    }
    catch {
        Write-Host "Command failed: $($_.Exception.Message)"
    }
    Write-Host ""
}

Run-Step "git rev-parse HEAD" { git rev-parse HEAD }
Run-Step "git status --porcelain" { git status --porcelain }
Run-Step "git diff --stat (last commit)" { TryGitDiffStat }
Run-Step "dotnet --info" { dotnet --info }
Run-Step "dotnet build RahBuilder.sln -v minimal" { dotnet build RahBuilder.sln -v minimal }

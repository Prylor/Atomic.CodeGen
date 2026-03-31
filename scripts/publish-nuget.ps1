#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Pack and publish Atomic.CodeGen to NuGet.org
.PARAMETER ApiKey
    NuGet API key. Falls back to NUGET_API_KEY env var.
.PARAMETER Version
    Override package version (optional — uses version from .csproj by default)
.PARAMETER DryRun
    Pack only, don't push to NuGet
#>
param(
    [string]$ApiKey = $env:NUGET_API_KEY,
    [string]$Version,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = "$root/src/Atomic.CodeGen/Atomic.CodeGen.csproj"
$outputDir = "$root/nupkg"

if (-not $DryRun -and [string]::IsNullOrEmpty($ApiKey)) {
    Write-Error "NuGet API key required. Pass -ApiKey or set NUGET_API_KEY env var."
    exit 1
}

Push-Location $root
try {
    # Clean output
    if (Test-Path $outputDir) { Remove-Item -Recurse -Force $outputDir }
    New-Item -ItemType Directory -Path $outputDir | Out-Null

    # Build
    Write-Host "Building Release..." -ForegroundColor Cyan
    dotnet build $project --configuration Release

    # Pack
    $packArgs = @("pack", $project, "--configuration", "Release", "--no-build", "--output", $outputDir)
    if ($Version) {
        $packArgs += @("/p:Version=$Version")
    }
    Write-Host "Packing..." -ForegroundColor Cyan
    & dotnet @packArgs

    $nupkg = Get-ChildItem "$outputDir/*.nupkg" | Select-Object -First 1
    Write-Host "Package: $($nupkg.Name)" -ForegroundColor Green

    if ($DryRun) {
        Write-Host "Dry run — skipping push." -ForegroundColor Yellow
        return
    }

    # Push
    Write-Host "Pushing to NuGet.org..." -ForegroundColor Cyan
    dotnet nuget push $nupkg.FullName --api-key $ApiKey --source https://api.nuget.org/v3/index.json --skip-duplicate

    Write-Host "Published successfully!" -ForegroundColor Green
} finally {
    Pop-Location
}

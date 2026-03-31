#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build Atomic.CodeGen
.PARAMETER Configuration
    Build configuration (Debug/Release). Default: Release
.PARAMETER Clean
    Clean before building
#>
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

Push-Location $root
try {
    if ($Clean) {
        Write-Host "Cleaning..." -ForegroundColor Cyan
        dotnet clean --configuration $Configuration --verbosity minimal
    }

    Write-Host "Restoring packages..." -ForegroundColor Cyan
    dotnet restore

    Write-Host "Building ($Configuration)..." -ForegroundColor Cyan
    dotnet build --configuration $Configuration --no-restore

    Write-Host "Build succeeded!" -ForegroundColor Green
} finally {
    Pop-Location
}

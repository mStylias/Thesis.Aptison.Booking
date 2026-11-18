#Requires -Version 5.1
<#
.SYNOPSIS
    Runs the test suites.

.DESCRIPTION
    TUnit runs on Microsoft.Testing.Platform, and each test project is its own executable. On the
    SDK here (10.0.202) `dotnet test` starts those executables but discovers nothing in them, so
    every project is run with `dotnet run --project` instead. The behaviour reproduces in the Synax
    repository, so it is the tooling and not this solution's wiring.

    Unit tests need nothing. Integration tests need Docker: the first run builds
    Dockerfile.postgres, which takes a minute or two, and every run after it is a cache hit.

.PARAMETER Unit
    Unit tests only. No Docker.

.PARAMETER Integration
    Integration tests only.

.PARAMETER Filter
    A TUnit --treenode-filter, e.g. '/*/*/CreateAppointmentTests/*'.

.EXAMPLE
    scripts\run-tests.ps1
    scripts\run-tests.ps1 -Unit
    scripts\run-tests.ps1 -Integration -Filter '/*/*/AppointmentRaceTests/*'
#>

[CmdletBinding()]
param(
    [switch]$Unit,
    [switch]$Integration,
    [string]$Filter
)

$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot

$projects = @()
if (-not $Integration) {
    $projects += Join-Path $RepoRoot 'tests\UnitTests\Aptison.Booking.Application.UnitTests'
}
if (-not $Unit) {
    $projects += Join-Path $RepoRoot 'tests\IntegrationTests\Aptison.Booking.Application.IntegrationTests'
}

$failed = @()

foreach ($project in $projects) {
    Write-Host ''
    Write-Host "Running $(Split-Path -Leaf $project)" -ForegroundColor Cyan

    $arguments = @('run', '--project', $project)
    if ($Filter) { $arguments += @('--', '--treenode-filter', $Filter) }

    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { $failed += (Split-Path -Leaf $project) }
}

Write-Host ''
if ($failed.Count -gt 0) {
    Write-Host "Failed: $($failed -join ', ')" -ForegroundColor Red
    exit 1
}

Write-Host 'All suites passed.' -ForegroundColor Green

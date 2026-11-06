<#
.SYNOPSIS
    Builds the Windows player to Build/ProjectVania/ProjectVania.exe (Unity batch mode; the Editor must be closed).
.DESCRIPTION
    Runs unity.ps1 build (ProjectBuilder.Build). Exits non-zero when Unity fails or the exe is not produced.
    The Unity log goes to Logs/unity-build.log.
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'Build/ProjectVania/ProjectVania.exe'

Write-Host '  Building Windows player...' -ForegroundColor Cyan
& (Join-Path $root 'unity.ps1') -Mode build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if (-not (Test-Path $exe)) {
    Write-Host "Unity exited 0 but $exe was not produced; see Logs/unity-build.log" -ForegroundColor Red
    exit 1
}
Write-Host "Built $exe"
exit 0

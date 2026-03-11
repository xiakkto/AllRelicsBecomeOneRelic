Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$gameRoot = Split-Path -Parent (Split-Path -Parent $projectRoot)
$builtDll = Join-Path $projectRoot "bin\\Release\\AllRelicsBecomeOneRelic.dll"
$targetDll = Join-Path $gameRoot "mods\\AllRelicsBecomeOneRelic\\AllRelicsBecomeOneRelic.dll"

if (-not (Test-Path $builtDll)) {
    throw "Build output not found: $builtDll"
}

Copy-Item $builtDll $targetDll -Force
Write-Host "Installed latest DLL:"
Write-Host "  $targetDll"

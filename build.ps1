Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$gameRoot = Split-Path -Parent (Split-Path -Parent $projectRoot)
$projectFile = Join-Path $projectRoot "AllRelicsBecomeOneRelic.csproj"
$configTemplate = Join-Path $projectRoot "AllRelicsBecomeOneRelic.json"
$packProjectDir = Join-Path $projectRoot "pack"
$outputDir = Join-Path $gameRoot "mods\\AllRelicsBecomeOneRelic"
$outputDll = Join-Path $outputDir "AllRelicsBecomeOneRelic.dll"
$outputPck = Join-Path $outputDir "AllRelicsBecomeOneRelic.pck"
$outputConfig = Join-Path $outputDir "AllRelicsBecomeOneRelic.json"
$tempPck = Join-Path $projectRoot "bin\\Release\\AllRelicsBecomeOneRelic.pck"
$godotExe = Join-Path $gameRoot "SlayTheSpire2.exe"
$relativePackProjectDir = ".\\mod_projects\\AllRelicsBecomeOneRelic\\pack"
$relativePackScript = "pack_mod.gd"

dotnet build $projectFile -c Release | Out-Host

$builtDll = Join-Path $projectRoot "bin\\Release\\AllRelicsBecomeOneRelic.dll"
if (-not (Test-Path $builtDll)) {
    throw "Build output not found: $builtDll"
}

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
Copy-Item $builtDll $outputDll -Force

if (-not (Test-Path $outputConfig)) {
    Copy-Item $configTemplate $outputConfig
}

if (Test-Path $tempPck) {
    Remove-Item $tempPck -Force
}

Push-Location $gameRoot
try {
    & $godotExe --headless --nomods --path $relativePackProjectDir --script $relativePackScript -- $packProjectDir $tempPck
    if ($LASTEXITCODE -ne 0) {
        throw "Godot pack command failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

for ($i = 0; $i -lt 20 -and -not (Test-Path $tempPck); $i++) {
    Start-Sleep -Milliseconds 250
}

if (-not (Test-Path $tempPck)) {
    throw "PCK build failed: $tempPck was not created."
}

Copy-Item $tempPck $outputPck -Force

Write-Host "Built mod:"
Write-Host "  $outputDll"
Write-Host "  $outputPck"
Write-Host "Config:"
Write-Host "  $outputConfig"

<#
.SYNOPSIS
    Build and install the Hello Native sample app into eStarter's app directory.
.DESCRIPTION
    Publishes HelloNative and copies the output + manifest.json to
    %LOCALAPPDATA%\eStarter\apps\hello.native\
    After running this script, launch eStarter and click "Refresh" to see the tile.
#>
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent | Split-Path -Parent
$projectDir = Join-Path $repoRoot "samples\hello-native"
$publishDir = Join-Path $projectDir "publish"
$destDir = Join-Path $env:LOCALAPPDATA "eStarter\apps\hello.native"

Write-Host "=== Building HelloNative ($Configuration) ===" -ForegroundColor Cyan
dotnet publish "$projectDir\HelloNative.csproj" -c $Configuration -o $publishDir --self-contained false
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

Write-Host "=== Installing to $destDir ===" -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $destDir | Out-Null
Copy-Item -Path "$publishDir\*" -Destination $destDir -Force -Recurse
Copy-Item -Path "$projectDir\manifest.json" -Destination $destDir -Force

Write-Host ""
Write-Host "=== Done! ===" -ForegroundColor Green
Write-Host "Start eStarter and click Refresh. The 'Hello Native' tile should appear."
Write-Host "Click the tile to launch HelloNative through ProcessHost."
Write-Host ""
Write-Host "Check Debug Output for lines like:"
Write-Host '  [HelloNative] Starting as "hello.native"...'
Write-Host '  [HelloNative] IsHostedMode = True'
Write-Host '  [HelloNative] Connected.'
Write-Host '  [HelloNative] Ping: OK'

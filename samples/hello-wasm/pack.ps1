# Pack hello-wasm as .eapp package
param([string]$OutputDir = ".")

$ErrorActionPreference = "Stop"
$Dir = Split-Path $MyInvocation.MyCommand.Path -Parent

# Build the WASM module
Write-Host "Generating hello.wasm..." -ForegroundColor Cyan
dotnet run --project "$Dir\WatCompiler\WatCompiler.csproj" -c Release -- "$Dir\hello.wasm"

$manifest = Get-Content "$Dir\manifest.json" | ConvertFrom-Json
$eappName = "$($manifest.id)-$($manifest.version).eapp"
$zipPath  = Join-Path $OutputDir ([System.IO.Path]::ChangeExtension($eappName, ".zip"))
$eappPath = Join-Path $OutputDir $eappName

# Stage files
$stage = Join-Path $Dir "stage"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null
Copy-Item "$Dir\manifest.json" "$stage\manifest.json"
Copy-Item "$Dir\hello.wasm" "$stage\hello.wasm"

# Create archive
if (Test-Path $zipPath) { Remove-Item $zipPath }
Compress-Archive -Path "$stage\*" -DestinationPath $zipPath -Force
if (Test-Path $eappPath) { Remove-Item $eappPath }
Move-Item $zipPath $eappPath -Force
Remove-Item $stage -Recurse -Force

$size = [math]::Round((Get-Item $eappPath).Length / 1KB, 1)
Write-Host "Created: $eappPath ($size KB)" -ForegroundColor Green

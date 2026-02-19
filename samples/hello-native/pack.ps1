# Pack HelloNative as .eapp package
# Usage: .\pack.ps1
param(
    [string]$OutputDir = "."
)

$ErrorActionPreference = "Stop"
$ProjectDir = Split-Path $MyInvocation.MyCommand.Path -Parent
$SolutionDir = Resolve-Path "$ProjectDir\..\.."

# Build and publish
Write-Host "Building HelloNative..." -ForegroundColor Cyan
dotnet publish "$ProjectDir\HelloNative.csproj" -c Release -o "$ProjectDir\publish" --self-contained false | Out-Null

# Read manifest to get app id
$manifest = Get-Content "$ProjectDir\manifest.json" | ConvertFrom-Json
$appId = $manifest.id
$version = $manifest.version
$eappName = "$appId-$version.eapp"
$eappPath = Join-Path $OutputDir $eappName

# Remove old package
if (Test-Path $eappPath) { Remove-Item $eappPath }

# Create .eapp (zip with .eapp extension)
Write-Host "Packing $eappName..." -ForegroundColor Cyan
Copy-Item "$ProjectDir\manifest.json" "$ProjectDir\publish\manifest.json" -Force

# Compress-Archive requires .zip extension; create as .zip then rename.
$zipPath = [System.IO.Path]::ChangeExtension($eappPath, ".zip")
if (Test-Path $zipPath) { Remove-Item $zipPath }
Compress-Archive -Path "$ProjectDir\publish\*" -DestinationPath $zipPath -Force
if (Test-Path $eappPath) { Remove-Item $eappPath }
Move-Item $zipPath $eappPath -Force

$size = [math]::Round((Get-Item $eappPath).Length / 1KB, 1)
Write-Host "Created: $eappPath ($size KB)" -ForegroundColor Green

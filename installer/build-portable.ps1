param(
    [ValidateSet("x64", "x86")][string]$Arch = "x64"
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$publish = Join-Path $root "artifacts\portable-$Arch"
$zip = Join-Path $root "artifacts\HeadphoneHub-Portable-$Arch.zip"

$proj = Get-ChildItem $root -Filter *.csproj | Select-Object -First 1
if (-not $proj) { throw 'HeadphoneHub.csproj not found.' }

if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish $proj.FullName -c Release -r win-$Arch --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none -p:DebugSymbols=false -o $publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

Set-Content -Path (Join-Path $publish 'portable.txt') -Value 'Settings are kept in the data folder next to HeadphoneHub.exe. Delete this file to use %AppData% instead.'

if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip
Write-Host "Done: artifacts\HeadphoneHub-Portable-$Arch.zip"

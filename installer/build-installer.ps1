param(
    [string]$Build = "1",
    [ValidateSet("x64", "x86")][string]$Arch = "x64"
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$publish = Join-Path $root "artifacts\publish-$Arch"

$proj = Get-ChildItem $root -Filter *.csproj | Select-Object -First 1
if (-not $proj) { throw 'HeadphoneHub.csproj not found.' }

if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish $proj.FullName -c Release -r win-$Arch --self-contained true -p:DebugType=none -p:DebugSymbols=false -o $publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

$exe = Get-ChildItem $publish -Filter *.exe | Where-Object { $_.Name -like 'HeadphoneHub*' } | Select-Object -First 1
if (-not $exe) { throw 'The published app .exe was not found.' }

$candidates = @(
    "${env:ProgramFiles(x86)}\NSIS\makensis.exe",
    "$env:ProgramFiles\NSIS\makensis.exe"
)
$makensis = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $makensis) { $makensis = (Get-Command makensis.exe -ErrorAction SilentlyContinue).Source }
if (-not $makensis) { throw 'makensis.exe not found. Install NSIS from https://nsis.sourceforge.io' }

New-Item -ItemType Directory -Path (Join-Path $root 'artifacts') -Force | Out-Null
& $makensis "/DAPP_BUILD=$Build" "/DARCH=$Arch" "/DPUBLISH_DIR=$publish" "/DAPP_EXE=$($exe.Name)" (Join-Path $PSScriptRoot 'HeadphoneHub.nsi')
if ($LASTEXITCODE -ne 0) { throw 'makensis failed.' }

Write-Host "Done: artifacts\HeadphoneHub-$Arch-Setup.exe"

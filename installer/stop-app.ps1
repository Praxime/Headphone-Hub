param([Parameter(Mandatory = $true)][string]$Dir)

$ErrorActionPreference = 'SilentlyContinue'

if (-not (Test-Path $Dir)) { exit 0 }
$root = (Resolve-Path $Dir).Path.TrimEnd('\') + '\'

$procs = @(Get-Process | Where-Object { $_.Path -and $_.Path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) })
$app = @($procs | Where-Object { $_.ProcessName -like 'HeadphoneHub*' })

foreach ($p in $app) { [void]$p.CloseMainWindow() }
if ($app.Count -gt 0) { Start-Sleep -Seconds 2 }

foreach ($p in $procs) {
    $p.Refresh()
    if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }
}

Start-Sleep -Milliseconds 500
exit 0

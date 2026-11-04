$ErrorActionPreference = 'Stop'
$dir = Join-Path $PSScriptRoot '..\pocketbase'
$exe = Join-Path $dir 'pocketbase.exe'

if (Test-Path $exe) { Write-Host 'PocketBase jau atsisiustas.'; exit 0 }

$release = Invoke-RestMethod 'https://api.github.com/repos/pocketbase/pocketbase/releases/latest' -Headers @{ 'User-Agent' = 'nesimokau' }
$asset = $release.assets | Where-Object { $_.name -like '*windows_amd64.zip' } | Select-Object -First 1
if (-not $asset) { throw 'Nerastas Windows PocketBase failas.' }

$zip = Join-Path $dir 'pocketbase.zip'
Write-Host "Atsisiunčiama $($asset.name)..."
Invoke-WebRequest $asset.browser_download_url -OutFile $zip
Expand-Archive $zip -DestinationPath $dir -Force
Remove-Item $zip
Write-Host 'Paruošta: pocketbase\pocketbase.exe'

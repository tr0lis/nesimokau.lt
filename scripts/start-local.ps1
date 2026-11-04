# Paleidžia Supabase API (3011) ir Blazor (http://localhost:5232).
$root = Split-Path $PSScriptRoot -Parent
if (-not (Test-Path (Join-Path $root '.env.local'))) {
    Write-Host 'Nėra .env.local – nukopijuok .env.example į .env.local ir užpildyk Supabase/API kintamuosius.' -ForegroundColor Yellow
    exit 1
}
if (-not (Test-Path (Join-Path $root 'node_modules'))) { Push-Location $root; npm install; Pop-Location }

Start-Process powershell -ArgumentList '-NoExit', '-Command', "Set-Location '$root'; npm run dev:api"
Start-Sleep -Seconds 2
Set-Location $root
dotnet run --launch-profile http

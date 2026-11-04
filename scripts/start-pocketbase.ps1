$dir = Join-Path $PSScriptRoot '..\pocketbase'
$exe = Join-Path $dir 'pocketbase.exe'
if (-not (Test-Path $exe)) { & (Join-Path $PSScriptRoot 'setup-pocketbase.ps1') }

# --dir naudoja pb_data, --migrationsDir – mūsų kolekcijų migracijas (pritaikomos automatiškai)
& $exe serve --http 127.0.0.1:8090 --dir (Join-Path $dir 'pb_data') --migrationsDir (Join-Path $dir 'pb_migrations')

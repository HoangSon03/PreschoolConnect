$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$postgresRoot = Join-Path $repoRoot '.postgres'
$binPath = Join-Path $postgresRoot 'bin'
$dataPath = Join-Path $postgresRoot 'data'
$logPath = Join-Path $postgresRoot 'postgres.log'

if (-not (Test-Path $dataPath)) {
    New-Item -ItemType Directory -Path $dataPath -Force | Out-Null
}

if (-not (Test-Path (Join-Path $dataPath 'PG_VERSION'))) {
    Write-Host 'Initializing PostgreSQL cluster in .postgres/data...'
    & (Join-Path $binPath 'initdb.exe') -D $dataPath -U postgres --auth=trust --encoding=UTF8
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $dataPath 'PG_VERSION'))) {
        throw "PostgreSQL cluster initialization failed with exit code $LASTEXITCODE."
    }
}

& (Join-Path $binPath 'pg_ctl.exe') -D $dataPath status 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host 'PostgreSQL is already running.'
} else {
    Write-Host 'Starting PostgreSQL...'
    & (Join-Path $binPath 'pg_ctl.exe') -D $dataPath -l $logPath -o "-p 5432 -h 127.0.0.1" start
    if ($LASTEXITCODE -ne 0) {
        throw "PostgreSQL startup failed with exit code $LASTEXITCODE."
    }
}

Write-Host 'Waiting for PostgreSQL to accept connections...'
$ready = $false
for ($i = 0; $i -lt 30; $i++) {
    & (Join-Path $binPath 'pg_isready.exe') -h localhost -p 5432 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        $ready = $true
        break
    }
    Start-Sleep -Seconds 1
}

if (-not $ready) {
    throw 'PostgreSQL did not become ready on localhost:5432.'
}

$psql = Join-Path $binPath 'psql.exe'

& $psql -h localhost -p 5432 -U postgres -d postgres -tAc "SELECT 1" 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'Could not connect to the local PostgreSQL cluster using its configured authentication.'
}

& $psql -h localhost -p 5432 -U postgres -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='preschoolconnect'" | Tee-Object -Variable dbExists | Out-Null
if (-not ($dbExists -match '1')) {
    Write-Host 'Creating the preschoolconnect database...'
    & (Join-Path $binPath 'createdb.exe') -h localhost -p 5432 -U postgres preschoolconnect
    if ($LASTEXITCODE -ne 0) {
        throw "Creating the preschoolconnect database failed with exit code $LASTEXITCODE."
    }
}

Write-Host 'PostgreSQL is ready for the Preschool Connect app.'

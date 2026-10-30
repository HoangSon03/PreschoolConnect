$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$postgresRoot = Join-Path $repoRoot '.postgres'
$dataPath = Join-Path $postgresRoot 'data'
$binPath = Join-Path $postgresRoot 'bin'

if (Test-Path $dataPath) {
    & (Join-Path $binPath 'pg_ctl.exe') -D $dataPath stop
}

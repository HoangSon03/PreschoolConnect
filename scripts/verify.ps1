$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$dotnetRoot = "$HOME\.dotnet"
if (-not (Test-Path (Join-Path $dotnetRoot 'dotnet.exe'))) {
    throw "The project-pinned .NET SDK installation was not found at $dotnetRoot."
}

$env:DOTNET_ROOT = $dotnetRoot
$env:PATH = "$dotnetRoot;$env:PATH"

Set-Location $repoRoot

Write-Host 'Running .NET tests...'
dotnet test server/PreschoolConnect.Tests/PreschoolConnect.Tests.csproj --nologo -v minimal
if ($LASTEXITCODE -ne 0) {
    throw ".NET tests failed with exit code $LASTEXITCODE."
}

Write-Host 'Running frontend build...'
Set-Location "$repoRoot/client"
npm run build
if ($LASTEXITCODE -ne 0) {
    throw "Frontend build failed with exit code $LASTEXITCODE."
}

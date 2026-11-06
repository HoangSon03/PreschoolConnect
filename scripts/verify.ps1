$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$dotnetRoot = "$HOME\.dotnet"
if (Test-Path (Join-Path $dotnetRoot 'dotnet.exe')) {
    $env:DOTNET_ROOT = $dotnetRoot
    $env:PATH = "$dotnetRoot;$env:PATH"
}
elseif (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "No .NET SDK was found in $dotnetRoot or on PATH."
}

Set-Location $repoRoot
$sdkVersion = dotnet --version
if ($LASTEXITCODE -ne 0) {
    throw "The .NET SDK selected by global.json could not be started."
}
Write-Host "Using .NET SDK $sdkVersion"

Write-Host 'Running .NET tests...'
dotnet test server/PreschoolConnect.Tests/PreschoolConnect.Tests.csproj --nologo -v minimal
if ($LASTEXITCODE -ne 0) {
    throw ".NET tests failed with exit code $LASTEXITCODE."
}

Write-Host 'Running frontend tests...'
Set-Location "$repoRoot/client"
npm test
if ($LASTEXITCODE -ne 0) {
    throw "Frontend tests failed with exit code $LASTEXITCODE."
}

Write-Host 'Running frontend build...'
npm run build
if ($LASTEXITCODE -ne 0) {
    throw "Frontend build failed with exit code $LASTEXITCODE."
}

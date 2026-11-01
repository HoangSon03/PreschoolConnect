[CmdletBinding()]
param(
    [switch]$NoOpen
)

$ErrorActionPreference = 'Stop'
$PinnedVersion = '0.9.100'
$PinnedPackage = "graphifyy==$PinnedVersion"
$ScriptDirectory = (Resolve-Path $PSScriptRoot).Path
$RepoRoot = (Resolve-Path (Join-Path $ScriptDirectory '..')).Path
Set-Location $RepoRoot

function Refresh-ProcessPath {
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    $env:PATH = (@($userPath, $machinePath, $env:PATH) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join [System.IO.Path]::PathSeparator
}

function Get-GraphifyCommand {
    return Get-Command graphify -CommandType Application -ErrorAction SilentlyContinue
}

function Invoke-Graphify {
    $command = Get-GraphifyCommand
    if ($null -eq $command) {
        throw 'Graphify executable was not found on PATH after installation.'
    }

    & $command.Source @args
    if ($LASTEXITCODE -ne 0) {
        throw "Graphify failed with exit code $LASTEXITCODE."
    }
}

Refresh-ProcessPath
$uv = Get-Command uv -CommandType Application -ErrorAction SilentlyContinue
if ($null -eq $uv) {
    $winget = Get-Command winget -CommandType Application -ErrorAction SilentlyContinue
    if ($null -eq $winget) {
        throw "uv is missing and WinGet is unavailable. Install uv using the official instructions at https://docs.astral.sh/uv/, restart PowerShell, and rerun this script."
    }

    Write-Host 'uv was not found. Installing astral-sh.uv with WinGet...'
    & $winget.Source install --id astral-sh.uv --exact --source winget --accept-source-agreements --accept-package-agreements
    if ($LASTEXITCODE -ne 0) {
        throw "WinGet could not install astral-sh.uv (exit code $LASTEXITCODE). Install uv using https://docs.astral.sh/uv/ and rerun this script."
    }

    Refresh-ProcessPath
    $uv = Get-Command uv -CommandType Application -ErrorAction SilentlyContinue
    if ($null -eq $uv) {
        throw 'WinGet installed uv, but this PowerShell session cannot find it. Close and reopen the terminal, then rerun this script.'
    }
}

Write-Host "Using uv at $($uv.Source)"
& $uv.Source python install 3.12
if ($LASTEXITCODE -ne 0) {
    throw "uv could not install its managed Python 3.12 runtime (exit code $LASTEXITCODE)."
}

$uvToolBinDirectory = (& $uv.Source tool dir --bin).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($uvToolBinDirectory)) {
    throw 'Could not discover uv''s tool executable directory.'
}
if (-not (Test-Path -LiteralPath $uvToolBinDirectory -PathType Container)) {
    throw "uv reported a tool executable directory that does not exist: $uvToolBinDirectory"
}
$env:PATH = "$uvToolBinDirectory$([System.IO.Path]::PathSeparator)$env:PATH"

$graphify = Get-GraphifyCommand
$needsInstall = $true
if ($null -ne $graphify) {
    $versionOutput = & $graphify.Source --version
    if ($LASTEXITCODE -ne 0) {
        throw "Could not verify Graphify at $($graphify.Source)."
    }
    if ($versionOutput -match "(?<!\d)$([regex]::Escape($PinnedVersion))(?!\d)") {
        $needsInstall = $false
    }
}

if ($needsInstall) {
    Write-Host "Installing pinned Graphify package $PinnedPackage with uv..."
    & $uv.Source tool install --force --python 3.12 $PinnedPackage
    if ($LASTEXITCODE -ne 0) {
        throw "uv could not install $PinnedPackage (exit code $LASTEXITCODE)."
    }
}

$graphify = Get-GraphifyCommand
if ($null -eq $graphify) {
    throw "Graphify $PinnedVersion was installed, but its executable is not available in this session. Run 'uv tool update-shell', close and reopen PowerShell, then rerun this script."
}
$versionOutput = & $graphify.Source --version
if ($LASTEXITCODE -ne 0 -or $versionOutput -notmatch "(?<!\d)$([regex]::Escape($PinnedVersion))(?!\d)") {
    throw "Expected Graphify $PinnedVersion, but found '$versionOutput' at $($graphify.Source)."
}
Write-Host "Using Graphify $PinnedVersion at $($graphify.Source)"

$skillPath = Join-Path $RepoRoot '.github\skills\graphify\SKILL.md'
$skillVersionPath = Join-Path $RepoRoot '.github\skills\graphify\.graphify_version'
if (-not (Test-Path -LiteralPath $skillPath -PathType Leaf)) {
    throw "Project Graphify skill is missing: $skillPath"
}
if (-not (Test-Path -LiteralPath $skillVersionPath -PathType Leaf)) {
    throw "Project Graphify skill version stamp is missing: $skillVersionPath"
}
$skillReferences = @(
    'add-watch.md',
    'exports.md',
    'extraction-spec.md',
    'github-and-merge.md',
    'hooks.md',
    'query.md',
    'transcribe.md',
    'update.md'
)
foreach ($reference in $skillReferences) {
    $referencePath = Join-Path (Join-Path $RepoRoot '.github\skills\graphify\references') $reference
    if (-not (Test-Path -LiteralPath $referencePath -PathType Leaf)) {
        throw "Project Graphify skill reference is missing: $referencePath"
    }
}
$skillVersion = (Get-Content -LiteralPath $skillVersionPath -Raw).Trim()
if ($skillVersion -ne $PinnedVersion) {
    throw "Project Graphify skill is version '$skillVersion'; expected '$PinnedVersion'."
}
Write-Host "Project Graphify skill verified at $skillPath"

$ignorePath = Join-Path $RepoRoot '.graphifyignore'
$requiredIgnoreRules = @(
    '.env',
    '.env.*',
    '**/.env',
    '**/.env.*',
    'appsettings*.json',
    '**/appsettings*.json',
    '**/secrets/**',
    '.vscode/mcp.json',
    '**/.vscode/mcp.json',
    '.agents/skills/',
    '**/.agents/skills/',
    '.github/skills/',
    '**/.github/skills/',
    '*.pem',
    '*.key',
    '*.pfx',
    '*.p12',
    '*.crt',
    '*.cer',
    '.postgres/',
    '**/.postgres/',
    '*.db',
    '*.sqlite',
    '*.sqlite3',
    '*.bak',
    '*.backup',
    '*.dump',
    '*.log',
    'node_modules/',
    '**/node_modules/',
    'bin/',
    '**/bin/',
    'obj/',
    '**/obj/',
    'dist/',
    '**/dist/',
    'build/',
    '**/build/',
    'publish/',
    '**/publish/',
    'TestResults/',
    '**/TestResults/',
    'graphify-out/'
)
if (-not (Test-Path -LiteralPath $ignorePath -PathType Leaf)) {
    throw "Graphify privacy exclusions are missing: $ignorePath"
}
$ignoreRules = @(Get-Content -LiteralPath $ignorePath | ForEach-Object { $_.Trim() })
$missingRules = @($requiredIgnoreRules | Where-Object { $ignoreRules -notcontains $_ })
if ($missingRules.Count -gt 0) {
    throw "Required Graphify privacy exclusions are missing ($($missingRules.Count) rules). Update .graphifyignore before building."
}
$uv = Get-Command uv -ErrorAction SilentlyContinue
if ($null -eq $uv) {
    throw 'uv is required to validate the installed Graphify ignore matcher.'
}
$toolDirectory = (& $uv.Source tool dir).Trim()
if ($LASTEXITCODE -ne 0) {
    throw 'Could not locate the uv tool directory.'
}
$toolPython = Join-Path $toolDirectory 'graphifyy\Scripts\python.exe'
if (-not (Test-Path -LiteralPath $toolPython -PathType Leaf)) {
    throw "Could not find Graphify's Python runtime at $toolPython."
}
$privacyProbe = @'
from pathlib import Path
import sys
import tempfile
import uuid
from graphify.detect import _is_ignored, _load_graphifyignore

root = Path(sys.argv[1]).resolve()
patterns = _load_graphifyignore(root)
private_files = (
    ".env", ".env.local", "server/appsettings.Development.json",
    ".vscode/mcp.json", "certs/private.pem", "certs/signing.key",
    "certs/app.pfx", "data/local.db", "data/local.sqlite",
    "data/backup.bak", "logs/app.log",
)
unexpected = [p for p in private_files if not _is_ignored(root / p, root, patterns)]
source_paths = ("server/PreschoolConnect.Api/Program.cs", "client/src/main.tsx")
unexpected.extend(p for p in source_paths if _is_ignored(root / p, root, patterns))
directory_cases = (
    ".postgres", "node_modules", "server/bin", "server/obj", "client/dist",
    "client/build", "server/publish", "server/TestResults",
    ".agents/skills", ".github/skills",
)
with tempfile.TemporaryDirectory(prefix=".graphify-ignore-probe-", dir=root) as temp:
    fixture = Path(temp)
    for relative in directory_cases:
        sentinel = fixture / relative / "synthetic.cs"
        sentinel.parent.mkdir(parents=True, exist_ok=True)
        sentinel.write_text("class SyntheticProbe {}", encoding="utf-8")
        if not _is_ignored(sentinel, root, patterns):
            unexpected.append(relative + "/synthetic.cs")

    graph_dir = root / "graphify-out"
    graph_dir.mkdir(exist_ok=True)
    graph_sentinel = graph_dir / (".privacy-probe-" + uuid.uuid4().hex + ".cs")
    try:
        graph_sentinel.write_text("class SyntheticProbe {}", encoding="utf-8")
        if not _is_ignored(graph_sentinel, root, patterns):
            unexpected.append("graphify-out/" + graph_sentinel.name)
    finally:
        graph_sentinel.unlink(missing_ok=True)

if unexpected:
    print("Graphify privacy exclusion check failed for representative paths: " + ", ".join(unexpected))
    raise SystemExit(1)
print(f"Graphify ignore matcher verified: {len(private_files) + len(directory_cases) + 1} private/build path cases excluded; source paths remain indexable.")
'@
$privacyProbePath = Join-Path ([System.IO.Path]::GetTempPath()) ("graphify-ignore-$([guid]::NewGuid().ToString('N')).py")
$utf8NoBom = New-Object System.Text.UTF8Encoding -ArgumentList $false
try {
    [System.IO.File]::WriteAllText($privacyProbePath, $privacyProbe, $utf8NoBom)
    & $toolPython $privacyProbePath $RepoRoot
    if ($LASTEXITCODE -ne 0) {
        throw 'Graphify ignore matcher verification failed.'
    }
}
finally {
    if (Test-Path -LiteralPath $privacyProbePath) {
        Remove-Item -LiteralPath $privacyProbePath -Force
    }
}

$graphOutput = Join-Path $RepoRoot 'graphify-out'
$artifacts = @(
    (Join-Path $graphOutput 'graph.html'),
    (Join-Path $graphOutput 'GRAPH_REPORT.md'),
    (Join-Path $graphOutput 'graph.json')
)
$env:GRAPHIFY_BACKEND = 'none'
$env:GRAPHIFY_NO_TIPS = '1'
Write-Host 'Generating local code-only graph (no semantic extraction, LLM backend disabled)...'
Invoke-Graphify extract . --code-only
Write-Host 'Clustering locally and generating the report and HTML without LLM labels...'
Invoke-Graphify cluster-only . --no-label

foreach ($artifact in $artifacts) {
    if (-not (Test-Path -LiteralPath $artifact -PathType Leaf)) {
        throw "Graphify did not create required artifact: $artifact"
    }
    if ((Get-Item -LiteralPath $artifact).Length -le 0) {
        throw "Graphify created an empty artifact: $artifact"
    }
}

$htmlPath = (Resolve-Path (Join-Path $graphOutput 'graph.html')).Path
Write-Host "Graph artifacts verified under $graphOutput"
if (-not $NoOpen) {
    try {
        Start-Process -FilePath $htmlPath
        Write-Host "Requested the default browser to open: $htmlPath"
    }
    catch {
        throw "Could not open the graph in the default browser: $($_.Exception.Message)"
    }
}
else {
    Write-Host "Browser launch skipped (-NoOpen). Visualization: $htmlPath"
}

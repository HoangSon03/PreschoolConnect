# Graphify setup

Graphify builds a local, code-only architecture graph for repository exploration.
The checked-in VS Code Copilot skill is under `.github/skills/graphify/`, and
the Graphify CLI version is pinned to `0.9.100`.

## Prerequisites

- Windows PowerShell 5.1 or later and VS Code.
- The project Graphify skill and `.graphifyignore` checked out with the repository.

The project Skill is checked in at `.github/skills/graphify/`; it provides
Copilot guidance but does not install the Graphify CLI. The setup script checks
for `uv` and, if missing, installs it with the official WinGet package
`astral-sh.uv`. It then installs uv-managed Python 3.12 and
`graphifyy==0.9.100` when needed. If WinGet is unavailable, install `uv` using
the official instructions at <https://docs.astral.sh/uv/> and rerun the script.
The script discovers executable locations dynamically and does not rely on a
particular user profile or repository path.

## Build and open the graph

After cloning the repository and opening it in VS Code, run this in the
PowerShell terminal:

```powershell
.\scripts\setup-graphify.ps1
```

The script resolves the repository root from its own location, so it can also
be invoked by absolute or relative path from another working directory. Paths
containing spaces are supported.

If the machine's PowerShell policy blocks unsigned local scripts, use a
process-scoped policy for this invocation; this does not change the persistent
execution policy:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\setup-graphify.ps1
```

To generate or refresh the graph without opening a browser:

```powershell
.\scripts\setup-graphify.ps1 -NoOpen
```

The script validates the privacy ignore rules, sets `GRAPHIFY_BACKEND=none`,
and runs:

```powershell
graphify extract . --code-only
graphify cluster-only . --no-label
```

The first command performs local AST extraction only; the second runs local
clustering and generates the report and interactive HTML without LLM labels.
Neither command semantically processes documentation or media or accesses
PostgreSQL. The script does not install MCP servers, configure hooks, or enable
file watchers. The generated team-facing artifacts are:

- `graphify-out/graph.html` - interactive visualization.
- `graphify-out/GRAPH_REPORT.md` - graph summary and central nodes.
- `graphify-out/graph.json` - queryable graph data.

These three portable outputs are intentionally tracked in Git as a shared
snapshot. A fresh clone can open `graphify-out/graph.html` immediately without
installing Graphify or rebuilding the graph. Run the setup script when you want
to refresh the snapshot after code changes. Machine-local Graphify state files
inside `graphify-out/` remain ignored; do not force-add them.

## Privacy exclusions

`.graphifyignore` excludes environment files, appsettings and secret files,
`.vscode/mcp.json`, private keys/certificates, database files and backups, local
PostgreSQL runtime files, logs, dependency directories, build outputs, test
results, and generated Graphify output. The setup script checks both the rules
and Graphify 0.9.100's ignore matcher before extraction. It prints no file
contents or secret values.

Graphify honors `.graphifyignore` and `.gitignore` with gitignore-style rules;
`.graphifyignore` has higher precedence. Keep exclusions current whenever local
configuration or generated output locations change.

## Troubleshooting

- If setup cannot install `uv` because WinGet is unavailable, install it using
  the official instructions at <https://docs.astral.sh/uv/> and restart
  PowerShell.
- If WinGet installed `uv` but this terminal cannot find it, close and reopen
  the VS Code terminal and rerun the setup script.
- If Graphify is not available after installation, run `uv tool update-shell`,
  close and reopen the VS Code terminal, and rerun the setup script.
- If an artifact is missing or empty, the script fails instead of reporting a
  successful build. Review the Graphify error output and verify the installed
  CLI version and ignore rules.
- Use `-NoOpen` when running in a non-interactive environment or when you prefer
  to open `graphify-out/graph.html` yourself.

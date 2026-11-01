# Figma MCP integration

## Status

The workspace-level remote server configuration is present. OAuth has not yet
been completed, and Figma tools have not been verified in Copilot. No Figma
design files have been read or changed as part of this setup.

## Endpoint and configuration

The official remote Figma MCP endpoint is `https://mcp.figma.com/mcp`. The
workspace configuration is [`.vscode/mcp.json`](../../.vscode/mcp.json):

```json
{
  "servers": {
    "figma": {
      "type": "http",
      "url": "https://mcp.figma.com/mcp"
    }
  }
}
```

VS Code 1.143.1 supports this HTTP server configuration. Current VS Code
documentation describes `.vscode/mcp.json` as a compatibility location and
labels it deprecated in the guided setup flow; a workspace-root `.mcp.json`
is the newer shared-workspace option when its feature is enabled. This project
keeps only the requested `.vscode/mcp.json` file and does not create a second
configuration.

## Start the server and authorize OAuth

1. Open this workspace in VS Code and make sure GitHub Copilot Chat is
   installed, signed in, and set to **Agent** mode.
2. Open the Command Palette (`Ctrl+Shift+P`) and run **MCP: List Servers**.
   Select `figma` and choose **Start Server** (or the equivalent start action
   shown by the installed VS Code version).
3. When VS Code opens Figma's OAuth sign-in, sign in directly with Figma and
   review/approve the requested access there. Do not paste passwords, access
   tokens, or authorization codes into chat or project files.
4. In Copilot Agent chat, confirm the `figma` server tools appear. Figma's
   official guide suggests searching for `#get_design_context`; if the tools
   are absent after authorization, reload or restart VS Code and check the
   server output.

The `code --list-extensions --show-versions` inspection for this environment
listed `anthropic.claude-code` but no GitHub Copilot extension. The workspace
configuration alone does not install or sign in to Copilot. Verify the active
VS Code profile has GitHub Copilot Chat before expecting Copilot Agent to use
this server.

## Capabilities and permissions

The official remote server documents design-context retrieval
(`get_design_context`), screenshots (`get_screenshot`), metadata/structure,
variables, and Code Connect workflows. The remote server also documents
`use_figma` for inspecting and creating or modifying native content on a Figma
canvas. The tools actually available depend on successful OAuth, the Figma
account, file access, and server rollout; this workspace has not yet verified
the live tool list.

Read-only design-context retrieval requires an authorized Figma account with
access to the target file. A harmless connectivity test needs a Figma file or
frame link that the signed-in account can access; use a read-only request such
as `get_design_context` or `get_screenshot`. Do not test by writing to a file.

Writing is supported by the remote server, but it changes native Figma content.
Use it only when explicitly requested and after confirming the target file and
intended changes. Do not send real student, child, or parent data to Figma or
include it in MCP prompts; use synthetic examples only. This setup task did not
authorize or perform any design write.

Figma's guide currently describes write-to-canvas as a beta feature and says
it is free during the beta; availability and pricing may change. The guide
reports up to six read-tool calls per month for Starter users and View/Collab
seats on paid plans; Dev/Full seats on Professional, Organization, or
Enterprise use per-minute limits aligned with Figma REST API Tier 1. Check
Figma's current plan, seat, and rate-limit terms for the signed-in account.
Use of the server and related skills is subject to the
[Figma Developer Terms](https://www.figma.com/legal/developer-terms/).

## Official Figma skills

VS Code supports the open Agent Skills standard used by GitHub Copilot. Figma's
[`mcp-server-guide`](https://github.com/figma/mcp-server-guide) repository
contains official skills including `figma-design-to-code` and `figma-use`;
the repository marks these skills as beta. The VS Code guide lists a Figma
plugin for other clients such as Cursor and Claude Code, but its VS Code setup
is manual MCP configuration rather than a dedicated Figma Copilot plugin.

The `figma-design-to-code` skill was inspected: it guides design-context and
screenshot retrieval and implementation from a Figma frame. The `figma-use`
skill was inspected: it is a prerequisite for the remote `use_figma` tool,
including operations that create or modify Figma content. These skills are not
copied into this workspace: this task is integration setup only, and the
write-oriented workflow is unnecessary until requested. For a future
design-to-code task, consider adding the relevant official skill after
reviewing its complete skill directory and Figma Developer Terms.

## Troubleshooting

- If `figma` is absent from **MCP: List Servers**, reopen the correct workspace
  and check `.vscode/mcp.json` for valid JSON.
- If startup reports an authentication requirement, use VS Code's Figma OAuth
  sign-in; do not put credentials in the configuration.
- If OAuth succeeds but tools are missing, restart VS Code, confirm Copilot
  Chat is signed in and in Agent mode, then inspect the Figma server output.
- A read test can fail if the signed-in Figma account cannot access the
  supplied file or if the account's plan/seat limits have been reached.
- Do not share private Figma links, designs, tokens, or server logs containing
  sensitive information.

## References

- [Official Figma MCP Server Guide](https://github.com/figma/mcp-server-guide)
- [Figma MCP Server documentation](https://developers.figma.com/docs/figma-mcp-server/)
- [VS Code MCP server configuration](https://code.visualstudio.com/docs/agent-customization/mcp-servers)
- [VS Code Agent Skills](https://code.visualstudio.com/docs/agent-customization/agent-skills)

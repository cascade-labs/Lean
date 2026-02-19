---
name: setup-mcp-proxy
description: "Configure project MCP tools to route through Apropos observability proxy."
---

# Setup MCP Proxy

Use this skill when a user wants an MCP configured in this project with Apropos proxy visibility.

## Goals

1. Ensure MCP tool config is present in:
- `.mcp.json` for Claude
- `.codex/config.toml` for Codex
2. Route MCP interactions through:
- `http://127.0.0.1:4311/api/proxy/codex`
- `http://127.0.0.1:4311/api/proxy/claude`
3. Verify basic connectivity and report changes.

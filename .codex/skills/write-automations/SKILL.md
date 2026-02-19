---
name: write-automations
description: "Create and update .automations JSON workflows for Apropos."
---

# Write Automations

Use this skill when a user asks to create or edit project automations.

## Goal

Create JSON automation files in `.automations/` that Apropos can run.

## File format

Each automation file must be valid JSON with:
- `name`: string
- `sessions`: array with at least one item

Each session item supports:
- `kind`: one of `tmux`, `codex`, `claude`
- `command`: optional string

## Steps

1. Ask for the workflow intent if unclear.
2. Propose a short automation file name (kebab-case).
3. Build a valid JSON payload with practical session order.
4. Save the file under `.automations/<name>.json`.
5. Validate JSON syntax before finishing.

## Example

```json
{
  "name": "default-workspace",
  "sessions": [
    { "kind": "tmux" },
    { "kind": "codex" },
    { "kind": "claude" },
    { "kind": "tmux", "command": "npm run dev" }
  ]
}
```

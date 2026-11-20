---
extends: positions/selectors
summary: The grammar of --positions - kinds, id globs, quoted ids, typed predicates - and shell quoting.
examples:
  - name: union
    kind: command
    language: shell
    content: "canvasnet render flowchart.mmd --positions node:send --positions 'edge:@from=stock' -o flow.png --json"
    description: The Send node and every edge that leaves the stock check, in one list.
---

## Command line

Each `--positions` takes one selector; repeat the option for more. A bare value
reads as `--param` reads one, and a malformed selector is usage-error.

## Quoting

Quote a selector that has `*`, `?`, `"`, a space, or `&`:

- bash and zsh: single quotes, `'node:"my*odd id"'`.
- PowerShell 7.3 or later: single quotes, as in bash. Windows PowerShell 5.1
  drops the inner double quotes, so use 7.3 or later.
- cmd: no single quotes; double quotes with `\"` inside,
  `"node:\"my*odd id\""`.

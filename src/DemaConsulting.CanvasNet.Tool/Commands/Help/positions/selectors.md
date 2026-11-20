---
extends: positions/selectors
examples:
  - name: union
    kind: command
    language: shell
    content: "canvasnet render flowchart.mmd --positions node:send --positions 'edge:@from=stock' -o flow.png --json"
    description: The Send node and every edge that leaves the stock check, in one list.
---

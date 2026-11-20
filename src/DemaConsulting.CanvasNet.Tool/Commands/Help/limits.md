---
extends: limits
examples:
  - name: formats
    kind: command
    language: shell
    content: 'canvasnet formats --json'
    description: Print the limits of the installed tool as JSON.
  - name: too-large
    kind: command
    language: shell
    content: 'canvasnet render big.svg --width 9000 -o big.png'
    description: A width above the limit fails with size-limit before any pixels are allocated.
---

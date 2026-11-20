---
extends: errors
see-also:
  - json
examples:
  - name: missing-input
    kind: command
    language: shell
    content: 'canvasnet render missing.png -o out.png --json'
    description: A failure that exits with 1 and reports input-not-found as JSON and on standard error.
  - name: strict-gate
    kind: command
    language: shell
    content: 'canvasnet render bar.vl.json --strict -o out.png --json'
    description: Turn every unsupported-feature diagnostic of a chart into a failure.
---

---
extends: mermaid/options
examples:
  - name: render
    kind: command
    language: shell
    content: 'canvasnet render flowchart.mmd -o flowchart.png --json'
    description: Draw flowchart.mmd and print the JSON result.
  - name: info
    kind: command
    language: shell
    content: 'canvasnet info flowchart.mmd --json'
    description: Report the type, direction, and counts without drawing.
---

---
extends: plantuml/options
examples:
  - name: render
    kind: command
    language: shell
    content: 'canvasnet render sequence.puml -o sequence.png --json'
    description: Draw sequence.puml and print the JSON result.
  - name: info
    kind: command
    language: shell
    content: 'canvasnet info sequence.puml --json'
    description: Report the type, title, and counts without drawing.
---

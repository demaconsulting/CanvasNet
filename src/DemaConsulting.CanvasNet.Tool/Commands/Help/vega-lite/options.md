---
extends: vega-lite/options
examples:
  - name: render
    kind: command
    language: shell
    content: 'canvasnet render bar.vl.json -o bar.png --json'
    description: Draw bar.vl.json and print the JSON result.
  - name: scale-background
    kind: command
    language: shell
    content: 'canvasnet render bar.vl.json --scale 2 --background "#f4f4f4" -o bar-large.png'
    description: Draw at twice the size over a light gray background.
  - name: info
    kind: command
    language: shell
    content: 'canvasnet info bar.vl.json --json'
    description: Report the laid-out size, marks, and data references without drawing.
  - name: strict-check
    kind: command
    language: shell
    content: 'canvasnet render bar.vl.json --strict -o bar.png'
    description: Fail instead of leaving an unsupported construct out.
---

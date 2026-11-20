---
extends: style
examples:
  - name: check
    kind: command
    language: shell
    content: 'canvasnet render chart.vl.json --scale 2 -o chart.png --strict --json'
    description: Draw at twice the natural size, fail if anything is left out, and print the result to check.
  - name: colors
    kind: command
    language: shell
    content: 'canvasnet inspect colors chart.png --count 6'
    description: List the dominant colors of a rendered picture, to check its palette and background.
---

---
extends: svg/composition
examples:
  - name: render-report
    kind: command
    language: shell
    content: 'canvasnet render report.svg --width 1200 -o report.png --strict --json'
    description: Draw the figure 1200 pixels wide and fail if a picture is not drawn.
---

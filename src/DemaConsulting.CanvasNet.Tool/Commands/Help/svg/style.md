---
extends: svg/style
examples:
  - name: render-page
    kind: command
    language: shell
    content: 'canvasnet render page.svg --width 1200 -o page.png --strict --json'
    description: Draw a drawing 1200 pixels wide, sharply, and fail if a linked picture is not drawn.
---

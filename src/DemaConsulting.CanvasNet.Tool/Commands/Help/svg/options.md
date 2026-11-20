---
extends: svg/options
examples:
  - name: render-width
    kind: command
    language: shell
    content: 'canvasnet render shapes.svg --width 480 -o shapes.png'
    description: Draw shapes.svg 480 pixels wide.
  - name: transparent
    kind: command
    language: shell
    content: 'canvasnet render shapes.svg --background transparent -o shapes-alpha.png'
    description: Keep the transparency of the drawing.
  - name: info
    kind: command
    language: shell
    content: 'canvasnet info shapes.svg --json'
    description: Report the intrinsic size without drawing.
---

---
extends: scad/options
examples:
  - name: define-width
    kind: command
    language: shell
    content: 'canvasnet render posts.scad -D width=30 -o posts.png'
    description: Draw posts.scad with the variable width set to 30.
  - name: strict-check
    kind: command
    language: shell
    content: 'canvasnet render plate.scad --strict -o plate.png --json'
    description: Fail instead of leaving an unsupported construct out.
---

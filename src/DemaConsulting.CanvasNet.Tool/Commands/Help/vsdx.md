---
extends: vsdx
examples:
  - name: render
    kind: command
    language: shell
    content: 'canvasnet render diagram.vsdx --dpi 150 -o diagram.png --json'
    description: Draw the first page at 150 dpi and print the JSON result.
  - name: info-pages
    kind: command
    language: shell
    content: 'canvasnet info diagram.vsdx --all-pages --json'
    description: List every page with its name and size as JSON.
---

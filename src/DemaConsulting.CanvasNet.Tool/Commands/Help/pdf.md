---
extends: pdf
examples:
  - name: info-pages
    kind: command
    language: shell
    content: 'canvasnet info report.pdf --all-pages --json'
    description: List every page with its size and rotation as JSON.
  - name: render-page
    kind: command
    language: shell
    content: 'canvasnet render report.pdf --page 2 --dpi 150 -o page2.png --json'
    description: Draw page 2 at 150 dpi and print the JSON result.
  - name: overview
    kind: command
    language: shell
    content: 'canvasnet montage report.pdf --all-pages --labels -o overview.png'
    description: Compose every page into one labeled contact sheet.
---

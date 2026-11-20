---
extends: composition
examples:
  - name: themes
    kind: command
    language: shell
    content: 'canvasnet montage "flow.mmd#theme=default" "flow.mmd#theme=forest" "flow.mmd#theme=neutral" --labels --cell 480x240 -o themes.png'
    description: Three themes of one diagram side by side, each chosen with the fragment of its input.
  - name: publish
    kind: command
    language: shell
    content: 'canvasnet flatten figure.svg -o figure.flat.svg'
    description: Write a copy of a figure whose diagrams, charts, models, and pages are PNG pictures, for any SVG reader.
---

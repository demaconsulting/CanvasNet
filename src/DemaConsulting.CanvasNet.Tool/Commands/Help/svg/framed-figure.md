---
extends: svg/framed-figure
examples:
  - name: read-anchors
    kind: command
    language: shell
    content: 'canvasnet render anchors.scad --size 440x300 -o slot.png --json'
    description: Read the anchors of the model at the size of its image; add the image x and y to each anchors[].pixel.
  - name: read-positions
    kind: command
    language: shell
    content: 'canvasnet render flowchart.mmd --size 430x280 --positions node:send --positions node:stock -o slot.png --json'
    description: Read the positions of two nodes of the diagram at the size of its image; add the image x and y.
  - name: render-framed
    kind: command
    language: shell
    content: 'canvasnet render framed.svg --width 1200 -o framed.png --strict --json'
    description: Draw the framed figure 1200 pixels wide and fail if the model is not drawn.
---

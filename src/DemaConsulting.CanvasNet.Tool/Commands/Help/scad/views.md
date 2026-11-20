---
extends: scad/views
examples:
  - name: render-top
    kind: command
    language: shell
    content: 'canvasnet render plate.scad --view top --size 400x400 -o plate-top.png --json'
    description: Draw plate.scad from above at 400x400 and print the JSON result.
  - name: free-view
    kind: command
    language: shell
    content: 'canvasnet render plate.scad --azimuth 30 --elevation 20 --projection perspective -o plate.png'
    description: Draw plate.scad from a free viewpoint with a perspective camera.
---

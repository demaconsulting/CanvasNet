---
extends: scad/composition
examples:
  - name: montage-variants
    kind: command
    language: shell
    content: 'canvasnet montage "posts.scad#def.width=10" "posts.scad#def.width=20" "posts.scad#def.width=30" -D ''$vpd=400'' --labels -o variants.png'
    description: Three versions of posts.scad side by side, chosen with the fragment of each input and drawn from one fixed camera (quote each input; the labels show the fragments).
---

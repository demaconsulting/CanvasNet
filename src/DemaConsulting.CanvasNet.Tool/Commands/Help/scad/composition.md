---
extends: scad/composition
see-also:
  - pandoc
examples:
  - name: montage-variants
    kind: command
    language: shell
    content: 'canvasnet montage "posts.scad#def.width=10" "posts.scad#def.width=20" "posts.scad#def.width=30" -D ''$vpd=400'' --labels -o variants.png'
    description: Three versions of posts.scad side by side, chosen with the fragment of each input and drawn from one fixed camera (quote each input; the labels show the fragments).
---

## Command line

- Step 2: `canvasnet render part.scad --size 600x400 --background transparent
  -o part.png --json`. Each entry of `anchors[]` gives the `pixel` [x, y] of a
  point in `part.png`; `--crop` and `--trim` move the anchors with the picture.
- Variants: give each cell of a montage its own fragment, and fix the camera
  with `-D '$vpd=...'`.
- Pandoc: a .scad picture, and a code block of class openscad that also has
  the class draw ({.openscad .draw view=iso}), are drawn by the filter; a
  plain openscad block stays code. See [[pandoc]].

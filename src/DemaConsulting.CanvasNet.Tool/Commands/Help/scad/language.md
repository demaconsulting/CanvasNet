---
extends: scad/language
---

## Command line

- `--strict`: Fails on a construct in Unsupported instead of leaving it out.
- `--default-color black` and `--background` (white by default, transparent
  keeps alpha) give a stronger 2D drawing.
- -D name=value overrides a variable (-D "$vpt=[0,0,20]"), as does the fragment
  of an input path: canvasnet render "part.scad#def.width=20" -o part.png. -D
  values that are expressions are not accepted.
- `--time`: Sets $t; run the command once per view.

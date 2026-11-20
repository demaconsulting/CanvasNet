---
title: Render options of a model
summary: Variables from the command line, time, default color, triangle budget, background, and --strict.
tags:
  - scad
  - hosting
see-also:
  - scad/views
  - limits
---

## Options

- `--define <name=value>`: Set a variable of the model; -D is the short form.
  Repeatable; the later definition wins. The value is a number, true, false,
  text, or a JSON array for a vector. A def.<name>=<value> in the fragment of
  the input (part.scad#def.width=20) wins over it for that input.
- `--time <T>`: The value of $t (default 0).
- `--default-color <color>`: The color of surfaces the model does not color
  (default gray); the alpha is ignored. A flat 2D drawing is gray by default, so
  --default-color black gives a high-contrast drawing; a color() in the model
  overrides it.
- `--max-triangles <N>`: The most triangles the model may produce (default
  {{scadMaxTriangles}}).
- `--background <color>`: The color the picture is flattened over (default
  white; black, #RRGGBB, or #RRGGBBAA); transparent keeps alpha.
- `--strict`: Fail with unsupported-feature instead of leaving a construct out.

---
title: Render options of a model
summary: Variables, time, default color, triangle budget, background, and strict mode of a model.
tags:
  - scad
  - hosting
see-also:
  - scad/views
  - limits
---

The host sets these in a `ScadViewOptions` (`ViewerRenderOptions.Scad` or
`SourceOptions.Scad`), and the background and strict mode in the render options;
the camera is in [[scad/views]].

## Options

- `Defines`: Set variables of the model by name; the value is a number, true,
  false, text, or a list for a vector. A def.<name>=<value> in the fragment of a
  link or an input (part.scad#def.width=20) wins over it for that input.
- `Time`: The value of $t (default 0).
- `DefaultColor`: The color of surfaces the model does not color (default gray);
  the alpha is ignored. A flat 2D drawing is gray by default, so black gives a
  high-contrast drawing; a color() in the model overrides it.
- `MaxTriangles`: The most triangles the model may produce (default
  {{scadMaxTriangles}}).
- `Background`: The color the picture is flattened over (white by
  default); a transparent color keeps alpha.
- `ThrowOnUnsupportedFeature`: Strict mode (`SourceOptions.Strict`): fail with
  unsupported-feature instead of leaving a construct out.

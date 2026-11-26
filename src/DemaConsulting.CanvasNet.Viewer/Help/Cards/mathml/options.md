---
title: Render options of an equation
summary: Scale, size, background, display mode, strict mode, limits, and what a probe reports.
tags:
  - mathml
  - hosting
see-also:
  - mathml/diagnostics
  - mathml/style
  - limits
---

The host chooses these when it renders an equation: the properties of
`ViewerRenderOptions` for `CanvasNetViewer.Render`, or of `SourceOptions` for
the operations of the Imaging namespace.

## Options

- `Scale`: The natural size is the equation at 16 pixels per em (the default
  font size of a browser); a scale factor lays it out again at that multiple, so
  it stays sharp (an equation larger than {{maxDimension}} pixels on a side at its natural
  size is refused).
- `Width` and `Height`: Draw the equation into a box of that size, scaled
  uniformly and centered; one side alone keeps the aspect ratio.
- `Background`: The color under the equation: white by default when it is drawn
  alone, transparent when it is placed in another picture; a transparent color
  gives a transparent picture.
- `ThrowOnUnsupportedFeature`: Strict mode (`SourceOptions.Strict`): the first
  element, attribute, or style declaration that is not drawn becomes an
  unsupported-feature error; warnings stay diagnostics.
- `MathMlLimits`: The resource limits of the equation
  (`ViewerRenderOptions.MathMlLimits`), lower or higher than the defaults, for
  the rendered file and every equation it embeds; raise one only for trusted
  content (see [[limits]]).
- Display: `SourceOptions.MathMl` (an `EquationOptions` with `Display =
  MathMlDisplay.Block` or `Inline`) and the `#display=` fragment of an input or
  a link replace the `display` attribute of the `math` element.
- Theme and positions: An equation has neither; the Mermaid theme
  (`DiagramOptions`) and the position selectors do not apply to it, and it reads
  no file, so the file-access policy has nothing to judge.

## Supported

- A probe (`SourceLoader.Probe`, its `SourceInfo.Equation`) reports the display
  mode it laid the equation out in (block or inline), the number of elements,
  the text alternative (`alttext`), the encodings of the annotations, and the
  baseline (the row of the alphabetic baseline in the natural picture, for
  placing the picture in a line of text); the width and height are the natural
  size.

---
title: Visio drawings (.vsdx)
summary: 'Visio drawings: each page drawn as an image.'
tags:
  - vsdx
  - hosting
  - diagnostics
aliases:
  - visio
see-also:
  - svg/fragments
  - limits
---

Each drawing page is a page, drawn as an image.

## Supported

- Pages: `Page` (from 1), and every page with `RenderRequest.AllPages` of the
  Imaging namespace; a probe (`SourceLoader.Probe`) lists the page names in
  `SourceInfo.Pages`. `Dpi` must be a whole number.
- Embedding: `<image href="drawing.vsdx#page=2" .../>` in an SVG file draws page
  2 (page 1 without the fragment); a page that does not exist is
  image-page-out-of-range. See [[svg]]. The same fragment on an
  input path chooses the page (it overrides `Page`).
- Shape trees with Master and MasterShape and StyleSheet inheritance: fills,
  strokes, and text.
- 1-D connectors with arrowheads and glue, and nested groups.
- The built-in color palette, themed colors, and line and fill transparency;
  NonPrinting and HideText are honored.
- PolylineTo, NURBSTo, and spline rows, drawn as flattened line segments.

## Unsupported

- These are left out or approximated and reported; the page is still drawn, and
  strict mode turns the first report into an unsupported-feature error:
- Embedded images and foreign shapes.
- `vsdx-themed-color`: Theme Quick Style variation matrices: a themed color that
  cannot be resolved is drawn neutral gray.
- Non-solid fill patterns and gradients, and the full table of arrowhead styles.
- `vsdx-geometry-row-<Type>`: A malformed geometry row, or a row type such as
  InfiniteLine, is skipped; <Type> names the row type.
- `vsdx-geometry-spline-approximated`: A curve that cannot be evaluated exactly
  is approximated by a polyline.

## Options

The host chooses these when it renders a page: the properties of
`ViewerRenderOptions` for `CanvasNetViewer.Render`, or of `SourceOptions` for
the operations of the Imaging namespace.

- `Page`: The page to draw, counted from 1 (default 1).
- `Dpi`: Resolution in dots per inch (default 96, a whole number above 0 and at
  most {{maxDpi}}).
- `Background`: The color transparency is flattened over (`SourceOptions` uses
  white by default).
- `ThrowOnUnsupportedFeature`: Strict mode: fail with unsupported-feature
  instead of drawing with an approximation.

## Diagnostics

- Each report is a diagnostic (`ViewerImage.Diagnostics`) with the source vsdx,
  the severity unsupported, and the token as its code, once per distinct feature
  per page. In strict mode the first report fails with unsupported-feature.
- `vsdx-themed-color`: A themed color was drawn neutral gray.
- `vsdx-geometry-spline-approximated`: A curve was approximated by a polyline.
- `vsdx-geometry-row-<Type>`: A geometry row was skipped.

## Known differences

- The approximations listed above, in particular neutral gray for themed colors
  that cannot be resolved.
- Fonts that are not embedded come from the machine, so text can differ between
  machines.

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

- Pages: --page N (from 1) and --all-pages; 'canvasnet info' lists the page
  names. --dpi must be a whole number.
- Embedding: `<image href="drawing.vsdx#page=2" .../>` in an SVG file draws page
  2 (page 1 without the fragment); a page that does not exist is
  image-page-out-of-range. See [[svg]]. On the command line the
  same fragment on the input chooses the page: canvasnet render
  "drawing.vsdx#page=2" -o p2.png (it overrides --page).
- Shape trees with Master and MasterShape and StyleSheet inheritance: fills,
  strokes, and text.
- 1-D connectors with arrowheads and glue, and nested groups.
- The built-in color palette, themed colors, and line and fill transparency;
  NonPrinting and HideText are honored.
- PolylineTo, NURBSTo, and spline rows, drawn as flattened line segments.

## Unsupported

- These are left out or approximated and reported; the page is still drawn, and
  --strict turns the first report into an unsupported-feature error:
- Embedded images and foreign shapes.
- `vsdx-themed-color`: Theme Quick Style variation matrices: a themed color that
  cannot be resolved is drawn neutral gray.
- Non-solid fill patterns and gradients, and the full table of arrowhead styles.
- `vsdx-geometry-row-<Type>`: A malformed geometry row, or a row type such as
  InfiniteLine, is skipped; <Type> names the row type.
- `vsdx-geometry-spline-approximated`: A curve that cannot be evaluated exactly
  is approximated by a polyline.

## Options

- `--page <N>`: The page to draw, counted from 1 (default 1).
- `--all-pages`: Draw every page; for a drawing of more than one page the output
  must contain {page} or be a directory.
- `--dpi <D>`: Resolution in dots per inch (default 96, a whole number above 0
  and at most {{maxDpi}}).
- `--background <color>`: The color transparency is flattened over (white by
  default).
- `--strict`: Fail with unsupported-feature instead of drawing with an
  approximation.

## Diagnostics

- Each report is listed in diagnostics[] as {source: vsdx, severity:
  unsupported, code: <token>} (without --json, also as an
  'Unsupported: <token>: <message>' line on standard error), once per distinct
  feature per page. With --strict the first report fails with
  unsupported-feature.
- `vsdx-themed-color`: A themed color was drawn neutral gray.
- `vsdx-geometry-spline-approximated`: A curve was approximated by a polyline.
- `vsdx-geometry-row-<Type>`: A geometry row was skipped.

## Known differences

- The approximations listed above, in particular neutral gray for themed colors
  that cannot be resolved.
- Fonts that are not embedded come from the machine, so text can differ between
  machines.

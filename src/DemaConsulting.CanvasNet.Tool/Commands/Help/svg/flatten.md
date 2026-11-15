---
title: Flattening for other SVG readers
summary: 'canvasnet flatten: a copy of a drawing that browsers and other SVG readers can show.'
see-also:
  - svg/embedding
  - svg/bindings
  - svg/diagnostics
examples:
  - name: flatten
    kind: command
    language: shell
    content: 'canvasnet flatten dashboard.svg -o dashboard-flat.svg --scale 2 --json'
    description: Write a copy of a drawing whose linked diagrams, models, charts, and document pages are PNG pictures twice their placement size, for an SVG reader that cannot draw them.
---

A browser or WeasyPrint cannot draw a linked `.mmd`, `.scad`, `.vl.json`, or
document page. Run `canvasnet flatten page.svg -o page.flat.svg` to replace each
such link with a PNG drawn at its placement.

## Reference

- replaced: An `image` whose link names a file only CanvasNet draws: a Mermaid
  diagram, an OpenSCAD model, a Vega-Lite chart, or a page of a PDF, PPTX, or
  VSDX document, with or without a fragment (`#page=2`, `#def.width=20`,
  `#param.t=5`, `#theme=dark`), and an SVG file that links such a file directly
  or through further SVG files. Its `href` (or its `xlink:href` when it has no
  `href`) becomes a `data:image/png;base64` URI of the picture.
- kept: Everything else, byte for byte: comments, formatting, line endings, a
  byte order mark, raster pictures, SVG files that link only rasters and SVG
  files, `data:` URIs (never followed), same-document `#` links, and network
  links.
- size: Each picture is drawn at the size of its placement in the drawing at its
  natural size (through the `viewBox` and every transform, as render draws it),
  times `--scale`; the SVG reader still fits it with `preserveAspectRatio`. A
  link used twice is drawn once, at the larger placement.
- other folder: When the copy is written to another folder, the relative links
  that are left are rewritten so they find their files from there; a drive
  letter or UNC path (`C:\x.png`, `\\server\share\x.png`) is left as written on
  every system. Links of other elements (an external `use`, a CSS `url()`) are
  not rewritten.
- bindings: A position binding (cn:x2y2 and the others, see `canvasnet help
  svg bindings`) is baked with the numbers of the PNG picture that replaces the
  link; its `cn:` attribute is removed, and the namespace declaration too when
  nothing uses it. The JSON document counts bindings.resolved and
  bindings.unresolved; a binding that fails keeps its attribute and numbers.
- nothing to flatten: When the drawing links no such picture, or shows none,
  nothing is written and the exit code is 0: the text says nothing to flatten
  and `flattened` is false in the JSON document.
- limits: The links are drawn by the resolver of render, with its link rule,
  file-access mode, and limits (at most 64 pictures, nested ones included, 4
  levels, and the pixel budget). The copy is at most 64 MiB (`size-limit`), and
  the drawing must be UTF-8 (`unsupported-feature` for another declared encoding
  unless the file is plain ASCII).
- `--strict`: A link that is not drawn (`svg-image-unresolved`,
  `svg-image-too-large`, `image-fragment-invalid`, or `image-page-out-of-range`)
  is an `unsupported-feature` error, as for render; a link the file-access mode
  denies (`image-access-denied`) is the error `file-access-denied`;
  a position binding that fails is the error `svg-binding-failed`;
  `svg-flatten-image-not-drawn` stays a warning.

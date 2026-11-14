---
title: Known differences from OpenSCAD
summary: Where the dialect differs from OpenSCAD, and how a model is placed in an SVG drawing.
see-also:
  - scad/views
  - svg/fragments
---

## Known differences

- This is an independent dialect, not OpenSCAD: pictures and tessellation are
  not pixel-identical to OpenSCAD and no parity is claimed.
- include and use are read relative to the folder of the file that names them
  (any local file, as in OpenSCAD, unless --file-access says otherwise; see
  'canvasnet help files'), a missing include is an error here, and an include
  the mode denies is the error file-access-denied.
- A model can be drawn inside an SVG diagram with `<image href="part.scad"
  .../>` (see 'canvasnet help svg'), with the view the library chooses for it
  (isometric for a 3D model, top-down for a flat 2D model, or the model's own
  $vp* variables) over a transparent background. `<image
  href="part.scad#view=front" .../>` chooses a named view instead (iso, front,
  back, left, right, top, bottom); #view without a value, or an unknown name, is
  image-fragment-invalid and the image is not drawn. Every def.<name>=<value> in
  the fragment sets a variable of the model exactly like -D name=value (same
  value grammar and precedence; a name that is not an identifier, or a value
  that is not valid, is image-fragment-invalid), for example `<image
  href="part.scad#view=front&amp;def.width=20" .../>`; the prefix def. means a
  variable can never collide with view. An input path of the command line takes
  the same fragment (canvasnet render "part.scad#view=front&def.width=20" -o
  part.png).
- Indexing is lenient: v[i] is undef for a negative or non-finite index, and a
  fractional index is truncated.
- text() is laid out one character at a time without shaping or ligatures;
  language and script are ignored; size is the height of the glyph H. A generic
  name (sans, serif, mono, sans-serif, monospace, ...) is a bundled Liberation
  font; any other family is looked up in the installed fonts (so it can differ
  between machines), then among the bundled Liberation families, and a family
  found nowhere is drawn with the bundled Liberation Sans and a warning. For
  each character the chosen font lacks, the glyph comes from the bundled Noto
  Sans, Noto Sans Math, or Noto Sans Symbols 2 (never an installed font), with
  no kerning across fonts; characters nothing covers are listed in one warning
  per text().
- The semantics of the # highlight and the % ghost are this package's documented
  choices; a ghost is part of the fitted bounds but not of the model.
- hull and minkowski constructs they cannot handle are omitted and reported,
  never approximated; so is a projection that is too complex or whose mesh is
  not closed.
- projection(cut = true) includes a face that lies exactly in the z = 0 plane (a
  closed section); the silhouette and the section are computed from the
  tessellated meshes, so a curved solid projects to its polygon.

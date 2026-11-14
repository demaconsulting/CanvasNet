---
title: Embedding files with image href
summary: Which files an image element may link, how they are sized, the link rule, and the diagnostics.
aliases:
  - links
  - image-links
see-also:
  - svg/fragments
  - files
examples:
  - name: embed
    kind: snippet
    language: xml
    file: diagram.svg
    description: 'A diagram whose image elements link a model, a chart, and another SVG file that sit in the same folder (a snippet: it needs those files, so it is not rendered alone).'
  - name: render-embed
    kind: command
    language: shell
    content: 'canvasnet render diagram.svg -o diagram.png'
    description: Draw that diagram.
---

- **href file types**: An image element may link a file whose name ends in .png,
  .jpg, .jpeg, .bmp, .tif, .tiff, .gif, .jp2, .j2k, or .j2c (a raster picture),
  .svg (another SVG file), .scad (an OpenSCAD model), .vl.json or .vl (a
  Vega-Lite chart), .mmd or .mermaid (a Mermaid diagram), or .pdf, .pptx, or
  .vsdx (a page of a document: a PDF page, a PowerPoint slide, or a Visio page;
  page 1 unless the href chooses another one, see page fragment below). The
  format is decided by the file name, not by the content, so a Vega-Lite chart
  in a file whose extension is only json is not linked; a raster file is still
  decoded by its content. Any other file is not drawn.
- **size**: A model, a chart, a Mermaid diagram, a document page, and an SVG
  file are drawn at the size of the image element (its width and height on the
  output), so they stay sharp at any scale; a raster is fitted into the element
  by preserveAspectRatio. An embedded model uses the library's own view of the
  model (isometric for a 3D model, top-down for a flat 2D model) over a
  transparent background.
- **link rule**: The href is relative to the folder of the SVG file (a nested
  SVG file finds its own images beside itself). A query (?cache=1) after the
  file name is ignored; a fragment (#page=2) after it is read as parameters, see
  page fragment below; an encoded %23 or %3F is a literal # or ? in the name. As
  in Pandoc, an absolute path, a drive letter, a path that leaves the folder
  with .., a local file: URI, and a symbolic link are followed; a network URL, a
  UNC path, and a file: URI with a host are never followed. Which local files
  may be read is chosen with --file-access and --allow-root (any regular local
  file by default); see 'canvasnet help files'.
- **unknown parameter**: A parameter the linked file does not use is not an
  error: the picture is drawn without it and the warning
  image-fragment-unknown-parameter is reported. This includes a fragment on a
  file that takes none, such as logo.png#anything or other.svg#top; such a link
  used to be ignored without a word.
- **embedded model includes**: A model linked from an image element follows the
  same rule as canvasnet render: a missing include or use file, an include that
  cannot be read, or a circular include is a scad-error, so the image is not
  drawn and the render fails, naming the link. Earlier versions only warned and
  drew the model without the file; this is a deliberate change, because a
  picture drawn without a file it asked for would be silently wrong.
- `svg-image-unresolved`: The warning for an href that was not drawn (missing, a
  network URL, a file type that is not listed here, a cycle, or a limit); it
  names the href and the reason, and --strict makes it an error.
- `svg-image-too-large`: The warning for an image whose picture is too large for
  the pixel budget of the render (64 million pixels in all, counted before
  anything is allocated, so a huge raster in a tiny box is refused, not
  decoded); nothing is drawn, and --strict makes it an error.
- `image-access-denied`: The warning for an href whose file the file-access mode
  denies: the image is not drawn, and the message names the href, the mode, and
  why; with --strict it is the error file-access-denied.
- **example**: `<image href="part.scad" x="10" y="10" width="120"
  height="120"/>` draws the model part.scad, which sits beside the SVG file, in
  a 120 by 120 box. Print a whole diagram with: canvasnet help svg --example
  embed

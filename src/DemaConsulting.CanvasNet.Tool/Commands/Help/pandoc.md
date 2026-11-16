---
title: The Pandoc filter (pandoc-filter)
summary: Pictures in Pandoc documents (docs/filters/canvasnet.lua).
aliases:
  - filter
see-also:
  - files
  - mermaid
  - svg
examples:
  - name: write-shim
    kind: command
    language: shell
    content: 'canvasnet pandoc-filter --write-shim docs/filters/canvasnet.lua'
    description: Write the Lua shim, then list it under filters in the Pandoc defaults file.
  - name: run
    kind: command
    language: shell
    content: 'canvasnet pandoc-filter --protocol 1 --filter-path docs/filters/canvasnet.lua --format html5 --input doc.json -o out.json'
    description: Run the filter without Pandoc on a document written by pandoc -t json.
---

The pictures of a Pandoc document are drawn by the canvasnet tool through the
Lua shim docs/filters/canvasnet.lua, which sends the whole document to
`canvasnet pandoc-filter` once and applies what comes back.

## Setup

- `shim`: Pin DemaConsulting.CanvasNet.Tool in .config/dotnet-tools.json, run
  'dotnet canvasnet pandoc-filter --write-shim docs/filters/canvasnet.lua', and
  list the file under filters in the Pandoc defaults file. Write it again only
  when the tool says that the protocol of the shim changed.
- `pictures`: A picture of a .scad model, a .vl.json or .vl chart, a .vsdx,
  .pptx, or .pdf page, or a .mmd or .mermaid diagram is drawn as a PNG picture,
  for example ![Part](part.scad){view=iso}. An .svg drawing that links one is
  flattened. Any other picture is left alone.

## Attributes

- `size`: WIDTHxHEIGHT in pixels, each 1 to 8192 (not for .svg). A model is
  1600x1200 unless size or scale is given.
- `scale`: A number from 0.001 up to 8192, not together with size. For .svg it
  sizes the pictures that replace the links.
- `background`: #RRGGBB, #RRGGBBAA, a color name, or transparent (not for .svg).
  A model is white unless one is given.
- `page, dpi`: .vsdx, .pptx, and .pdf: the page counted from 1 (default 1), and
  the resolution above 0 up to 2400 (a whole number for .vsdx).
- `view, azimuth, elevation, projection`: .scad: iso, front, back, left, right,
  top, or bottom; degrees about the Z axis; degrees from -90 to 90; ortho or
  perspective.
- `define-NAME`: .scad: sets a variable of the model.
- `param-NAME`: .vl.json and .vl: sets a parameter of the chart.
- `theme`: .mmd and .mermaid: default, base, dark, forest, or neutral.
- `others`: Every other attribute (width, an id, a class) stays on the picture.
  An attribute of another input type is an error, not ignored.

## Mermaid blocks

- `code block`: A fenced code block of class mermaid is drawn like a .mmd
  picture. With caption="..." it becomes a figure whose caption is read as
  Markdown and which takes the id; otherwise a paragraph holds the picture.
- `not drawn`: A diagram type the tool does not draw
  (mermaid-diagram-unsupported) is left as written, with a warning.

## Cache

- `folder`: The pictures are written to generated/canvasnet beside the folder
  of the shim, named by a SHA-1 of the source, every file it reads, the
  attributes, the tool, and the file-access arguments, so a changed file draws
  the picture again and an unchanged one is never drawn twice.
- `markers`: A diagram left as written and a drawing with nothing to flatten
  leave a marker, so later builds show the warning without drawing again.

## Variables

- `CANVASNET_TOOL`: The command instead of dotnet canvasnet, for example dotnet
  path/DemaConsulting.CanvasNet.Tool.dll. White space separates, double quotes
  group, and there are no escapes.
- `CANVASNET_FILE_ACCESS`: roots, no-symlinks, or roots,no-symlinks; a picture
  the mode denies stops the build. The files a picture references are read
  for its cache name under the same mode, so a file the mode denies (a link
  included) is never read. See 'canvasnet help files'.
- `CANVASNET_ROOTS`: Folders separated like PATH, each passed as --allow-root.
- `CANVASNET_JOBS`: How many pictures are drawn at once, 1 to 64 (default: the
  number of processors, at most 4). --jobs wins over it.

## Errors

- `pandoc-filter-error`: An attribute, a source that is not in the resource
  path, a limit (64 levels, 512 files), a variable, or the cache. The build
  stops with the message, which names the picture.
- `usage-error`: The shim speaks a protocol this tool does not. Write the shim
  again with --write-shim, or update the tool.
- `drawing failed`: A picture the tool cannot draw stops the build with the
  code and the message of the tool.

## Reserved names

- `canvasnet-warnings`: The metadata key the warnings come back in.
- `canvasnet-markdown`: The class of the placeholder span of a caption.

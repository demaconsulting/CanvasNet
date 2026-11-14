---
title: Diagnostics entries
summary: The fields of an entry of diagnostics[].
see-also:
  - errors/diagnostics
  - json/results
---

- `source`: Where the report comes from: scad, vega-lite, mermaid, pptx, vsdx,
  or svg. PDF and raster images have no diagnostics channel.
- `severity`: unsupported for a feature that was left out, or warning for a
  questionable construct that still rendered.
- `code`: The stable generic identifier of the report, for example
  scad-module-surface, vl-mark-geoshape, svg-image-unresolved, or
  image-page-out-of-range. A warning of a SCAD model always has the code
  scad-warning, a warning of a Vega-Lite chart the code vega-lite-warning, and a
  warning of a Mermaid diagram the code mermaid-warning; the finer identifier is
  in detail.
- `message`: A description; for a model, chart, or diagram it gives file, line,
  and column.
- `detail`: Only when the code is generic: the finer identifier of the library,
  for example scad-anchor-invalid for a scad-warning, vl-unknown-property for a
  vega-lite-warning, or mermaid-config-unknown-key for a mermaid-warning. Branch
  on code and detail, never on the message.
- `location`: Only when the file gave a position: {file, line, column} for a
  model or a diagram, or {file, pointer, line, column} for a chart (the JSON
  pointer of the offending property); a member that is not known is left out.
- `origin`: Only for a report about an embedded file (an SVG that links other
  files): the files, outermost first, that lead to it, as folder-relative names
  (a link that was not drawn names the SVG file that contains it). The message
  starts with the href as written.
- `page`: The page of a document the report belongs to.
- Without --json the same reports are written to standard error as
  'Unsupported: <code>: <message>' or 'Warning: <code>: <message>' lines (and
  echo as 'ECHO: <text>'); with --json standard error carries none of them, so
  standard output stays one JSON document.

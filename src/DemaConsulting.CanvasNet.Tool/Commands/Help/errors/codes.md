---
title: Error codes
summary: Every stable error code and what to do about it.
see-also:
  - errors
---

## Error codes

- `usage-error`: The command line is wrong: an unknown command, option, or help
  subject, a missing or malformed value, conflicting options, or a missing
  output. Read the hint and 'canvasnet <command> --help'.
- `operation-failed`: A command could not complete for a reason that has no more
  specific code. Read the message.
- `input-not-found`: The input file or folder does not exist. Check the path.
- `input-unreadable`: The input exists but cannot be read (permissions, a
  folder, a locked file).
- `unsupported-format`: The input is not a format the tool reads. 'canvasnet
  formats' lists them.
- `corrupt-input`: The content is damaged, or is not what its extension says, or
  a model, chart, diagram, or SVG file is beyond a size or depth limit. Repair
  or replace the file.
- `unsupported-feature`: The file is valid but uses a feature the library cannot
  draw or decode, or any skipped feature with --strict. Remove the feature, or
  drop --strict if it is only a skipped picture.
- `page-out-of-range`: --page, or a page= or slide= fragment of the input, is
  beyond the last page. Pages are counted from 1; 'canvasnet info' gives the
  count.
- `image-fragment-invalid`: The fragment of an input ('#...' after the file
  name, as in part.scad#def.width=20) cannot be used: it is longer than 2048
  characters or has more than 32 parameters, a page is not a whole number, a
  def. or param. value is not valid (a vector must be a JSON array, a param.
  value valid JSON when it starts with a bracket, brace, or quote), a def. name
  is not an identifier, the view is not one of the views, or the theme is not a
  Mermaid theme. Fix the value, and quote the input in a shell so that & is not
  read as a command separator. An unknown parameter name is only a warning
  (image-fragment-unknown-parameter, or vl-param-unknown for a chart).
- `size-limit`: A requested, declared, or resulting size exceeds
  {{maxDimension}} pixels on a side. Use a smaller size, resolution, or border.
- `same-input-output`: The output file is the input file. Name a different
  output.
- `output-exists`: The output exists and --no-clobber was given.
- `output-write-failed`: The output could not be written (an invalid path, a
  file where a folder should be, permissions, a full disk).
- `scad-error`: An OpenSCAD model has a syntax error, an evaluation error, or an
  include that cannot be read; the message has file, line, and column.
- `vega-lite-error`: A Vega-Lite chart has an invalid specification, a data file
  that is missing or unreadable, or cannot be laid out or drawn; the message has
  file, line, column, and JSON pointer.
- `mermaid-error`: A Mermaid diagram has a syntax error (mermaid-syntax-error),
  an unknown diagram type (mermaid-unknown-diagram), invalid frontmatter, or
  cannot be laid out; the message has file, line, column, and the mermaid-*
  code, and the hint suggests the fix.
- `file-access-denied`: A file the document references (a model's include or use
  file, a chart's data file, or with --strict a linked picture) was denied by
  the file-access mode chosen with --file-access and --allow-root, or the mode
  could not be applied; the message names the file as written, the mode, and
  why. The default mode, any, reads every regular local file; see 'canvasnet
  help files'.
- `budget-exceeded`: A model exceeds a rendering limit such as --max-triangles,
  or a chart or diagram exceeds a limit of its library (mermaid-limit-exceeded
  for a diagram). Raise the limit (a model) or simplify the model, chart, or
  diagram.
- `size-mismatch`: compare was given two images of different sizes and no
  --resize-to.
- `threshold-failed`: compare measured a value on the wrong side of a
  --threshold limit; the result and the pictures were still written.
- `positions-unmatched`: With --strict, a --positions selector matched no
  feature of the diagram or chart; nothing was written. Without --strict it is
  a warning with the same code. See 'canvasnet help positions selectors'.
- `svg-binding-failed`: With --strict, an SVG position binding (cn:x2y2 and the
  others) was not resolved; nothing was written. The message names the warning
  code (svg-binding-unmatched, svg-binding-ambiguous, and so on), the element,
  and the value. Without --strict the binding keeps the numbers written in the
  file. See 'canvasnet help svg bindings'.
- `pandoc-filter-error`: The pandoc-filter command cannot draw the pictures of
  a Pandoc document: an attribute of a picture is not valid, a source is not in
  the resource path, a model, chart, diagram, or drawing has too many files or
  is nested too deep, CANVASNET_FILE_ACCESS, CANVASNET_ROOTS, or CANVASNET_JOBS
  is not valid, or a file of the picture cache cannot be written. The message
  names the picture; see 'canvasnet help pandoc'.

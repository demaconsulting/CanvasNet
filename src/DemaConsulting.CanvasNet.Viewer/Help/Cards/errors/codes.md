---
title: Error codes
summary: Every stable error code and what to do about it.
tags:
  - diagnostics
see-also:
  - errors
---

## Error codes

- `usage-error`: A request is wrong: an option value the library refuses (a
  fractional resolution for a Visio drawing, an unknown view or theme name), or
  facts of a probe given to an operation with another file-access policy. Read
  the message and the hint.
- `operation-failed`: A source failed for a reason that has no more specific
  code (a host's own image source); read the message.
- `input-not-found`: The input file or folder does not exist. Check the path.
- `input-unreadable`: The input exists but cannot be read (permissions, a
  folder, a locked file), or is larger than 16 MiB.
- `unsupported-format`: The input is not a format CanvasNet reads.
  `CanvasNetViewer.Extensions` lists the file types.
- `corrupt-input`: The content is damaged, or is not what its extension says, or
  a model, chart, diagram, or SVG file is beyond a size or depth limit. Repair
  or replace the file. For malformed SVG markup `Location` has the line and
  column of the XML error.
- `unsupported-feature`: The file is valid but uses a feature the library cannot
  draw or decode, or any skipped feature in strict mode. Remove the feature, or
  turn strict mode off if it is only a skipped picture.
- `page-out-of-range`: The page (`Page`), or a page= or slide= fragment of the
  input, is beyond the last page. Pages are counted from 1;
  `ViewerImage.PageCount` or a probe gives the count.
- `image-fragment-invalid`: The fragment of an input ('#...' after the file
  name, as in part.scad#def.width=20) cannot be used: it is longer than 2048
  characters or has more than 32 parameters, a page is not a whole number, a
  def. or param. value is not valid (a vector must be a JSON array, a param.
  value valid JSON when it starts with a bracket, brace, or quote), a def. name
  is not an identifier, the view is not one of the views, or the theme is not a
  Mermaid theme. Fix the value. An unknown parameter name is only a warning
  (image-fragment-unknown-parameter, or vl-param-unknown for a chart).
- `size-limit`: A requested, declared, or resulting size exceeds
  {{maxDimension}} pixels on a side. Use a smaller size, resolution, or border.
- `same-input-output`: The output file is the input file. Name a different
  output.
- `output-exists`: The output exists and `WriteOptions.NoClobber` is set.
- `output-write-failed`: The output could not be written (an invalid path, a
  file where a folder should be, permissions, a full disk).
- `scad-error`: An OpenSCAD model has a syntax error, an evaluation error, or an
  include that cannot be read; the message and `Location` have file, line,
  and column.
- `vega-lite-error`: A Vega-Lite chart has an invalid specification, a data file
  that is missing or unreadable, or cannot be laid out or drawn; the message and
  `Location` have file, line, column, and JSON pointer.
- `mermaid-error`: A Mermaid diagram has a syntax error (mermaid-syntax-error),
  an unknown diagram type (mermaid-unknown-diagram), invalid frontmatter, or
  cannot be laid out; the message has file, line, column, and the mermaid-*
  code (also in `Location` and `DetailCode`), and the hint suggests the fix.
- `plantuml-error`: A PlantUML diagram has a syntax error
  (plantuml-syntax-error), an unknown @start tag (plantuml-unknown-diagram), an
  !include that cannot be read (plantuml-include-failed), or cannot be laid out;
  the message has file, line, column, and the plantuml-* code (also in
  `Location` and `DetailCode`), and the hint suggests the fix.
- `file-access-denied`: A file the document references (a model's include or use
  file, a chart's data file, a PlantUML diagram's !include file, or in strict
  mode a linked picture) was denied by the file-access policy of the render, or
  the policy could not be applied; the message names the file as written, the
  mode, and why. The default policy, any (`FileAccessPolicy.AllowAll`), reads
  every regular local file; see [[files]].
- `budget-exceeded`: A model exceeds a rendering limit such as its triangle
  limit (`MaxTriangles`), a chart or diagram exceeds a limit of its library
  (mermaid-limit-exceeded or plantuml-limit-exceeded for a diagram), or the
  picture does not fit in `ViewerRenderOptions.MaxDecodedPixels`. Raise the
  limit (a model, the pixel budget) or simplify the model, chart, or diagram.
- `size-mismatch`: A comparison (`ImageComparer`) was given two images of
  different sizes and no resize target (`Resize`).
- `positions-unmatched`: In strict mode, a position selector matched no feature
  of the diagram or chart; nothing was returned. Otherwise it is a warning with
  the same code. See [[positions/selectors]].
- `svg-binding-failed`: In strict mode, an SVG position binding (cn:x2y2 and the
  others) was not resolved; nothing was written. The message names the warning
  code (svg-binding-unmatched, svg-binding-ambiguous, and so on), the element,
  and the value. Otherwise the binding keeps the numbers written in the file.
  See [[svg/bindings]].

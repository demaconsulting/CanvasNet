---
title: Errors, diagnostics, exit codes, and --strict
summary: Exit codes, error codes, diagnostics, and --strict.
children:
  - codes
  - diagnostics
see-also:
  - json
  - files
examples:
  - name: missing-input
    kind: command
    language: shell
    content: 'canvasnet render missing.png -o out.png --json'
    description: A failure that exits with 1 and reports input-not-found as JSON and on standard error.
  - name: strict-gate
    kind: command
    language: shell
    content: 'canvasnet render bar.vl.json --strict -o out.png --json'
    description: Turn every unsupported-feature diagnostic of a chart into a failure.
---

How a failure is reported, the stable error codes, and what --strict changes.

## How failures are reported

- The exit code is 0 for success and 1 for every failure. The finer distinction
  is the stable error code.
- Standard error carries 'Error: <code>: <message>' and, when there is advice, a
  'Hint: <advice>' line. There is no stack trace for an expected failure. A
  successful command writes its diagnostics there too (see 'Diagnostics are not
  errors').
- Exception: a malformed global option (such as --depth abc) or a --log file
  that cannot be opened fails before the command line is fully read; it prints
  'Error: <message>' with no code and no JSON document, and the exit code is
  still 1.
- With --json (and without --silent) the failure is also written to standard
  output as a JSON failure document with error.code, error.message, and
  error.hint; see 'canvasnet help json'.
- 'Unexpected error: <message>' is a defect of the tool, not a mistake of the
  user: no JSON document is written and the exit code is the runtime's own
  non-zero code. Please report it.
- A usage-error names the closest valid command or option ('Did you mean ...?')
  when there is one.

## What --strict does

- `scad, vega-lite, mermaid, vsdx`: Every unsupported-feature diagnostic becomes
  an unsupported-feature error and no output is written.
- `pptx`: A skipped EMF or WMF picture becomes an error. Other unsupported
  shapes, fills, and pictures are errors with or without --strict.
- `svg`: An image that was not drawn (svg-image-unresolved, or
  svg-image-too-large) and an unsupported feature of a model, chart, or diagram
  it embeds become unsupported-feature errors, and an image the file-access
  mode denied (image-access-denied) becomes the error file-access-denied. Text
  that is not drawn or drawn without a feature (svg-text-not-drawn,
  svg-text-unsupported) is an unsupported-feature error, also in an embedded
  SVG file.
- `pdf, raster`: No effect: they have no diagnostics channel. A PDF feature that
  cannot be drawn is an error with or without --strict.

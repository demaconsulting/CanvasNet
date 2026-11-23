---
extends: errors/diagnostics
see-also:
  - json/diagnostics
---

## Command line

- A diagnostic is a note in diagnostics[] of a successful result; read
  diagnostics even when ok is true.
- Without --json each diagnostic is also written to standard error, one line
  each: 'Unsupported: <code>: <message>' for severity unsupported, 'Warning:
  <code>: <message>' for severity warning, and 'Info: <code>: <message>' for
  severity info, with the input and page (or the side of a comparison) in
  parentheses at the end. The exit code stays 0 and standard
  output keeps only the result. At most 20 are written, then '... and N more
  diagnostics (use --json for all)'.
- A SCAD model's echo output is written to standard error as the model printed
  it, at most 20 lines. --silent suppresses all of these lines; with --json none
  are written (standard output is one JSON document and diagnostics[] and echo[]
  carry the same notes).
- svg-arrowhead-shape, the fragment warnings, and the info note vega-lite-info
  (a chart field without a type) never fail a command, not even with --strict.

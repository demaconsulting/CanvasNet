---
extends: errors
title: Errors, diagnostics, exit codes, and --strict
summary: Exit codes, error codes, diagnostics, and --strict.
see-also:
  - json
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

## Command line

- The exit code is 0 for success and 1 for every failure. The finer distinction
  is the stable error code.
- Standard error carries 'Error: <code>: <message>' and, when there is advice, a
  'Hint: <advice>' line. There is no stack trace for an expected failure. A
  successful command writes its diagnostics there too (see
  [[errors/diagnostics]]).
- Exception: a malformed global option (such as --depth abc) or a --log file
  that cannot be opened fails before the command line is fully read; it prints
  'Error: <message>' with no code and no JSON document, and the exit code is
  still 1.
- With --json (and without --silent) the failure is also written to standard
  output as a JSON failure document with error.code, error.message, and
  error.hint; see [[json]].
- 'Unexpected error: <message>' is a defect of the tool, not a mistake of the
  user: no JSON document is written and the exit code is the runtime's own
  non-zero code. Please report it.
- A usage-error names the closest valid command or option ('Did you mean ...?')
  when there is one.
- `--strict` turns on strict mode for every command that draws.

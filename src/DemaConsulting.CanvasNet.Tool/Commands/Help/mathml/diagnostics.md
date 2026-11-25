---
extends: mathml/diagnostics
---

## Command line

- A warning is listed in diagnostics[] of the JSON result as {source: mathml,
  severity: warning, code: mathml-warning, detail, location: {file, line,
  column}} (without --json, as a 'Warning: mathml-warning: <message>' line on
  standard error). An element, attribute, or style that is not drawn is
  {severity: unsupported, code: mathml-element-unsupported and the like}; with
  --strict the first one fails with unsupported-feature and no file is written.
- An error ends the command with exit code 1, an 'Error: <code>: <message>'
  line, and a 'Hint:' line on standard error, for example 'Error: mathml-error:
  MathML syntax error: eq.mml:3:10: mathml-syntax-error: the text is not
  well-formed XML: ...'; with --json the error is in the JSON document.

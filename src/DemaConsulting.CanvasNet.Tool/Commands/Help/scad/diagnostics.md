---
extends: scad/diagnostics
---

## Command line

- Without --strict an unsupported construct is listed in diagnostics[] of the
  JSON result as {source: scad, severity: unsupported, code: <id>} (without
  --json, as an 'Unsupported: <id>: <message>' line on standard error). With
  --strict the first one fails and no file is written.
- The output of echo() is in echo[] of the JSON result; without --json it is
  written to standard error in the same form.
- `--max-triangles`: The triangle budget that budget-exceeded names.

---
extends: vega-lite/diagnostics
---

## Command line

An unsupported construct is listed in diagnostics[] as {source: vega-lite,
severity: unsupported, code: <id>} (without --json, also as an 'Unsupported:
<id>: <message>' line on standard error). With --strict the first one fails with
unsupported-feature.

A field without a type is listed as {source: vega-lite, severity: info, code:
vega-lite-info, detail: vl-type-inferred} (without --json, an 'Info:
vega-lite-info: <message>' line); it never fails --strict.

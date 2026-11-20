---
extends: vega-lite/diagnostics
---

## Command line

An unsupported construct is listed in diagnostics[] as {source: vega-lite,
severity: unsupported, code: <id>} (without --json, also as an 'Unsupported:
<id>: <message>' line on standard error). With --strict the first one fails with
unsupported-feature.

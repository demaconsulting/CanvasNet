---
extends: vega-lite/warnings
---

## Command line

- `--strict` fails on svg-image-unresolved, image-fragment-invalid, and
  image-page-out-of-range, not on vl-param-unknown.
- `--param` of the command line is a vega-lite-error for a param the
  specification does not declare.

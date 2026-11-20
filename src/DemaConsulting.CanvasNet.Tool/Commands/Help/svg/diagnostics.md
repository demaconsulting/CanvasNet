---
extends: svg/diagnostics
see-also:
  - json/diagnostics
---

## Command line

- `--strict`: Turns on strict mode, so the codes above that it fails on are
  errors.
- `--file-access`, `--allow-root`: Choose the file-access mode that
  image-access-denied names.
- `canvasnet flatten`: Reports svg-flatten-image-not-drawn.
- With --json each of these reports carries the embedding file in origin (see
  [[json]]); the message starts with the href as written.

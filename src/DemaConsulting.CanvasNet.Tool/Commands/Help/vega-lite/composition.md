---
extends: vega-lite/composition
---

## Command line

- `canvasnet info chart.vl.json --json` reports the natural size of the chart.
- `#param.<name>=<value>` sets a param the way `--param` does on the command
  line.
- Render the chart alone with `--positions` and `--json` to read the positions;
  at --scale 2 a raster picture covers twice the pixels.

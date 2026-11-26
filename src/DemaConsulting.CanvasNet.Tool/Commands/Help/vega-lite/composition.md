---
extends: vega-lite/composition
see-also:
  - pandoc
---

## Command line

- `canvasnet info chart.vl.json --json` reports the natural size of the chart.
- `#param.<name>=<value>` sets a param the way `--param` does on the command
  line.
- Render the chart alone with `--positions` and `--json` to read the positions;
  at --scale 2 a raster picture covers twice the pixels.
- Pandoc: a code block of class vega-lite (or vegalite) and a .vl.json or .vl
  picture are drawn by the filter; a chart that reads a data file must be a
  picture. See [[pandoc]].

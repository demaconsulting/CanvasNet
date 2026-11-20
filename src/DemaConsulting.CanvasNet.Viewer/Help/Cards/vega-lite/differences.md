---
title: Known differences from Vega-Lite
summary: General differences from Vega-Lite and vega-embed.
tags:
  - vega-lite
  - diagnostics
see-also:
  - vega-lite/data
  - vega-lite/layout
---

The differences that concern one subject are on its card: data, marks,
encodings, transforms, params, layout, and composition.

## Known differences

- Text is measured with the bundled fonts, so labels, legends, and facet headers
  can differ from a browser by a few pixels. A character the Liberation fonts
  lack is drawn from the bundled Noto fonts, never from an installed font, so
  the result does not depend on the machine.
- Compared with vega-embed: no actions menu, no signals, no renderer choice, and
  no config or datasets injection from the command line.

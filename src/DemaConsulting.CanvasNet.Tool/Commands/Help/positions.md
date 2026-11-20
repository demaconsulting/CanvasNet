---
extends: positions
see-also:
  - json/results
examples:
  - name: read-positions
    kind: command
    language: shell
    content: 'canvasnet render flowchart.mmd --size 430x280 --positions node:send --positions edge -o slot.png --json'
    description: The Send node and every edge of the flowchart example at the size of its image, with their boxes and pixels.
  - name: read-marks
    kind: command
    language: shell
    content: 'canvasnet render bar.vl.json --positions mark:x=B --positions axis -o bar.png --json'
    description: The bar of stall B and both axes of the bar chart example.
---

## Command line

- `--positions <selector>`: Render with `--positions <selector> --json`; it
  needs `--json` and a diagram or chart; repeat it to ask for more
  ([[#read-positions]], [[#read-marks]]). With `--strict` a
  selector that matches nothing is the error positions-unmatched and nothing is
  written.
- In the JSON result each item of `positions[]` is {kind, id, ..., box: {x, y,
  width, height}, pixel: [x, y], inside}, and positionsTruncated is true when
  more matched.

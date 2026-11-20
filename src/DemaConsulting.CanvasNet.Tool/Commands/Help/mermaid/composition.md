---
extends: mermaid/composition
---

## Command line

- Add `--strict` to fail on a picture that is not drawn; `info` lays the diagram
  out without the pictures.
- Show variants side by side with montage, one cell per theme:

```shell
canvasnet montage "flow.mmd#theme=default" "flow.mmd#theme=neutral" --labels --cell 480x240 -o themes.png
```

- Render the diagram alone at the size of its placement with `--positions` and
  `--json` to read the positions.

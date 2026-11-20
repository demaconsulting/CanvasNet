---
title: 'State diagrams: settings and known differences'
summary: The state settings and the known differences of a state diagram.
see-also:
  - mermaid/state
  - mermaid/differences
---

Set these in the YAML frontmatter (`config:`) or in an `%%{init: ...}%%`
directive, with Mermaid's precedence.

## Settings

- `state.nodeSpacing` (default 50): The space between the states of a rank.
- `state.rankSpacing` (default 50): The space between the ranks.
- `state.titleTopMargin` (default 25): The margin above the title.
- `state.defaultRenderer` (default `dagre-wrapper`): Another value selects
  Mermaid's legacy renderer for a `stateDiagram`; it is reported as
  `mermaid-layout-unsupported` and drawn the same way.
- `htmlLabels` (default unset): `false` draws the texts of the states as SVG
  text instead of HTML labels.
- `flowchart.curve` (default `basis`): The curve of the transition lines.
- `flowchart.padding` (default 15): The padding of a note.
- `state.padding`, `state.radius`, and the other legacy keys (default as in
  Mermaid): Accepted, and without effect as in Mermaid's renderer.
- `state.useMaxWidth` (default `true`): No meaning for a picture.

## Known differences

- Mermaid's legacy state renderer is not ported: a stateDiagram with another
  state.defaultRenderer is drawn with the current renderer and reported as
  mermaid-layout-unsupported at the state key of the configuration.
- The direction info reports for a state or entity relationship diagram is the
  last top-level direction statement, the one the diagram is laid out in, where
  Mermaid's getDirection gives the first.
- The last concurrent region of a composite state has the id id-random-N where
  Mermaid's id holds a random number; the id is never drawn.

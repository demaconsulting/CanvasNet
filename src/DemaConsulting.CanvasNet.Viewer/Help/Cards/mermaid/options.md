---
title: Render options of a diagram
summary: Theme, size, background, and --strict for a diagram, and what info reports.
tags:
  - mermaid
  - hosting
see-also:
  - mermaid/diagnostics
  - limits
---

## Options

- `--theme`: The theme: default, base, dark, forest, or neutral, like mmdc -t. A
  theme the diagram sets in its frontmatter or a directive still wins. In a link
  or an input path the fragment theme=dark does the same (flow.mmd#theme=dark).
  On any other kind of input --theme is a usage-error.
- `--scale`: The natural size is Mermaid's SVG view box in pixels; --scale,
  --width, and --height draw the diagram sharply at another size (a diagram
  larger than {{maxDimension}} pixels on a side needs one of them).
- `--background`: The color under the diagram: white by default, transparent
  keeps the transparency of Mermaid's own SVG.
- `--strict`: Every unsupported construct, and a picture that is not drawn,
  becomes an unsupported-feature error.

## Supported

- info reports the type, direction, titles, and the numbers of nodes, edges, and
  subgraphs (participants, messages, and boxes for a sequence diagram; classes,
  relations, and namespaces for a class diagram; states, transitions, and
  composite states for a state diagram; entities, relationships, and subgraphs
  for an entity relationship diagram; the slices of a pie chart (its legend
  entries) as nodes; the tasks and the sections of a gantt chart as nodes and
  subgraphs; the nodes of a mindmap, and its edges, one fewer than its nodes;
  the blocks of a packet diagram as nodes; the commits of a git graph as nodes
  and the links to their parents as edges) without drawing the diagram.

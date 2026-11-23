---
title: Render options of a diagram
summary: Theme, size, background, and strict mode for a diagram, and what a probe reports.
tags:
  - mermaid
  - hosting
see-also:
  - mermaid/diagnostics
  - limits
---

The host chooses these when it renders a diagram: the properties of
`ViewerRenderOptions` for `CanvasNetViewer.Render`, or of `SourceOptions` for
the operations of the Imaging namespace.

## Options

- `Theme`: The theme of `DiagramOptions` (`SourceOptions.Mermaid`): default,
  base, dark, forest, or neutral, like mmdc -t. A theme the diagram sets in its
  frontmatter or a directive still wins. In a link or an input path the fragment
  theme=dark does the same (flow.mmd#theme=dark). Any other name is a
  usage-error; every other kind of file ignores the option.
- `Scale`: The natural size is Mermaid's SVG view box in pixels; a scale factor,
  a `Width`, or a `Height` draws the diagram sharply at another size (a diagram
  larger than {{maxDimension}} pixels on a side needs one of them).
- `Background`: The color under the diagram (white by default);
  a transparent color keeps the transparency of Mermaid's own SVG.
- `ThrowOnUnsupportedFeature`: Strict mode (`SourceOptions.Strict`): every
  unsupported construct, and a picture that is not drawn, becomes an
  unsupported-feature error.
- `MermaidLimits`: The resource limits of the diagram
  (`ViewerRenderOptions.MermaidLimits`), lower or higher than the defaults, for
  the diagram and every diagram it embeds; raise one only for trusted content
  (see [[limits]]).

## Supported

- A probe (`SourceLoader.Probe`, its `SourceInfo.Diagram`) reports the type,
  direction, titles, and the numbers of nodes, edges, and subgraphs
  (participants, messages, and boxes for a sequence diagram; classes, relations,
  and namespaces for a class diagram; states, transitions, and composite states
  for a state diagram; entities, relationships, and subgraphs for an entity
  relationship diagram; the slices of a pie chart (its legend entries) as nodes;
  the tasks and the sections of a gantt chart as nodes and subgraphs; the nodes
  of a mindmap, and its edges, one fewer than its nodes; the blocks of a packet
  diagram as nodes; the commits of a git graph as nodes and the links to their
  parents as edges) without drawing the diagram.

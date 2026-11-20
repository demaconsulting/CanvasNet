---
title: Render options of a PlantUML diagram
summary: Size, background, strict mode, file access, and what a probe reports for a PlantUML diagram.
tags:
  - plantuml
  - hosting
see-also:
  - plantuml/diagnostics
  - limits
  - files
---

The host chooses these when it renders a diagram: the properties of
`ViewerRenderOptions` for `CanvasNetViewer.Render`, or of `SourceOptions` for
the operations of the Imaging namespace.

## Options

- `Scale`: The natural size is PlantUML's SVG size in pixels (times the
  diagram's own scale); a scale factor, a `Width`, or a `Height` draws the
  diagram sharply at another size (a diagram larger than {{maxDimension}} pixels
  on a side needs one of them).
- `Background`: The color under the diagram: the diagram's own background
  (white, or its skinparam backgroundColor) by default; a transparent color
  gives a transparent picture.
- `ThrowOnUnsupportedFeature`: Strict mode (`SourceOptions.Strict`): every
  unsupported construct, and a type that is not drawn, becomes an
  unsupported-feature error.
- `FileAccess`: The `FileAccessPolicy` the !include files are read under (see
  [[files]]).
- Theme and positions: A PlantUML diagram has neither; the Mermaid theme
  (`DiagramOptions`) and the position selectors do not apply to it.

## Supported

- A probe (`SourceLoader.Probe`, its `SourceInfo.Diagram`) reports the type
  (sequence, activity, or the name of a type that is not drawn, such as class or
  usecase), whether this version draws it, the title, and the numbers of nodes,
  edges, and subgraphs: the participants, messages, and boxes of a sequence
  diagram; the actions, start, stop, end, connector, and decision nodes, the
  arrows, and the swimlanes and partitions of an activity diagram. The direction
  and the accessible title are absent.

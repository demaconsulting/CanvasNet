---
title: Unsupported Mermaid
summary: The diagram types and constructs that are reported and not drawn.
tags:
  - mermaid
  - diagnostics
see-also:
  - mermaid/diagnostics
  - mermaid/differences
---

## Unsupported

- `mermaid-diagram-unsupported`: The diagram type is not drawn by this version
  (every type but `flowchart`, `graph`, `sequenceDiagram`, `classDiagram`,
  `stateDiagram`, `erDiagram`, `pie`, `gantt`, `mindmap`, `packet`, and
  `gitGraph`); the picture is empty.
- Every other diagram type is recognized: a probe reports it as not supported
  (`DiagramFacts.Supported` is false), and a render draws an empty picture of 16
  by 16 pixels with the diagnostic mermaid-diagram-unsupported (strict mode
  makes it an unsupported-feature failure).
- `mermaid-icon-unsupported`: An icon (`fa:fa-car` in a label, an `@{ icon: ...
  }` node, an `icon` in the `properties` of a sequence participant, an
  `::icon(...)` of a mindmap node); the node or participant keeps its shape and
  label without the icon.
- `mermaid-math-unsupported`: Math (`$$...$$`, drawn by KaTeX in Mermaid) in a
  sequence diagram; the text is drawn as written.
- `mermaid-look-unsupported`: A `look` other than `classic` (`handDrawn`,
  `neo`); the diagram is drawn classic.
- `mermaid-layout-unsupported`: A `layout` other than `dagre` (`elk`, also the
  `flowchart-elk` keyword) or, for a mindmap, other than `cose-bilkent`, or a
  `state.defaultRenderer` that selects Mermaid's legacy state renderer; the
  diagram is laid out with dagre (a mindmap with cose-bilkent), and a legacy
  state diagram is drawn as a `stateDiagram-v2`.
- `mermaid-theme-css-unsupported`: A `themeCSS` rule whose selector is not one
  of the diagram's own (such as `.node rect` in a flowchart), an at-rule, or a
  nested rule; the rule is not applied.

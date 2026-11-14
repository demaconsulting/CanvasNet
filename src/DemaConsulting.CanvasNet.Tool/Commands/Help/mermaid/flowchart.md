---
title: Flowcharts
summary: Nodes, shapes, links, subgraphs, labels, styles, and settings of a flowchart.
aliases:
  - graph
see-also:
  - mermaid/style
  - mermaid/composition
  - mermaid/options
  - mermaid/diagnostics
examples:
  - name: flowchart
    kind: source
    language: mermaid
    file: flowchart.mmd
    description: A small flowchart with a decision, a subgraph, link labels, and a class.
---

## Supported

- Flowcharts: the flowchart and graph keywords with the directions TB, TD, BT,
  LR, and RL, laid out with a port of Mermaid's dagre layout, so the nodes,
  edges, and labels land where Mermaid 11 puts them.
- Node shapes: every bracket form ([ ], ( ), ([ ]), [[ ]], [( )], (( )), (((
  ))), > ], { } and its doubled form (a hexagon), [/ /], [\ \], [/ \], [\ /])
  and the @{ shape: ... } syntax with all of Mermaid's named shapes (rect,
  rounded, stadium, cyl, diam, hex, doc, docs, fork, delay, and so on).
- Links: arrows, open links, dotted (-.->) and thick (==>) links, circle and
  cross heads, two-headed links, links of any length (--->), labels (-- text -->
  and -->|text|), chains and & groups (a & b --> c), invisible links (~~~),
  self-loops, and edge ids with curve and animate shape data (an animated edge
  is drawn as its first frame).
- Subgraphs: titles, nesting, their own direction, links to and from a subgraph.
- Labels: quoted text, Markdown strings ("`**bold** and *italic*`"), <br> and
  the common inline tags (b, strong, i, em, u, ins, span with a style, img),
  entity codes (#quot;, #35;), and wrapping at wrappingWidth.
- Styles: style, classDef (including classDef default), class, :::, linkStyle
  (including default and interpolate), with fill, stroke, stroke-width,
  stroke-dasharray, color, font-weight, font-style, font-size, font-family, and
  opacity.
- Configuration: YAML frontmatter (title, config), %%{init: ...}%% directives,
  the five themes (default, base, dark, forest, neutral), themeVariables, and
  the flowchart settings (nodeSpacing, rankSpacing, curve, padding,
  wrappingWidth, and so on), with Mermaid's precedence.
- Titles: the title statement, else the frontmatter title, is drawn above the
  diagram; accTitle and accDescr are read as metadata.

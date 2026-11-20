---
title: Mermaid diagrams (.mmd, .mermaid)
summary: 'Mermaid diagrams (.mmd): ten types laid out like Mermaid.'
tags:
  - mermaid
  - authoring
aliases:
  - mmd
children:
  - flowchart
  - sequence
  - class
  - state
  - er
  - pie
  - gantt
  - mindmap
  - packet
  - gitgraph
  - style
  - composition
  - options
  - diagnostics
  - unsupported
  - differences
see-also:
  - style
  - composition
  - svg
  - limits
---

A Mermaid flowchart, sequence diagram, class diagram, state diagram, entity
relationship diagram, pie chart, gantt chart, mindmap, packet diagram, or git
graph is laid out like Mermaid 11 and drawn as one still picture with the
bundled fonts. Every other diagram type is recognized and reported (see
[[mermaid/unsupported]]).

Read the card of your diagram type, for example [[mermaid/gantt]];
each has a small source example
([[#gantt]]). Render with
`canvasnet render flow.mmd -o flow.png --json` and look at the PNG.

For a good-looking diagram read [[mermaid/style]]; to show pictures
in nodes or place a diagram in a figure, [[mermaid/composition]].

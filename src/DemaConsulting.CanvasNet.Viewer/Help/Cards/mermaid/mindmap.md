---
title: Mindmaps
summary: Nodes, shapes, texts, sections, edges, and the layout of a mindmap.
tags:
  - mermaid
  - authoring
children:
  - settings
see-also:
  - mermaid/style
  - mermaid/options
  - mermaid/diagnostics
examples:
  - name: mindmap
    kind: source
    language: mermaid
    file: mindmap.mmd
    description: 'A small mindmap with three sections and every node shape: a circle root, default nodes, a rectangle, a rounded rectangle, a bang, a hexagon, a cloud, and a Markdown string.'
---

Mindmaps: a mindmap starts with mindmap. It is drawn by a port of Mermaid's
mindmap renderer and laid out with a port of cose-bilkent, the force-directed
layout Mermaid uses for a mindmap, so the nodes and the branches land where
Mermaid 11 puts them; its syntax and settings are listed below and on the
settings card ([[mermaid/mindmap/settings]]).

## Syntax

- Header: `mindmap` on its own line.
- Nodes: one node per line; the first node is the root, and each other node is a
  child of the nearest node above it that is indented less; a node that no node
  can take as its parent (a second root) is a `mermaid-syntax-error`, as in
  Mermaid.
- Shapes: `id[text]` a rectangle, `id(text)` a rounded rectangle, `id((text))` a
  circle, `id)text(` a cloud, `id))text((` a bang, the text between two braces
  on each side a hexagon, and a node without brackets the default shape, a box
  with a line under it.
- Texts: a node's text is its id when it has no brackets; in brackets it can be
  quoted (`id["text"]`), also as a Markdown string (a backtick inside each
  quote); every text is Markdown (`**bold**`, `*italic*`) and wraps at
  `mindmap.maxNodeWidth`.
- Icons: `::icon(fa fa-book)` on the line after a node is read and reported
  (`mermaid-icon-unsupported`), and the node keeps its shape and text; Mermaid
  11 does not draw it either.
- Classes: `:::urgent large` on the line after a node gives it classes, which
  only `themeCSS` can style, with a rule for one of them and an element the node
  draws (`rect`, `path`, `circle`, or `polygon` for its shape, `line` for the
  line under a default node, `text` or `span` for its text), such as `.urgent
  rect`.
- Sections: each child of the root starts a section that its whole branch
  shares; the sections take the theme's section colors in turn, eleven before
  they repeat, and the root has a color of its own.
- Edges: a curve from each node to each of its children, in the color of the
  section, thinner the further its node is indented, as in Mermaid.
- Layout: cose-bilkent, Mermaid's default for a mindmap: the root's children
  around it, and each branch pushed away from the others; a `layout` other than
  `cose-bilkent` is reported (`mermaid-layout-unsupported`), and the mindmap is
  laid out with cose-bilkent.
- Comments: `%%` comments on their own line.
- Colors: the theme variables `cScale0` to `cScale11` (the section fills and
  edges), `cScaleLabel0` to `cScaleLabel11` (the section texts), `cScaleInv0` to
  `cScaleInv11` (the line under a default node, which the base, dark, forest,
  and neutral themes leave out), `git0` (the root's fill), and `gitBranchLabel0`
  (the root's text).
- Configuration: YAML frontmatter, `%%{init: ...}%%` directives, the five
  themes, `themeVariables`, `themeCSS`, and the mindmap settings below, with
  Mermaid's precedence.

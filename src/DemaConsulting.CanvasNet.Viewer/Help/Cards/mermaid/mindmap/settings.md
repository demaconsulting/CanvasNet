---
title: 'Mindmaps: settings and known differences'
summary: 'The mindmap settings and the known differences of a mindmap: layout, icons, and depth.'
see-also:
  - mermaid/mindmap
  - mermaid/differences
---

Set these in the YAML frontmatter (`config:`) or in an `%%{init: ...}%%`
directive, with Mermaid's precedence.

## Settings

- `mindmap.padding` (default 10): The space between a node's text and its
  outline (twice as much in a hexagon; a rectangle and a rounded rectangle keep
  Mermaid's own space), and the margin around the mindmap.
- `mindmap.maxNodeWidth` (default 200): The width at which a node's text wraps
  (a rectangle, a rounded rectangle, and a hexagon wrap at
  `flowchart.wrappingWidth`, as in Mermaid).
- `mindmap.layoutAlgorithm` (default `cose-bilkent`): Accepted, and without
  effect as in Mermaid; the top-level `layout` selects the layout.
- `mindmap.useMaxWidth` (default `true`): No meaning for a picture.

## Known differences

- A mindmap is laid out exactly as Mermaid's cose-bilkent lays it out when its
  node sizes are Mermaid's, up to 1,016 nodes (above that, a few node counts
  differ where Chrome's pow is not correctly rounded). The layout is chaotic: a
  node whose size differs slightly from Chrome's (such as a cloud or a bang,
  whose arcs Chrome measures in single precision) can move other nodes far. The
  layout is the same on every machine.
- The icons of a mindmap (::icon(...)) are not drawn, as in Mermaid 11, and are
  reported (mermaid-icon-unsupported).
- Another mindmap layout (dagre, elk, tidy-tree, and others that mmdc can load)
  is reported (mermaid-layout-unsupported), and the mindmap is laid out with
  cose-bilkent.
- A mindmap of the keyword alone has no view box in Mermaid; here it is an empty
  picture of the margin, 20 by 20 pixels at the default padding.
- An ::icon(...) or a ::: class before the first node of a mindmap, which
  Mermaid fails on with a script error, is a located mermaid-syntax-error.
- The depth of a mindmap is bounded by MaxNestingDepth, where Mermaid has no
  limit, and a mindmap of about 1,000 nodes or more can reach the layout work
  limit (MaxLayoutWork).

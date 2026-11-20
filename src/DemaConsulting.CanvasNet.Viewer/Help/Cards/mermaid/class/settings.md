---
title: 'Class diagrams: settings and known differences'
summary: The class settings and the known differences of a class diagram.
tags:
  - mermaid
  - authoring
see-also:
  - mermaid/class
  - mermaid/differences
---

Set these in the YAML frontmatter (`config:`) or in an `%%{init: ...}%%`
directive, with Mermaid's precedence.

## Settings

- `class.hideEmptyMembersBox` (default `false`): Leaves out the empty member
  compartment of a class that has no members and no methods.
- `class.hierarchicalNamespaces` (default `true`): Makes a dotted namespace name
  (`Geo.Plane`) a namespace inside another; `false` draws one namespace of that
  name.
- `class.padding` (default 12): The padding of a class box, and when it is set,
  of the namespaces and the notes too, which otherwise pad by 16 and 6.
- `class.defaultRenderer` (default `dagre-wrapper`): Another value selects
  Mermaid's legacy `class` diagram, which is drawn the same way.
- `state.nodeSpacing` (default 50): The space between the classes of a rank.
- `state.rankSpacing` (default 50): The space between the ranks.
- `state.titleTopMargin` (default 25): The margin above the title.
- `htmlLabels` (default unset): `false` draws the texts of the class boxes as
  SVG text instead of HTML labels; such a box is within a pixel of Mermaid's.
- `flowchart.curve` (default `basis`): The curve of the relation lines.
- `class.nodeSpacing`, `class.rankSpacing`, `class.titleTopMargin` (default
  unset): Accepted, and without effect as in Mermaid; the `state` keys above
  apply instead.
- `class.htmlLabels`, `class.diagramPadding`, `class.useMaxWidth` (default
  unset): Accepted, and without effect as in Mermaid.

## Known differences

- With htmlLabels: false the texts of a class box are SVG text, measured as
  Chrome measures them; Chrome's box of a few glyphs at the end of a text is a
  pixel wider, so such a class box can differ from Mermaid's by up to a pixel.
- Mermaid's fix-up of an SVG class text that holds &gt (a generic) is not
  ported; the entities are decoded instead, which gives the same characters for
  a text of one line.
- $$ math in a class text is drawn and measured as written, without a report.

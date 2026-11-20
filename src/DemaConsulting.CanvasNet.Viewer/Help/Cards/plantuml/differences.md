---
title: Known differences from PlantUML
summary: Fonts, framing, includes, the sequence and activity engines, and other general differences.
tags:
  - plantuml
  - diagnostics
see-also:
  - plantuml/unsupported
  - plantuml/style
---

The differences of one diagram type are on its card, for example
[[plantuml/sequence]].

## Known differences

- Text is measured and drawn with the bundled Liberation fonts (PlantUML's
  default sans-serif is Arial, whose advances Liberation Sans shares), never an
  installed font, so the result does not depend on the machine; the reference
  diagrams agree with PlantUML's SVG within 1.5 pixels.
- A text with no @start tag is read as one diagram, as PlantUML reads a piped
  text; PlantUML refuses such a file.
- A missing !include file is an error, where PlantUML ignores it, and the
  diagram's own file counts as already included.
- Only the default look and the supported skinparam settings are drawn: no
  themes, <style> blocks, shadows, gradients (the first color is used), or
  hand-written mode.
- Statements are recognized by their leading keywords and arrows, so in rare
  ambiguous cases a diagram of a type that is not drawn is named differently;
  that never changes whether it is drawn.
- The picture is static and has no links.

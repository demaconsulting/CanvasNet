---
title: 'Entity relationship diagrams: settings and known differences'
summary: The entity relationship settings and the known differences of an entity relationship diagram.
see-also:
  - mermaid/er
  - mermaid/differences
---

Set these in the YAML frontmatter (`config:`) or in an `%%{init: ...}%%`
directive, with Mermaid's precedence.

## Settings

- `er.nodeSpacing` (default 140): The space between the entities of a rank.
- `er.rankSpacing` (default 80): The space between the ranks.
- `er.titleTopMargin` (default 25): The margin above the title.
- `er.diagramPadding` (default 20): The padding added to the width of each
  column of a table, and around the name of an entity without attributes.
- `er.entityPadding` (default 15): The padding added to the height of each row
  of a table.
- `er.minEntityWidth` (default 100): The least width of an entity without
  attributes.
- `htmlLabels` (default unset): `false` draws the texts of the entities as SVG
  text instead of HTML labels; `true` leaves out the quarter that Mermaid adds
  to the paddings of a table otherwise.
- `er.minEntityHeight`, `er.layoutDirection`, `er.fill`, `er.stroke`,
  `er.fontSize` (default as in Mermaid): Accepted, and without effect as in
  Mermaid's renderer.
- `er.useMaxWidth` (default `true`): No meaning for a picture.

## Known differences

- Mermaid's entity relationship markers clip the line at the edge of their view
  port, which hides half of the stroke at the tip; the clip is not modeled, so a
  tip can be half a stroke longer.
- With htmlLabels: false the texts of an entity table take the style sheet's
  label color (nodeTextColor, else textColor), where Chrome uses textColor; the
  two are the same in the built-in themes.
- The marker shapes of the neo look and the entity colors of the redux themes
  are not drawn (the look is reported, and the themes are not available).

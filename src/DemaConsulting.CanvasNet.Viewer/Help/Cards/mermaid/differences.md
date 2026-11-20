---
title: Known differences from Mermaid
summary: Fonts, text widths, the background, static pictures, and other general differences.
tags:
  - mermaid
  - diagnostics
see-also:
  - mermaid/unsupported
  - mermaid/options
---

The differences of one diagram type are on its settings card, for example
[[mermaid/gantt/settings]].

## Known differences

- Text is measured and drawn with the bundled Liberation Sans (Mermaid's default
  trebuchet ms, verdana, and arial map to it) and the bundled Noto fallback
  fonts, never an installed font, so the result does not depend on the machine;
  against Mermaid rendered with the same font the layout matches to a hundredth
  of a pixel.
- The circles of the new shapes are true circles where Mermaid draws rough.js
  curves (a few hundredths of a pixel).
- The picture is static: no interaction, and an animated edge is drawn as its
  first frame.
- info lays the diagram out without the pictures of its nodes (it reads no other
  file), so a picture node without w and h can make the drawn diagram a little
  larger or smaller than the size info reports (with --width or --height it is
  fitted into the box); give an @{ img } node its w and h (constraint on) so
  that the layout does not depend on the picture. A render without --width and
  --height reports the natural size of the diagram it drew (the natural size of
  render --json); a library render of the diagram file without a size
  (CanvasNetViewer.Render) also charges its pixel budget for that drawn size,
  while a document that embeds the diagram always gives it a size.
- A diagram is drawn over white unless --background says otherwise (Mermaid's
  SVG is transparent); as a picture in an SVG file it stays transparent.
- Mermaid crashes on some invalid graphs (a subgraph cycle, a link to a subgraph
  without an anchor, two composite states that contain each other, a note on a
  state that appears nowhere before it, a relationship to an entity named like a
  subgraph declared after it, a relationship from a subgraph to an entity inside
  it); these are a located mermaid-error instead.
- Interaction (click, href, call, tooltips, the link and callback of a class
  diagram, and the click of a state diagram or a gantt chart) is read and
  ignored without a report, because a picture is static.
- The color keywords and system colors of a box or a rect background take the
  values of headless Chrome in the light scheme (system colors depend on the
  platform in a browser).
- The width of a text is the advance of its glyphs grown to the pixels its first
  and last glyphs reach, as Chrome measures it; for fonts or sizes other than
  those of the reference diagrams, a width can differ by a pixel, which can move
  the margins by up to a pixel.
- A style declaration of a class, state, or entity relationship diagram outside
  the supported subset is reported at the first mention of the class, state, or
  entity, not at the style statement.
- A corner radius (rx or ry) in a style, a classDef, or themeCSS is ignored with
  a mermaid-style-ignored warning; the radii of Mermaid's own style sheet are
  drawn.
- The texts of class and state diagrams are always cleaned as at Mermaid's
  strict level; securityLevel changes nothing.
- A character that no bundled font has is drawn as the missing-glyph box in a
  pie, gantt, packet, or git graph text without the warning
  mermaid-text-missing-glyph.

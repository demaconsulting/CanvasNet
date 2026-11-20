---
title: Packet diagrams
summary: Blocks, labels, rows, bit numbers, titles, and colors of a packet diagram.
children:
  - settings
see-also:
  - mermaid/style
  - mermaid/options
  - mermaid/diagnostics
examples:
  - name: packet
    kind: source
    language: mermaid
    file: packet.mmd
    description: A small packet diagram of three rows with ranges, single bits, +N blocks, a block split across two rows, and a title.
---

Packet diagrams: a packet diagram starts with packet or packet-beta. It is drawn
by a port of Mermaid's packet renderer, so the blocks, the bit numbers, and the
title land where Mermaid 11 puts them; its syntax and settings are listed below
and on the settings card (`canvasnet help mermaid packet settings`).

## Syntax

- Header: `packet` or `packet-beta`; the first statement can follow on the same
  line.
- Blocks: `0-15: "label"` (the bits 0 to 15), `16: "label"` (one bit), or `+8:
  "label"` (the next 8 bits), one per line; each block starts at the bit after
  the block before it, the first at bit 0, and a gap, an overlap, an end before
  the start, or `+0` is a `mermaid-syntax-error` with Mermaid's message.
- Labels: in double or single quotes, with backslash escapes; a label is SVG
  text, so entity codes (`#35;`) are drawn as their characters, HTML tags as
  written, and every run of white space as one space.
- Rows: `packet.bitsPerRow` bits to a row; a block that crosses the end of a row
  is split there, and both parts show its label.
- Bit numbers: the first and the last bit of each block above its left and right
  edges (one number in the middle of a block of one bit), unless
  `packet.showBits` is `false`.
- Titles: `title`, else the frontmatter `title`, is drawn centered in a row of
  its own below the blocks; `accTitle` and `accDescr` (on one line, or in braces
  over several lines) are read as metadata.
- Comments: `%%` comments, on their own line or after a block.
- Colors: the theme variable `packet`, an object with `startByteColor`,
  `endByteColor`, `byteFontSize`, `labelColor`, `labelFontSize`, `titleColor`,
  `titleFontSize`, `blockStrokeColor`, `blockStrokeWidth`, and `blockFillColor`;
  it replaces the object of the dark and forest themes, so a key it leaves out
  takes the default black or `#efefef`; the frontmatter and a directive keep
  only `labelColor`, `titleColor`, `labelFontSize`, and `titleFontSize`, as
  Mermaid does, and report the others as `mermaid-config-unknown-key`.
- Configuration: YAML frontmatter, `%%{init: ...}%%` directives, the five
  themes, `themeVariables`, `themeCSS` (the selectors `.packetBlock`,
  `.packetLabel`, `.packetTitle`, `.packetByte`, `.packetByte.start`, and
  `.packetByte.end`), and the packet settings below, with Mermaid's precedence.

---
title: 'Packet diagrams: settings and known differences'
summary: The packet settings and the known differences of a packet diagram.
tags:
  - mermaid
  - authoring
see-also:
  - mermaid/packet
  - mermaid/differences
---

Set these in the YAML frontmatter (`config:`) or in an `%%{init: ...}%%`
directive, with Mermaid's precedence.

## Settings

- `packet.rowHeight` (default 32): The height of a row of blocks.
- `packet.bitWidth` (default 32): The width of one bit.
- `packet.bitsPerRow` (default 32): The number of bits in a row.
- `packet.showBits` (default `true`): `false` draws no bit numbers and takes
  away the 10 pixels above each row that they need.
- `packet.paddingX` (default 5): The space between two blocks of a row.
- `packet.paddingY` (default 5): The space above each row (10 pixels more when
  the bit numbers are drawn).
- `packet.useMaxWidth` (default `true`): No meaning for a picture.

## Known differences

- MaxNodes bounds the blocks of a packet diagram and the parts that its rows
  split them into, so a range that Mermaid splits into thousands of rows (such
  as 0-99999999) fails with mermaid-limit-exceeded at the default limits; with a
  raised limit, Mermaid's own cap of 10,000 rows is kept.
- A packet setting of the wrong type is mermaid-config-invalid and ignored,
  where Mermaid computes with it. A fractional, zero, or negative
  packet.bitsPerRow splits the blocks exactly as Mermaid splits them, and where
  Mermaid then fails with Block start ... is greater than block end ..., the
  render is a located mermaid-render-failed. Every block is checked when the
  text is read, so an invalid block is always the mermaid-syntax-error, where
  Mermaid can fail at an earlier split first.

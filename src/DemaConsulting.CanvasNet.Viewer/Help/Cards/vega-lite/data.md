---
title: Files, data, and dates
summary: The file names a chart may have, inline and local data, dates in UTC, and fonts.
tags:
  - vega-lite
  - authoring
  - file-access
see-also:
  - files
  - limits
---

## Supported

- Files: .vl.json and .vl, or a .json file whose top-level $schema is a
  Vega-Lite schema. A $schema of version 5 or 6 (version 6 defaults apply) or
  none.
- Data: inline values, a local url (JSON, CSV, TSV, or DSV, relative to the
  specification's folder, absolute, or a file: URI without a host), sequence,
  and the datasets of the specification; the file-access policy decides which
  local files may be read.
- Dates in UTC; text drawn with the bundled Liberation fonts, and a character
  they lack (such as the math sign U+221D) taken from the bundled Noto Sans,
  Noto Sans Math, or Noto Sans Symbols 2, per character; a character none covers
  is drawn as the font's missing-glyph box and warned about
  (vl-text-missing-glyph).

## Known differences

- A schema of version 5 is drawn with the version 6 defaults (a view is 300 by
  300 pixels by default, for example).
- Dates are always UTC; nothing is read from the network; data files are read
  relative to the specification's folder, under the file-access policy.
- Delimited text is not type-inferred (as in Vega): cells stay strings unless
  format.parse says otherwise.

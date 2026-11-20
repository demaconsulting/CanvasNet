---
title: Diagnostics of a PlantUML diagram
summary: The plantuml-* warnings and errors of a diagram.
tags:
  - plantuml
  - diagnostics
see-also:
  - plantuml/unsupported
  - errors
---

## Diagnostics

- `plantuml-warning`: The code of every warning of a diagram; the specific
  plantuml-* code is in detail and the line and column in location.
- `plantuml-syntax-error`: Error (plantuml-error): a statement no parser of the
  diagram type accepts, an empty diagram, or text that is not valid UTF-8; the
  message gives the file, the line, and the column, and the hint suggests the
  fix.
- `plantuml-unknown-diagram`: Error (plantuml-error): an `@start` tag PlantUML
  does not define; the hint names the nearest one.
- `plantuml-include-failed`: Error (plantuml-error): an `!include` file that is
  missing or cannot be read; a file the file-access mode denies is
  file-access-denied instead.
- `plantuml-limit-exceeded`: Error (budget-exceeded): a limit of the library
  (nodes, edges, includes, nesting, label length, layout work, painted pixels);
  see [[limits]].
- `plantuml-render-failed`: Error (plantuml-error): the diagram cannot be laid
  out; please report it.
- `plantuml-skinparam-ignored`: Warning: a `skinparam` setting outside the
  supported subset, or with a value that cannot be used; the message says why.
- `plantuml-pragma-ignored`: Warning: a `!pragma` line, which has no effect.
- `plantuml-text-missing-glyph`: Warning: a character that the font of its text
  does not have; it is drawn with a bundled fallback font, or as a box when no
  bundled font has it.
- `plantuml-diagnostics-suppressed`: Warning: more reports than the diagnostics
  limit; the rest are not listed.

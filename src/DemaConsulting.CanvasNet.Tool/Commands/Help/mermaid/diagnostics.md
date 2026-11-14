---
title: Diagnostics of a diagram
summary: The mermaid-* warnings and errors of a diagram.
see-also:
  - mermaid/unsupported
  - errors
---

## Diagnostics

- `mermaid-warning`: The code of every warning of a diagram; the specific
  mermaid-* code is in detail and the line and column in location.
- `mermaid-shape-unknown`: Error (mermaid-error): a shape name Mermaid does not
  define; the hint names the nearest one.
- `mermaid-syntax-error`: Error (mermaid-error): the text does not follow the
  grammar of its diagram type; the message gives the line, the column, and the
  tokens that were expected, and the hint suggests the fix.
- `mermaid-unknown-diagram`: Error (mermaid-error): the first statement is not a
  Mermaid diagram keyword; the hint names the nearest one.
- `mermaid-yaml-invalid`: Error (mermaid-error): the frontmatter or the shape
  data is not valid YAML; anchors, aliases, and tags are refused
  (mermaid-yaml-unsupported).
- `mermaid-limit-exceeded`: Error (budget-exceeded): a limit of the library
  (nodes, edges, nesting, label length, layout work, pictures, painted pixels);
  see 'canvasnet help limits'.
- `mermaid-render-failed`: Error (mermaid-error): the diagram cannot be laid
  out, for example a subgraph that contains itself, an empty sequence block, two
  composite states that contain each other, or a gantt task whose date is
  invalid or whose after and until references go in a circle.
- `mermaid-participant-type-unknown`: Warning: a sequence participant has a type
  Mermaid does not define; it is not drawn, as in Mermaid, and the hint names
  the nearest type.
- `mermaid-config-unknown-key`: Warning: a configuration key Mermaid does not
  define; it is ignored.
- `mermaid-config-invalid`: Warning: a configuration value of the wrong type or
  range, or a theme that is not available; it is ignored
  (`sequence.textPlacement: fo` is drawn with the default placement).
- `mermaid-config-secure-key`: Warning: the text tried to change a secure key
  (`securityLevel`, `maxTextSize`, `maxEdges`, and so on); the change is
  ignored.
- `mermaid-style-ignored`: Warning: a style declaration outside the supported
  subset; it is ignored.
- `mermaid-html-tag-ignored`: Warning: an HTML tag outside the supported label
  subset; the tag is dropped and its text is kept.
- `mermaid-image-unresolved`: Warning: a picture the resolver did not give; the
  node keeps its frame.
- A link that is not drawn (a missing file, a network URL) is the warning
  svg-image-unresolved with the reason, a link the file-access mode denies is
  image-access-denied, and --strict makes either a failure.
- `mermaid-text-missing-glyph`: Warning: a character that no bundled font has
  (for example CJK); it is drawn as the missing-glyph box.
- `mermaid-gantt-today-unavailable`: Warning: a gantt chart needs today (a
  `todayMarker` style, an `after` or `until` id that no task has, or a date
  without a year, month, or day) and `MermaidRenderOptions.Now` is not set; the
  today line is not drawn and 1970-01-01 is taken as today.
- `mermaid-gantt-ticks-skipped`: Warning: the `tickInterval` of a gantt chart
  spans more than 10,000 of its intervals (Mermaid's estimate of the ticks;
  exactly 10,000 draws 10,001 ticks, both ends included); the default ticks are
  drawn, as in Mermaid.
- `mermaid-gantt-excludes-skipped`: Warning: the tasks of a gantt chart with
  excluded days span more than five years; the excluded days are not shaded, as
  in Mermaid.
- `mermaid-diagnostics-suppressed`: Warning: more reports than `MaxDiagnostics`;
  the rest are dropped.
- A text longer than {{mmdMaxTextSize}} characters is corrupt-input.

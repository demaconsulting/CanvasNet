---
title: Unsupported PlantUML
summary: The diagram types and constructs that are reported and not drawn.
see-also:
  - plantuml/diagnostics
  - plantuml/differences
---

## Unsupported

- `plantuml-diagram-unsupported`: The diagram type is not drawn by this version
  (every type but sequence, activity, class, object, and state diagrams); the
  picture is empty.
- Every other diagram type is recognized: usecase, component, deployment,
  timing, the legacy activity syntax (activity-legacy), and the `@start`
  tags of mindmap, gantt, wbs, json, yaml, salt, and the others.
  info reports it with "supported": false, and render writes an empty picture of
  20 by 20 pixels with the diagnostic plantuml-diagram-unsupported (--strict
  makes it an unsupported-feature failure).
- `plantuml-multiple-diagrams`: The text has more than one `@start` block; only
  the first one is drawn.
- `plantuml-preprocessor-unsupported`: A preprocessor directive outside the
  subset (`!if`, `!function`, `!procedure`, `!foreach`, built-in `%functions`,
  `!define` with parameters); it is dropped with the block it opens.
- `plantuml-include-unsupported`: An include that is never followed: a URL, the
  standard library (`<name>`), a part of a file (`file!part`), `!includeurl`,
  `!includesub`, `!includedef`, or `!import`.
- `plantuml-theme-unsupported`: A `!theme` line; the default look is drawn.
- `plantuml-style-unsupported`: A `<style>` block; it is skipped.
- `plantuml-statement-unsupported`: A statement PlantUML defines that this
  version does not draw (`ref over`, `newpage`, `switch`, `goto`, a
  specification and description language box style, a role or a crow's foot
  of a class relationship, `together`, an entry point of a state diagram,
  ...); the rest of the diagram is drawn.
- `plantuml-markup-unsupported`: Creole markup outside the subset (images,
  sprites, icons, tables, lists, headings); the markup is drawn as text.

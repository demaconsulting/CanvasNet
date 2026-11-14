---
title: Class diagrams
summary: Classes, members, generics, annotations, relations, namespaces, notes, and styles.
children:
  - settings
see-also:
  - mermaid/options
  - mermaid/diagnostics
examples:
  - name: class
    kind: source
    language: mermaid
    file: class.mmd
    description: A small class diagram with an abstract class, members and methods, a generic, a static method, inheritance, cardinalities, and a note.
---

Class diagrams: a class diagram starts with classDiagram. It is laid out with
the same port of Mermaid's dagre layout as a flowchart, so the class boxes,
relations, namespaces, and notes land where Mermaid 11 puts them; its syntax,
relations, and settings are listed below and on the settings card (`canvasnet
help mermaid class settings`).

## Syntax

- Header: `classDiagram` and `classDiagram-v2`, and `direction` with `TB`, `BT`,
  `LR`, or `RL`.
- Classes: `class Name`, a body in braces (`class Name { ... }`), member lines
  (`Name : +int id`), a label (`class Order["Purchase order"]`), names written
  in backticks, and classes named only by a relation.
- Members: attributes and methods with the visibility `+`, `-`, `#`, or `~`,
  parameters, return types, and the classifiers `$` (static, underlined) and `*`
  (abstract, italic).
- Generics: `~T~` in class names, members, and methods, drawn as `<T>`, also
  nested (`List~List~int~~`).
- Annotations: `<<interface>>`, `<<enumeration>>`, and any other annotation, in
  a body, after the class name, or as a statement (`<<service>> Service`); only
  the first annotation of a class is drawn, as in Mermaid.
- Relations: the eight kinds of relation in both directions, two-way relations,
  lollipop interfaces, labels (`A --> B : uses`), and cardinalities (`A "1" -->
  "*" B`).
- Namespaces: `namespace Name { ... }`, nested, dotted names (`namespace
  Geo.Plane`), and a label (`namespace Utils["Utility types"]`).
- Notes: `note "text"` and `note for Name "text"`, with `\n` and `<br>` line
  breaks.
- Styles: `classDef` (including `classDef default`), `cssClass "A,B" name`,
  `:::`, and `style`, with the declarations of a flowchart style; as in Mermaid,
  a style cannot start with a key that holds a dash, so write
  `fill:#f9f,stroke-width:2px`, not `stroke-width:2px,fill:#f9f`.
- Interaction: `click`, `link`, and `callback` are read and ignored, because a
  picture is static.
- Titles: the frontmatter `title` is drawn above the diagram; `accTitle` and
  `accDescr` are read as metadata.
- Configuration: YAML frontmatter, `%%{init: ...}%%` directives, the five
  themes, `themeVariables`, and the class settings below, with Mermaid's
  precedence.

## Relations

- `<|--` and `--|>`: Inheritance: a solid line with a hollow triangle.
- `*--` and `--*`: Composition: a solid line with a filled diamond.
- `o--` and `--o`: Aggregation: a solid line with a hollow diamond.
- `<--` and `-->`: Association: a solid line with an arrowhead.
- `--`: Link: a solid line without markers.
- `<..` and `..>`: Dependency: a dashed line with an arrowhead.
- `<|..` and `..|>`: Realization: a dashed line with a hollow triangle.
- `..`: Dashed link: a dashed line without markers.
- `()--` and `--()`: Lollipop interface: a solid line to a small circle labeled
  with the name of the interface (`Engine --() Runnable`).
- `<|--|>`, `*--*`, `o--o`, `<-->`, `<..>`, `*--o`: Two-way: a marker at both
  ends; any two ends combine, on a solid (`--`) or a dashed (`..`) line.
- `A "1" --> "*" B`: Cardinality: a quoted text before or after the line (`"1"`,
  `"0..1"`, `"1..*"`, `"many"`) is drawn small next to that end.
- `A --> B : text`: Label: the text after the colon is drawn at the middle of
  the line.

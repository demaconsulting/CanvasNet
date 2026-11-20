---
title: Entity relationship diagrams
summary: Entities, attributes, relationships, cardinalities, subgraphs, and styles.
tags:
  - mermaid
  - authoring
children:
  - settings
see-also:
  - mermaid/style
  - mermaid/composition
  - mermaid/options
  - mermaid/diagnostics
examples:
  - name: er
    kind: source
    language: mermaid
    file: er.mmd
    description: A small entity relationship diagram with tables of attributes, keys, comments, and three relationships with their cardinalities.
---

Entity relationship diagrams: an entity relationship diagram starts with
erDiagram. It is laid out with the same port of Mermaid's dagre layout as a
flowchart, so the entities, relationships, and subgraphs land where Mermaid 11
puts them; its syntax, cardinalities, and settings are listed below and on the
settings card ([[mermaid/er/settings]]).

## Syntax

- Header: `erDiagram`, and `direction` with `TB`, `BT`, `LR`, or `RL`; the last
  top-level `direction` lays the diagram out.
- Entities: names, quoted names with spaces (`"Entry Log"`), an alias in
  brackets (`p["Person"]` or `p[Person]`; the first alias is kept), and entities
  named only by a relationship.
- Relationships: `A ||--o{ B : places`: an entity, a cardinality, a line, a
  cardinality, an entity, and after the colon a role of one word or a quoted
  text; the cardinalities and lines are listed below.
- Attributes: a block in braces after the entity (`CUSTOMER { ... }`), one
  attribute per line: a type, a name, keys (`PK`, `FK`, `UK`, several with
  commas, such as `PK, FK`), and a quoted comment; types such as `varchar(255)`,
  `string[]`, and `decimal?`; names and types in backticks; repeated blocks add
  to the attributes.
- Generics: `~T~` in an attribute type, drawn as `<T>` (`List~int~` is
  `List<int>`).
- Subgraphs: `subgraph Id["Title"] ... end`, nested, with their own `direction`;
  the subgraphs are laid out in Mermaid's order, the last one first.
- Styles: `classDef` (including `classDef default`), `class A,B name`, `:::` on
  an entity or a relationship end, and `style`, with the declarations of a
  flowchart style.
- Titles: the frontmatter `title` is drawn above the diagram; `accTitle` and
  `accDescr` are read as metadata.
- Configuration: YAML frontmatter, `%%{init: ...}%%` directives, the five
  themes, `themeVariables`, and the entity relationship settings below, with
  Mermaid's precedence.

## Relations

- `|o` and `o|`, or the words `zero or one` and `one or zero`: Zero or one: a
  circle and a bar.
- `||`, or the words `only one`, `one`, and `1`: Exactly one: two bars.
- `}o` and `o{`, or the words `zero or more`, `zero or many`, `many(0)`, `0+`,
  and `many`: Zero or more: a circle and a crow's foot.
- `}|` and `|{`, or the words `one or more`, `one or many`, `many(1)`, and `1+`:
  One or more: a bar and a crow's foot.
- `u`, before the line only (`A u--o{ B`): Parent: no marker at that end, as in
  Mermaid.
- `--` and `to`: Identifying: a solid line.
- `..`, `.-`, `-.`, and `optionally to`: Non-identifying: a dashed line.
- `A ||--o{ B : places`: Role: the word or the quoted text after the colon is
  drawn at the middle of the line.

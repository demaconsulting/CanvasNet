---
title: Activity diagrams
summary: Actions, decisions, loops, forks, swimlanes, partitions, notes, and arrows of an activity diagram.
tags:
  - plantuml
  - authoring
see-also:
  - plantuml/style
  - plantuml/options
  - plantuml/diagnostics
examples:
  - name: activity
    kind: source
    language: plantuml
    file: activity.puml
    description: A small activity diagram with a decision, a fork, a loop, and two stops.
---

## Supported

- Activity diagrams: a diagram in the new activity syntax (`start`, `:action;`,
  `if`, `while`, ...) is an activity diagram. It is laid out like PlantUML's
  ftile composition, with no Graphviz.
- Start and end: `start`, `stop`, `end`, `kill`, and `detach`.
- Actions: `:text;` on one or more lines, with a `#color` before the colon and
  creole markup in the text.
- Decisions: `if (condition) then (label)`, `elseif`, `else`, and `endif` (or
  `end if`), with the `is` and `equals` forms, nested.
- Loops: `while (condition) is (label)` ... `endwhile (label)`, and `repeat` ...
  `repeat while (condition) is (label) not (label)`, with `backward:`.
- Parallel branches: `fork`, `fork again`, `end fork`, and `end merge`; `split`,
  `split again`, and `end split`.
- Swimlanes: `|Lane|` and `|#color|Lane|`; the lanes are columns in the order
  they appear.
- Partitions: `partition Name { ... }` and `group` frames around instructions.
- Notes: `note left` or `note right`, on one line or up to `end note`, floating
  notes, and a color.
- Arrows: `-> label;` before an instruction, and the `-[#color]->`,
  `-[dashed]->`, `-[dotted]->`, `-[bold]->`, and `-[hidden]->` styles.
- Connectors: `(A)` circles that join two parts of a flow.
- Page texts: `title`, `header`, `footer`, `caption`, and `legend` ...
  `endlegend`, and `scale N`.

## Known differences

- switch blocks, goto and label, break, the specification and description
  language box styles (:text|, :text<, :text>, :text/, :text], :text}), and the
  frames of package, rectangle, and card groups are reported as
  plantuml-statement-unsupported; an action with such a style is drawn as a
  plain box.
- An arrow with several colors is drawn with its first color, and a repeat loop
  whose back arrow crosses swimlanes is drawn with the simple back arrow.
- The legacy activity syntax ((*) --> ...) is a type that is not drawn.

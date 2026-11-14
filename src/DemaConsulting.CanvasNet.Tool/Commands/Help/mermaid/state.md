---
title: State diagrams
summary: States, transitions, composite states, concurrency, pseudo-states, notes, and styles.
children:
  - settings
see-also:
  - mermaid/options
  - mermaid/diagnostics
examples:
  - name: state
    kind: source
    language: mermaid
    file: state.mmd
    description: A small state diagram with start and end states, labeled transitions, a composite state, and a note.
---

State diagrams: a state diagram starts with stateDiagram-v2 or stateDiagram. It
is laid out with the same port of Mermaid's dagre layout as a flowchart, so the
states, transitions, composite states, and notes land where Mermaid 11 puts
them; its syntax and settings are listed below and on the settings card
(`canvasnet help mermaid state settings`).

## Syntax

- Header: `stateDiagram-v2` and `stateDiagram`, and `direction` with `TB`, `BT`,
  `LR`, or `RL`; the last top-level `direction` lays the diagram out.
- States: an id (`Idle`), a description after a colon (`Idle : waiting for
  input`), several descriptions, `state "Waiting for input" as Waiting`, and
  states named only by a transition.
- Transitions: `A --> B`, a label after a colon (`A --> B : start`), self
  transitions, and `[*]` as the start or the end state.
- Composite states: `state Name { ... }`, nested, with their own `direction` and
  their own `[*]`, and transitions to and from a composite state or a state
  inside it.
- Concurrency: `--` inside a composite state divides it into concurrent regions,
  drawn with dashed borders.
- Pseudo-states: `<<fork>>`, `<<join>>`, and `<<choice>>`, and the forms
  `[[fork]]`, `[[join]]`, and `[[choice]]` (`state split <<fork>>`).
- Notes: `note left of` and `note right of` a state, with the text after a colon
  or on the lines up to `end note`, and `<br>` line breaks; a floating note
  (`note "text" as N`) is read and not drawn, as in Mermaid.
- Styles: `classDef` (but not `classDef default`, which Mermaid's grammar
  refuses), `class A,B name`, `:::`, and `style`, with the declarations of a
  flowchart style.
- Interaction: `click` and `click ... href` are read and ignored, because a
  picture is static.
- Titles: the frontmatter `title` is drawn above the diagram; `accTitle` and
  `accDescr` are read as metadata; `hide empty description` and `scale` are read
  and ignored, as in Mermaid's renderer.
- Configuration: YAML frontmatter, `%%{init: ...}%%` directives, the five
  themes, `themeVariables`, and the state settings below, with Mermaid's
  precedence.

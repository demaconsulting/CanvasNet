---
title: Sequence diagrams
summary: Participants, messages, activations, notes, blocks, numbers, and menus of a sequence diagram.
see-also:
  - mermaid/options
  - mermaid/differences
examples:
  - name: sequence
    kind: source
    language: mermaid
    file: sequence.mmd
    description: A small sequence diagram with an actor, an activation, numbered messages, an alt block, and a note.
---

## Supported

- Sequence diagrams: a sequence diagram starts with sequenceDiagram. It is laid
  out by a port of Mermaid's sequence renderer: the participants stand in a row
  in the order they appear, and every message, note, activation, and block is
  placed below the one before it, where Mermaid 11 puts it.
- Participants: participant and actor, aliases (participant Bank as Payment
  provider), and participants named only by a message.
- Participant types: the eight types of @{ type: ... }: participant, actor,
  boundary, control, entity, database, collections, and queue, with the alias
  key.
- Boxes: box with a color and a title (box LightBlue Front end ... end), with a
  color only, or with a title only.
- Create and destroy: create participant and create actor just before the first
  message of the participant, and destroy just before its last message.
- Messages: the arrows ->, -->, ->>, -->>, <<->>, <<-->>, -x, --x, -), and --),
  the half-arrow heads, self messages, and central connections (A->>()B,
  A()->>B, A()->>()B).
- The half-arrow heads draw one side of a head: -|\ and -|/ draw a solid half
  head at the top or at the bottom, and -\\ and -// a stick half head; with --
  the line is dotted (--|\, --//, and so on). Written before the line, they
  point back at the sender: /|-, \|-, //-, and \\-, and the dotted /|--, \|--,
  //--, and \\--.
- Activations: activate and deactivate, and the + and - shorthands on a message,
  nested.
- Notes: Note left of, right of, and over one or two participants.
- Blocks: loop, alt and else, opt, par and and, par_over, critical and option,
  and break, nested; rect backgrounds with a CSS color.
- Numbers: autonumber, with a start and a step, and autonumber off; the
  showSequenceNumbers setting.
- Text: <br> line breaks, the wrap: and nowrap: prefixes, wrapping when wrap is
  on (a word wider than the line is hyphenated), and entity codes.
- Menus and properties: link, links, and properties; a menu is drawn only with
  forceMenus, and a class in properties changes the paint of the participant.
- A hidden menu still takes its room: the diagram is as large as if the menu
  panel below the participant (20 pixels plus 30 per link, and as wide as its
  widest label) were drawn.
- The lines of a menu link label are drawn on one baseline when the participant
  font size has a unit, such as an actorFontSize of 18px with the top-level
  fontSize set to 0, because Mermaid spaces them by the size as a number.
- Configuration: YAML frontmatter, %%{init: ...}%% directives, the five themes,
  themeVariables, and the sequence settings (actorMargin, mirrorActors,
  rightAngles, messageAlign, noteAlign, wrap, and so on), with Mermaid's
  precedence. As in Mermaid, the top-level fontFamily and fontSize replace the
  font families and sizes of the sequence settings, so every sequence text is 16
  pixels in the theme's font by default; set the top-level fontSize to change
  it. A top-level fontWeight replaces the three weights in the same way.

## Known differences

- Mermaid draws a broken sequence diagram or fails for an empty block (a block
  with no message or note inside), for link or links on an actor, boundary,
  control, or entity participant, and for a properties icon that is not text;
  these are a located mermaid-render-failed instead.
- $$ math is drawn and measured as written (mermaid-math-unsupported), and
  textPlacement: fo (HTML labels) is drawn with the default placement.

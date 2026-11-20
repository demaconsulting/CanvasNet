---
title: Styling a PlantUML diagram
summary: The default look, the supported skinparam settings, and colors, for a good-looking diagram.
see-also:
  - style
  - plantuml/composition
  - plantuml/differences
examples:
  - name: styled
    kind: source
    language: plantuml
    file: styled.puml
    description: A sequence diagram restyled with skinparam colors for its participants, arrows, notes, and group.
---

PlantUML draws its modern default look: white background, light purple
participants (#E2E2F0), light gray actions and classes (#F1F1F1), pale yellow
notes, and dark lines. Change it with skinparam, then render and look at the
PNG.

## Supported

- skinparam: one setting per line (skinparam arrowColor #2F6DB3) or a block
  (skinparam participant { ... }); settings outside the subset are the warning
  plantuml-skinparam-ignored.
- Elements: the eight participant kinds, note, sequenceGroup and
  sequenceGroupHeader, sequenceBox, sequenceDivider, sequenceDelay,
  sequenceLifeLine, arrow, activity, activityDiamond, swimlane and
  swimlaneTitle, partition, title, header, footer, legend, and caption; in
  class and object diagrams also class, classStereotype, classAttribute (the
  members), object, objectStereotype, map, and package; in state diagrams also
  state and stateAttribute (the descriptions); in use case, component, and
  deployment diagrams also usecase, component, interface, node, rectangle,
  folder, frame, cloud, card, artifact, storage, file, stack, agent, hexagon,
  person, label, and package, and the participant kinds style their actors,
  databases, queues, boundaries, controls, entities, and collections; and
  arrow styles the links of all these diagrams.
- Properties: BackgroundColor, BorderColor, FontColor, FontSize, FontName,
  FontStyle, and BorderThickness of each element.
- Document: backgroundColor (transparent keeps the page), defaultFontName,
  defaultFontSize, defaultFontColor, roundCorner, shadowing false,
  sequenceArrowThickness, sequenceMessageAlign, responseMessageBelowArrow,
  maxMessageSize, and footbox; in class and object diagrams also
  classAttributeIconSize (0 writes the visibility characters instead of
  icons) and circledCharacterRadius; in state diagrams also stateStartColor
  and stateEndColor; and in class, object, state, use case, component, and
  deployment diagrams nodesep and ranksep.
- Colors: HTML color names, #RRGGBB, #RRGGBBAA, #RGB, one hex digit for a gray
  (#8), and transparent; a gradient uses its first color.
- Fonts: sans-serif, SansSerif, Arial, and any unknown name are Liberation
  Sans; Serif and Times Liberation Serif; Monospaced and Courier Liberation
  Mono. Creole markup (**bold**, //italic//, ""mono"", <color:red>,
  <size:16>) styles part of a text.

## Unsupported

- !theme is reported as plantuml-theme-unsupported and the default look is
  drawn; there is no --theme for a PlantUML diagram.
- <style> blocks are reported as plantuml-style-unsupported.

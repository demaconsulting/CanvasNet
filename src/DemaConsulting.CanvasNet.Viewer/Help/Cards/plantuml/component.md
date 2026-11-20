---
title: Component diagrams
summary: Components, interfaces, containers, links with sockets, and notes of a component diagram.
tags:
  - plantuml
  - authoring
see-also:
  - plantuml/deployment
  - plantuml/usecase
  - plantuml/style
  - plantuml/options
  - plantuml/diagnostics
examples:
  - name: component
    kind: source
    language: plantuml
    file: component.puml
    description: A small component diagram with containers, an interface and a socket, a database, and a note.
---

## Supported

- Component diagrams: a diagram of description elements and links without an
  actor or a use case is a component diagram when a statement names a
  component, an interface, or a port (`component`, `interface`, `[Name]`, `()
  Name`), recognized in the order PlantUML tries its diagram types. It is
  drawn with PlantUML's shapes and look and laid out with the dagre layered
  layout instead of Graphviz.
- Components: `component Name`, `[Name]`, `component "Long name" as C` (or
  `component C as "Long name"`), and `[Long name] as C`, drawn as a box with
  PlantUML's component icon; a description in brackets over several lines
  (`component C [` ... `]`) replaces the name.
- Interfaces: `interface Name`, `() Name`, and `() "Long name" as I`, drawn as
  a small circle with the name below it; a name used only by a link is an
  interface.
- Other elements: `node`, `rectangle`, `folder`, `package`, `frame`, `cloud`,
  `database`, `card`, and every symbol of a deployment diagram, each with a
  display name (`as`), stereotypes (`<<name>>`, several, written above the
  name), and `#color` or `#back;line:color;text:color`.
- Containers: `package`, `node`, `folder`, `frame`, `cloud`, `database`,
  `rectangle`, `card`, and `component` with `{ ... }`, nested, named or not,
  with a quoted title, stereotypes, and a `#color`; an element inside a
  container is found from outside by its short name when only one element has
  that name, and a link can start or end at a container.
- Links: `-->` and `..>` (dashed) with an arrowhead, `==>` (bold), `--` and
  `..` without one, the ends of the relationships of a class diagram (`<|--`,
  `*--`, `o--`, ...), the sockets `)--`, `0)--`, `--(`, and `--(0`, a label
  after a colon with its direction mark `<` or `>`, labels in quotes at both
  ends, the length of the arrow (one dash keeps both ends side by side, each
  further dash adds a rank), `-up->`, `-down->`, `-left->`, and `-right->` (or
  `-u->`, `-d->`, `-l->`, `-r->`), and the styles `-[hidden]->`,
  `-[#color]->`, `-[dashed]->`, `-[dotted]->`, `-[bold]->`, and
  `-[thickness=N]->`.
- Notes: `note left`, `right`, `top`, or `bottom of Name`, a note after a
  declaration (`note left : text`), `note on link`, and floating notes (`note
  "text" as N`) linked with `..`, on one line or up to `end note`, with a
  color.
- Page texts: `title`, `header`, `footer`, `caption`, and `legend` ...
  `endlegend`, `left to right direction` and `top to bottom direction`, and
  `scale N`.

## Known differences

- The components, interfaces, containers, notes, and arrows have PlantUML's
  shapes, sizes, and look, but the dagre layout places them differently from
  Graphviz, so the picture has another size; an interface always reserves room
  for its name and stereotypes around its circle.
- Ports and their links, middle decorations of links, roles, qualifiers, and
  stereotypes of links, together, hide, show, remove, restore, archimate
  elements, json and map blocks, URLs, and notes on containers (drawn
  floating) are reported as plantuml-statement-unsupported; an arrow body of
  `~` is not read.
- A stray `}`, an unclosed container, a short name of elements in several
  containers, and a container declared with the name of an element are a
  plantuml-syntax-error, where PlantUML draws a picture that does not show what
  was written.
- A qualified name such as `N.C` from outside its container names the element
  `N.C`, where PlantUML creates a second element with that name.
- A line color or style written `##` (`node N ##blue`) is a
  plantuml-syntax-error, as PlantUML refuses it; write `#line:blue`.

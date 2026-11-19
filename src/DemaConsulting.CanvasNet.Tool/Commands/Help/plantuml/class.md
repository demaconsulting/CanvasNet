---
title: Class diagrams
summary: Classes, members, relationships, packages, notes, and hide and show of a class diagram.
see-also:
  - plantuml/object
  - plantuml/style
  - plantuml/options
  - plantuml/diagnostics
examples:
  - name: class
    kind: source
    language: plantuml
    file: class.puml
    description: A small class diagram with an abstract class, an interface, members, a package, and a note.
---

## Supported

- Class diagrams: a diagram of class declarations and class relationships is a
  class diagram, recognized in the order PlantUML tries its diagram types. It
  is drawn with PlantUML's shapes and look and laid out with the dagre layered
  layout instead of Graphviz.
- Classes: `class`, `abstract class` (or `abstract`), `interface`, `enum`,
  `annotation`, `entity`, `exception`, `struct`, `protocol`, `metaclass`,
  `stereotype`, `record`, and `dataclass`, each with the circled character and
  the color of its kind; a name used only by a relationship is a class.
- Names: a quoted display name with `as`, generic parameters (`class Box<T>`),
  stereotypes (`<<name>>`, several), a circled character of its own
  (`<< (S,#FF7700) name >>`), and colors (`#color`,
  `#back;line:color;text:color`, and `##line`).
- Members: a body `{ ... }` or `Name : member`; a member with parentheses (or
  `{method}`) is a method and the others (or `{field}`) are fields; `-`, `#`,
  `~`, `+`, and `*` are drawn as PlantUML's visibility icons, `{static}` (or
  `{classifier}`) underlines a member, and `{abstract}` writes it in italics.
- Separators: `--`, `..`, `==`, and `__` lines in a body, each with an
  optional title (`.. title ..`).
- Relationships: extension `<|--`, implementation `<|..`, composition `*--`,
  aggregation `o--`, association `-->`, dependency `..>`, plain `--` and `..`,
  and the ends `#`, `x`, `+`, `0`, `(`, and `)`, at either end and in either
  direction, and `extends` and `implements` in a declaration.
- Labels: multiplicities in quotes at both ends (`"1" *-- "many"`), a label
  after a colon, and its direction mark `<` or `>`.
- Layout hints: the length of the arrow (one dash keeps both ends side by
  side, each further dash adds a rank), `-up-`, `-down-`, `-left-`, and
  `-right-` (or `-u-`, `-d-`, `-l-`, `-r-`), and the styles `-[hidden]-`,
  `-[#color]-`, `-[dashed]-`, `-[dotted]-`, `-[bold]-`, and `-[thickness=N]-`.
- Packages: `package` and `namespace` blocks, nested, with a quoted title, a
  `#color`, stereotypes, and the outlines `<<Folder>>`, `<<Rectangle>>`,
  `<<Frame>>`, `<<Node>>`, `<<Cloud>>`, `<<Database>>`, and `<<Card>>`; a
  dotted name (`net.http.Client`) makes its packages, and `set
  namespaceSeparator` changes the separator or turns it off.
- Notes: `note left`, `right`, `top`, or `bottom of Name`, a note after a
  declaration (`note left : text`), `note on link`, and floating notes (`note
  "text" as N`) linked with `..`, on one line or up to `end note`, with a
  color.
- Hide and show: `hide` or `show` `members`, `fields` (or `attributes`),
  `methods`, `circle`, and `stereotype`, with `empty`, for every class or for a
  kind (`interface`), a stereotype (`<<tag>>`), or a name.
- Page texts: `title`, `header`, `footer`, `caption`, and `legend` ...
  `endlegend`, `left to right direction` and `top to bottom direction`, and
  `scale N`.

## Known differences

- The boxes, members, packages, notes, and arrows have PlantUML's shapes,
  sizes, and look, but the dagre layout places them differently from Graphviz,
  so the picture has another size; classes that no relationship connects stand
  in one row.
- Roles, qualifiers, crow's feet, the redefinition ends, middle decorations,
  norank, together, remove, association classes ((A, B) .. C), lollipop
  interfaces, json blocks, hiding single elements or by visibility, and a link
  to a member (A::x, drawn from its class) are reported as
  plantuml-statement-unsupported.
- An actor, a use case, a component, or another element of a description
  diagram in a class diagram is a plantuml-syntax-error, as PlantUML refuses it
  without allowmixing.

### ClassModel

![Class Structure](PlantUmlClassView.svg)

The `ClassModel` unit is the second software unit in the `Class` subsystem. Its source files are
`Class/ClassModel.cs` (the diagram, its rules, its facts, its lowering, and its layout) and
`Class/ClassModel.Elements.cs` (the elements, the member and body rules, and the enumerations).

#### Purpose

`ClassModel` is the immutable result of reading a class or object diagram. It answers the document's questions (title,
facts, reports) and turns the diagram into a scene for every measure or render call through the Svek engine. The
unit's behavior is specified by the `CanvasNetPlantUml-Class-ClassModel-*` requirements in
`docs/reqstream/canvas-net-plant-uml/class/class-model.yaml`.

#### Data Model

- `ClassModel` - the entities, packages (a parent before its children), notes, links, and hide and show rules in text
  order; the page texts; the theme with the diagram's skin parameters; the reports; the location the type was detected
  at; the rank direction, `nodesep`, `ranksep`, and the diagram's own scale. `NodeCount` is the number of entities
  (notes not counted), `EdgeCount` the number of links that do not touch a note (hidden links counted), and
  `SubgraphCount` the number of packages that hold an entity, a package, or a floating note.
- `ClassKind` - class, abstract class, interface, enum, annotation, entity, exception, struct, protocol, metaclass,
  stereotype, record, dataclass, object, and map; `MemberVisibility` - none, public, private, protected, package, and
  mandatory; `ClassPortion` - members, fields, methods, circle, and stereotype.
- `ClassEntity` - the id (the qualified name), the display, the kind, the generic parameters, the stereotypes, the spot
  of a stereotype, the colors, the body lines (members and separators), the map entries, and its package.
- `ClassPackage` - the id, the title, the container symbol, the stereotypes, the fill, and its parent package;
  `ClassNote` - the text, the target entity or link and the position, or floating with its package, and the fill;
  `ClassLink` - the ends, the decorations, the style, the thickness, the color, the length, the direction, the label
  and its direction mark, the end labels, and the weight.
- `ClassHideRule(Show, Gender, EmptyOnly, Portion)` and `ClassPortions(Fields, Methods, Circle, Stereotype)`.

#### Key Methods

- `ClassMember.Parse(raw)` - PlantUML's member rules (`Member`, `VisibilityModifier`): a first character `+ - # ~ *` is
  the visibility of a line of more than two characters whose second character differs from it; `{static}` and
  `{classifier}` make it static (underlined), `{abstract}` abstract (italic), wherever they are written; `<<x>>` is
  written as `«x»`.
- `ClassBody.IsSeparator`, `SeparatorTitle`, `IsMethod`, `Split` - a separator is a line of `--`, `..`, `==`, or `__`
  with an optional title between two runs (`-- title --`); a method is a line with a parenthesis that is not
  `{field}`, or `{method}`; a body without separators splits into fields and methods.
- `PortionsOf(entity)` - applies the rules in order to every entity, to a kind (`class`, `interface`, ...), a
  stereotype (`<<S>>`), or a name; `empty` applies only to a compartment with no lines when the body has no separator.
- `Lower(context)` - builds a `SvekDiagram` for one call: a `SvekCluster` per package under its parent with the image of
  `SvekRenderer.ClusterImage`; a `SvekEntity` per entity with the image of `ClassRenderer.Image` for its portions; the
  floating notes; the links with every setting; the notes attached to entities or links; notes kept in text order; the
  direction, the layout settings, the theme, and the location. Cancellation is observed between elements.
- `Layout(context)` - lowers, runs `SvekLayout.Run`, draws with `SvekRenderer.Render`, and decorates with the page
  texts (`ScenePage.Decorate` with `PageMetrics.Svek`), over the theme's background at the diagram's scale.

#### Error Handling

Null arguments throw `ArgumentNullException`. Layout failures (the work limit) are the located errors of the Svek
layout; a canceled call throws `OperationCanceledException`.

#### Dependencies

The `ClassRenderer` unit, the Svek subsystem (`SvekDiagram` and its elements, `SvekLayout`, `SvekRenderer`), the
Styling subsystem (`SkinTheme`), and the Scenegraph subsystem (`Scene`, `PageTexts`, `ScenePage`, `PageMetrics`).

#### Callers

The `ClassParser` unit builds it; `PlantUmlDocument` measures, renders, and describes it; the tests.

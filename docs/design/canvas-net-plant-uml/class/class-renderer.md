### ClassRenderer

![Class Structure](PlantUmlClassView.svg)

The `ClassRenderer` unit is the third software unit in the `Class` subsystem. Its source files are
`Class/ClassRenderer.cs` (the entry point, `ClassImage`, the colors of the spots and icons, and the painter),
`Class/ClassRenderer.Header.cs` (the class box and its header), `Class/ClassRenderer.Members.cs` (the compartments,
separators, and icons), and `Class/ClassRenderer.Objects.cs` (objects and maps).

#### Purpose

`ClassRenderer` measures an entity with the text layout of a call and returns the Svek entity image that draws it as
PlantUML draws it. The unit's behavior is specified by the `CanvasNetPlantUml-Class-ClassRenderer-*` requirements in
`docs/reqstream/canvas-net-plant-uml/class/class-renderer.yaml`.

#### Data Model

- `ClassImage(width, height, draw)` - an `ISvekEntityImage` whose `Draw(x, y)` returns the scene items of the box at
  that place.
- The look comes from the theme: `Class` (fill `#F1F1F1`, line `#181818` 0.5, corners 5, name 14), `ClassStereotype`
  (italic 12), `ClassAttribute` (members 14, black), `Object`, `ClassAttributeIconSize` (10), and
  `CircledCharacterRadius` (11); `classFontColor` colors only the name.
- The spot colors of PlantUML's default skin: class and entity `#ADD1B2`, abstract class `#A9DCDF`, interface
  `#B4A7E5`, enum `#EB937F`, annotation `#E3664A`, exception `#D94321`, metaclass `#CCCCCC`, stereotype `#FF77FF`,
  dataclass `#7E57C2`, record `#FF8F00`, struct and protocol white; the icon colors of `VisibilityModifier`: private
  `#C82930`/`#F24D5C`, protected `#B38D22`/`#FFFF44`, package `#1963A0`/`#4177AF`, public `#038048`/`#84BE84`.

#### Key Methods

- `Image(entity, portions, theme, text, sink)` - the class box, the object box, or the map of the entity's kind.
- `ClassBoxImage`, `MeasureHeader`, `DrawHeader` - PlantUML's `EntityImageClass` and `EntityImageClassHeader`: the
  header is the circle block (2r + 4 wide, 2r + 10 high) beside the stereotypes (italic, width + 2) above the name
  (width + 6), and the generic box (width + 4), as high as the larger of 32, the texts plus 10, and the generic plus 4,
  the spare height split as `HeaderLayout` splits it; the circle is drawn in the spot color with a `#181818` outline and
  its character in bold monospaced 17; the name is italic for an abstract class and an interface; the generic
  parameters are drawn in a dashed white box over the top-right corner; the box is rounded with the class style.
- `ClassCompartments`, `Blocks`, `MeasureCompartment`, `DrawCompartment` - PlantUML's `MethodsOrFieldsArea` and
  `BodyEnhanced`: without separators the fields then the methods, each in its compartment below a line; with
  separators a compartment per block; an empty compartment is 8 high; the members are 6 from the side and 4 from the
  line, rows as high as the larger of the icon plus 1 and the text; a separator `--` is a line 1 wide, `..` dotted,
  `==` doubled, `__` a thin line; a title is centered on the line between two halves.
- `DrawIcon` - the square (private), diamond (protected), triangle (package), or circle (public) of the visibility in a
  column of the icon size plus 4, hollow for a field and filled for a method (a black circle for the mandatory `*`);
  static members are underlined and abstract ones italic; with icon size 0 the visibility characters are written
  instead.
- `ObjectImage`, `MapImage` - PlantUML's `EntityImageObject` (the name centered with a padding of 2 above a line, the
  fields 6 from the side, 16 high without fields) and `EntityImageMap` (a row per entry, the keys centered in a column
  as wide as the widest key plus 10, the values 5 from theirs, lines one pixel wide).

#### Error Handling

Null arguments throw `ArgumentNullException`. A text that cannot be measured with the bundled fonts is reported through
the sink as the text layout reports it.

#### Dependencies

The `ClassModel` unit (the entity, its portions, the member rules), the Svek subsystem (`ISvekEntityImage`), the
Styling subsystem (`SkinTheme`, `SkinElement`, `ElementStyle`), the Scenegraph subsystem (`ScenePath`, `SceneText`,
`SceneShapes`, `TextLayout`, `TextBlock`), and the core `Canvas` subsystem (`Rgba32`).

#### Callers

`ClassModel.Lower` and the tests.

### StateRenderer

![State Structure](PlantUmlStateView.svg)

The `StateRenderer` unit is the third software unit in the `State` subsystem. Its source file is
`State/StateRenderer.cs` (the entry points, `StateImage`, `StateCompositeImage`, `StateDescription`, and the shapes).

#### Purpose

`StateRenderer` measures a state, a pseudo-state, or the header of a composite state with the text layout of a call
and returns the Svek image that draws it as PlantUML draws it. The unit's behavior is specified by the
`CanvasNetPlantUml-State-StateRenderer-*` requirements in
`docs/reqstream/canvas-net-plant-uml/state/state-renderer.yaml`.

#### Data Model

- `StateImage(width, height, shape, draw)` - an `ISvekEntityImage` whose clip shape (a rectangle, a circle, or a
  diamond) is where the transitions end and whose `Draw(x, y)` returns the scene items at that place.
- `StateCompositeImage` - an `ISvekClusterImage` with the header height (the name with margins of 5, and the
  descriptions below it), the minimum width (the widest text plus 20), and the padding (10); `Draw(box)` returns the
  composite state in the box.
- `StateDescription` - the description lines of a state, each a text block or a rule (a line of four or more dashes,
  `IsRule`), with their width and height.
- The look comes from the theme: `State` (fill `#F1F1F1`, line `#181818` 0.5, corners 25, name 14), `StateAttribute`
  (descriptions 14), `StateStart` and `StateEnd` (`#222222`), `StatePseudo` (choices and histories, line 0.5), and
  `ActivityBar` (forks and joins, `#555555`).

#### Key Methods

- `Image(element, hideEmptyDescription, rankDir, theme, text, sink)` - the image of the element's kind.
- `BoxImage` - PlantUML's `EntityImageState`: a rounded box at least 50 by 50 and as wide as the widest text plus 20,
  the name centered 5 below the top, a line 5 below the name across the box, the descriptions 5 from the left and 5
  below the line; with `hide empty description` and no description, PlantUML's `EntityImageStateEmptyDescription`: a
  box at least 50 by 40 with the name centered and no line. The element's own fill and line color replace the theme's.
- `Circle`, `EndImage` - PlantUML's `EntityImageCircleStart` (a circle of 20) and `EntityImageCircleEnd` (a ring of 22
  around a filled circle of 12); a junction is a filled circle of 12.
- `ChoiceImage` - PlantUML's `EntityImageBranch`: a diamond of 24.
- `BarImage` - PlantUML's `EntityImageSynchroBar`: a bar of 80 by 8 across the ranks (8 by 80 left to right).
- `HistoryImage` - PlantUML's `EntityImagePseudoState` and `EntityImageDeepHistory`: a circle of 22 with `H` or `H*`.
- `CompositeImage`, `Describe`, `RoundedTop` - PlantUML's `Cluster.drawUState` with `RoundedContainer`: the header band
  filled with rounded top corners, the rounded outline (filled whole when the state has its own color), the line under
  the header, the title centered 5 below the top, and the descriptions below the title.

#### Error Handling

Null arguments throw `ArgumentNullException`. A text that cannot be measured with the bundled fonts is reported through
the sink as the text layout reports it.

#### Dependencies

The `StateModel` unit (the element and its kind), the Svek subsystem (`ISvekEntityImage`, `ISvekClusterImage`,
`SvekClip`, `SvekRankDir`), the Styling subsystem (`SkinTheme`, `SkinElement`, `ElementStyle`), the Scenegraph
subsystem (`ScenePath`, `SceneText`, `SceneShapes`, `TextLayout`, `TextBlock`), and the core `Canvas` subsystem
(`Rgba32`).

#### Callers

`StateModel.Lower` and the tests.

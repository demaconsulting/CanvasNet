### SequenceModel

![Sequence Structure](PlantUmlSequenceView.svg)

The `SequenceModel` unit is the second software unit in the `Sequence` subsystem. Its source files are
`Sequence/SequenceModel.cs` (the class `SequenceModel`) and `Sequence/SequenceModel.Elements.cs` (the element records
and enumerations).

#### Purpose

`SequenceModel` is the parsed sequence diagram: what the parser read, in drawing order, and the entry point that lays
it out for a call. It implements `IPlantUmlDiagram`, so the document reads its facts and reports and asks it for a
scene. The unit's behavior is specified by the `CanvasNetPlantUml-Sequence-SequenceModel-*` requirements in
`docs/reqstream/canvas-net-plant-uml/sequence/sequence-model.yaml`.

#### Data Model

- `ParticipantKind` (`Participant`, `Actor`, `Boundary`, `Control`, `Entity`, `Database`, `Collections`, `Queue`) and
  `SequenceParticipant(Code, Display, Kind, Location)` with `Fill` and `Order`.
- `SequenceBox(Title, First, Last, Location)` with `Fill`.
- `ArrowHead` (`None`, `Filled`, `Open`, `HalfTop`, `HalfBottom`, `Cross`) and `ArrowStyle(Dotted, FromHead, ToHead)`
  with `FromCircle`, `ToCircle`, and `Color`.
- `LifeAction` (`Activate`, `Deactivate`, `Destroy`) and `LifeChange(Participant, Action, Fill)`.
- The events (`SequenceEvent` with `Location`): `SequenceMessage(From, To, Label, Arrow)` with `Number`, `Changes`, and
  `IsSelf`; `SequenceLifeEvent(Change)`; `NotePosition` (`Left`, `Right`, `Over`), `NoteShape` (`Note`, `Hexagon`,
  `Box`), and `SequenceNote(Position, First, Last, Text, Shape)` with `Fill` and `OnMessage`;
  `SequenceGroupStart(Keyword, Label)` with `Fill`; `SequenceGroupElse(Label)`; `SequenceGroupEnd`;
  `SequenceDivider(Text)`; `SequenceDelay(Text)`; `SequenceSpace(Height)`.
- `SequenceModel` - `Participants` (sorted), `Boxes`, `Events`, `Page` (the page texts), `Theme` (with the skin
  parameters applied), `KeywordLocation`, `Scale`, `Reports`, and the facts `Title`, `NodeCount` (participants),
  `EdgeCount` (messages), and `SubgraphCount` (boxes).

#### Key Methods

- `Layout(context)` - `SequenceLayout.Run`, then `SequenceRenderer.Render`, then `ScenePage.Decorate` with the page
  texts, and the scene gets the theme's background and the diagram's scale. Nothing is kept between calls.

#### Error Handling

The constructor and `Layout` throw `ArgumentNullException` for null arguments; the layout's limit and cancellation
exceptions pass through.

#### Dependencies

The Styling subsystem (`SkinTheme`), the Scenegraph subsystem (`Scene`, `PageTexts`, `ScenePage`), `SequenceLayout`,
and `SequenceRenderer`.

#### Callers

`SequenceParser` creates the model; `PlantUmlDocument` reads its facts and reports and calls `Layout`.

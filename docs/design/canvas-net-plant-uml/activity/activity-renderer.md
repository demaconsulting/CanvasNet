### ActivityRenderer

![Activity Structure](PlantUmlActivityView.svg)

The `ActivityRenderer` unit is the fourth software unit in the `Activity` subsystem. Its source file is
`Activity/ActivityRenderer.cs`, which defines the internal static class `ActivityRenderer`.

#### Purpose

`ActivityRenderer` turns the placed shapes of an activity diagram into the items of a scene that the painter paints.
The unit's behavior is specified by the `CanvasNetPlantUml-Activity-ActivityRenderer-*` requirements in
`docs/reqstream/canvas-net-plant-uml/activity/activity-renderer.yaml`.

#### Data Model

The unit holds no state. It reads an `ActivityGeometry` and writes a `Scene` of `ScenePath` and `SceneText` items.

#### Key Methods

- `Render(model, geometry)` - the scene of the geometry's size with the theme's background and one item per drawn
  shape, in paint order.
- `ToItem(shape)` - a rectangle (with its corner radius), an ellipse, a polygon or path (moved to the shape's origin),
  or a line becomes a `ScenePath` with its fill, stroke, role, and location; a text becomes its `SceneText` moved to
  the shape's place; empty space and shapes without paint become nothing.

The roles of the items name what they draw: `activity` and `activity-text`, `start`, `stop`, `end`, `connector` and
`connector-text`, `diamond` and `diamond-text`, `bar` and `bar-text`, `split`, `arrow`, `arrow-head`, and `arrow-text`,
`note` and `note-text`, `partition` and `partition-text`, `lane`, `lane-divider`, and `lane-title`.

#### Error Handling

Null arguments throw `ArgumentNullException`.

#### Dependencies

The Scenegraph subsystem (`Scene`, `ScenePath`, `SceneText`, `SceneShapes`, `ScenePage`) and the layout's shapes.

#### Callers

`ActivityModel.Layout`; the tests.

## Styling

![Styling Structure](PlantUmlStylingView.svg)

### Overview

The `Styling` subsystem (namespace `DemaConsulting.CanvasNet.PlantUml.Styling`, folder
`src/DemaConsulting.CanvasNet.PlantUml/Styling/`) holds the look of the diagrams: PlantUML's default skin and the
`skinparam` statements that change it. It is **internal**. It contains two units:

- **SkinTheme** - the internal record `SkinTheme` with the `SkinElement` enumeration and the `ElementStyle` record
  (`SkinTheme.cs`): the style of every element and the document and sequence settings, with the values of PlantUML's
  default skin; see *SkinTheme Unit Design*
- **SkinParams** - the internal static `SkinParams` with the `SkinParamEntry` record (`SkinParams.cs`) and PlantUML's
  colors (`SkinParams.Colors.cs`): the reading of `skinparam` statements, the supported subset, and the reports of what
  has no effect; see *SkinParams Unit Design*

The subsystem's namespace documentation is in `Styling/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetPlantUml-Styling-*` requirements in `docs/reqstream/canvas-net-plant-uml/styling.yaml`. The SysML2 model names
the subsystem `PlantUmlStyling`.

### Interfaces

**Exposed** (internal only): `SkinTheme.Default`, the indexer by `SkinElement`, `SkinTheme.With`, the settings
(`Background`, `Shadowing`, `MaxMessageSize`, `MessageAlign`, `ResponseBelowArrow`, `Footbox`, `ArrowThickness`), the
color constants and `SkinTheme.ParticipantElements`; `SkinParams.TryRead`, `SkinParams.TrySkipStyle`,
`SkinParams.Apply`, `SkinParams.TryParseColor`, and `SkinParams.IsKeyword`.

**Consumed**: the core `Canvas` subsystem (`Rgba32`, `CssColor` for the color names and hexadecimal forms), the
Syntax subsystem's `SourceStatement`, and the root namespace's `PlantUmlLocation`, `PlantUmlDiagnosticCodes`, and
`PlantUmlPendingReport`.

**Consumers**: the diagram parsers read the `skinparam` and `<style>` statements and apply them to the default theme;
the diagram layouts and renderers read the styles; the `TextLayout` unit of the Scenegraph subsystem reads the colors
of the `<color>` tag with `TryParseColor`.

### Design

A diagram parser collects the `SkinParamEntry` records of the diagram in text order with `TryRead` and applies them
once with `Apply` to `SkinTheme.Default`, keeping the warnings as pending reports. `Apply` tries each entry as a
document or sequence setting, then as a property of the longest element prefix of its name, and folds the change into a
new theme with `With`, so the default theme is never changed and a later value wins.

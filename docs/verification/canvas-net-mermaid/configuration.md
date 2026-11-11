## Configuration Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Configuration` subsystem (internal
namespace `DemaConsulting.CanvasNet.Mermaid.Configuration`), containing four units: `MermaidConfigResolver` (the
effective configuration of a call and its located reports, with the typed view `MermaidConfig`), `ThemeResolver` (the
five built-in themes and the flowchart, sequence, class, state, and entity relationship style sheets
`FlowchartStyles`, `SequenceStyles`, `ClassStyles`, `StateStyles`, and `ErStyles`), `ColorMath` (the port of the
khroma color functions), and `StyleDeclarations` (the CSS subset of style statements and `themeCSS`).

### Verification Approach

The `Configuration` subsystem is verified at two tiers. The unit tests (see the unit verification designs under
`configuration/`) verify each unit in isolation, most of them against vectors recorded from the pinned Mermaid and
khroma. The subsystem-level scenarios in `ConfigurationTests` verify the units working together on whole diagram
texts: the `Preprocessor` of the `Syntax` subsystem reads the frontmatter and the directives,
`MermaidConfigResolver.Resolve` merges them over the site configuration and computes the theme with `ThemeResolver` and
`ColorMath`, and `FlowchartStyles.Create` builds the flowchart's paint from the theme variables and the `themeCSS`
parsed by `StyleDeclarations` (`SequenceStyles.Create` the sequence diagram's paint, with the sequence section of the
configuration, `ClassStyles.Create` the class diagram's paint, with the class section and the state spacing of the
configuration, and `StateStyles.Create` and `ErStyles.Create` the state and entity relationship diagrams' paint, with
the state and `er` sections of the configuration). Two scenarios go through the public `MermaidDocument` to verify
that the configuration of the text drives the diagram detection and that its warnings are delivered by a render
call.

The expectations are the values Mermaid gives for the same input: the theme that wins, the node fill of each theme
(its `mainBkg`), the dark theme's derived `mainBkg` (`#1f2020`), the edge and marker colors from an overridden
`lineColor`, a `themeCSS` declaration in the resolved rule, and the code and location of each warning. No golden
images are used. The subsystem uses no mocking or stubbing; the `Preprocessor`, the report sink, and the core
`CssColor` are real. The subsystem is internal, so its tests reach it through the test assembly's
`InternalsVisibleTo` access.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fixtures**: None at the subsystem level (the unit tests read `Fixtures/upstream-vectors/themes.json`)
- **Isolation**: Each test builds its own text, options, and report sink; no files are read

### Acceptance Criteria

The `Configuration` subsystem's verification passes when every subsystem-level scenario below and every unit-level
scenario in the four unit verification documents passes without error or unexpected exception.

### Test Scenarios

#### CanvasNetMermaid-Configuration-Precedence: Layers Resolve In Mermaid's Order

**Tests**: `ConfigurationTests.Configuration_ResolveThemeAndStyles_FullText`,
`ConfigurationTests.Configuration_ThemeOption_NodeFill`,
`ConfigurationTests.Configuration_ResolveSequenceSectionAndStyles_FullText`,
`ConfigurationTests.Configuration_ResolveClassSectionAndStyles_FullText`,
`ConfigurationTests.Configuration_ResolveStateAndErSectionsAndStyles_FullText`,
`ConfigurationTests.Configuration_ResolvePieAndGanttSectionsAndStyles_FullText`

Asserts that a text whose frontmatter names the `forest` theme and whose directive names `dark`, rendered with the theme
option `neutral`, resolves to the `dark` theme with the frontmatter's flowchart curve, and that the theme option alone
selects each of the five themes; that a sequence diagram whose frontmatter names the `forest` theme and sets
`sequence.mirrorActors: false` resolves to that theme and setting, while its `sequence.noteAlign: 5` (a number) is
reported as `mermaid-config-invalid` at line 6 and the default `center` is kept; that a class diagram whose
frontmatter names the `forest` theme and sets `class.padding: 5` and `state.nodeSpacing: 80` resolves to that theme,
a class padding of 5, and a node spacing of 80 taken from the state section, as Mermaid's class renderer reads it; and
that a state diagram whose frontmatter names the `forest` theme and sets `state.rankSpacing: 70` resolves to a rank
spacing of 70, and an entity relationship diagram whose frontmatter names the `dark` theme and sets
`er.minEntityWidth: 30` resolves to a minimum entity width of 30, each with the node fill of its resolved theme's
`mainBkg`; and that a pie chart whose frontmatter names the `forest` theme and sets `pie.donutHole: 0.4` resolves to a
donut hole of 0.4, and a gantt chart whose frontmatter names the `dark` theme and sets `gantt.barHeight: 30` resolves
to a bar height of 30.

#### CanvasNetMermaid-Configuration-Reports: Configuration Problems Are Reported And Ignored

**Tests**: `ConfigurationTests.Configuration_ResolveThemeAndStyles_FullText`,
`ConfigurationTests.Configuration_ResolveClassSectionAndStyles_FullText`,
`ConfigurationTests.Configuration_ResolveStateAndErSectionsAndStyles_FullText`,
`ConfigurationTests.Configuration_ResolvePieAndGanttSectionsAndStyles_FullText`,
`ConfigurationTests.Configuration_Document_RenderDeliversConfigurationWarnings`,
`ConfigurationTests.Configuration_Document_UnsupportedType_ConfigurationSilent`

Asserts that a frontmatter value of the wrong type (`rankSpacing: wide`) is reported as `mermaid-config-invalid` at its
key and the default is kept, that a class value of the wrong type (`class.hideEmptyMembersBox: maybe`) is the one
warning of a class diagram text, `mermaid-config-invalid` at its line 6, with the default `false` kept, that in the
state and entity relationship texts above `state.titleTopMargin: yes` and `er.entityPadding: wide` are each the one
warning of their text, `mermaid-config-invalid` at line 6, with the defaults 25 and 15 kept and no unsupported-feature
report, that in the pie and gantt texts above `pie.textPosition: far` and `gantt.barGap: wide` are each the one warning
of their text, `mermaid-config-invalid` at line 6, with the defaults 0.75 and 4 kept and no unsupported-feature report,
that a secure key in the frontmatter of a flowchart is delivered by a render call as `mermaid-config-secure-key` at its
location, and that the secure and unknown keys of a user journey (not drawn by this version) produce no warning, only
the unsupported diagram report.

#### CanvasNetMermaid-Configuration-Themes: Five Themes With Mermaid's Variables

**Tests**: `ConfigurationTests.Configuration_ThemeOption_NodeFill`,
`ConfigurationTests.Configuration_ResolveThemeAndStyles_FullText`

Asserts the node fill of each of the five themes (`#ECECFF`, `#fff4dd`, `#1f2020`, `#cde498`, `#eee`) and that the
dark theme named by a directive gives its `mainBkg` (`#1f2020`) while the directive overrides `primaryColor` and
`lineColor`.

#### CanvasNetMermaid-Configuration-StyleSheet: Diagram Paint From The Theme And ThemeCSS

**Tests**: `ConfigurationTests.Configuration_ResolveThemeAndStyles_FullText`,
`ConfigurationTests.Configuration_ThemeOption_NodeFill`,
`ConfigurationTests.Configuration_ResolveSequenceSectionAndStyles_FullText`,
`ConfigurationTests.Configuration_ResolveClassSectionAndStyles_FullText`,
`ConfigurationTests.Configuration_ResolveStateAndErSectionsAndStyles_FullText`,
`ConfigurationTests.Configuration_ResolvePieAndGanttSectionsAndStyles_FullText`

Asserts that the flowchart's edge and marker colors follow the overridden `lineColor`, that the node fill of the
style sheet is the theme's, and that a `themeCSS` rule for `.cluster rect` is applied to that selector's declarations;
that for the sequence diagram above a `themeCSS` rule for `.note` given in a directive overrides the theme's note
fill, while the participant fill is the `forest` theme's `actorBkg`; that for the class diagram above a
`themeCSS` rule `.divider { stroke: #ff0000 }` given in a directive makes the class box dividers red, while the class
box fill is the `forest` theme's `mainBkg`; and that for the state and entity relationship diagrams above the
directive rules `.transition { stroke: #ff0000 }` and `.relationshipLine { stroke: #0000ff }` set the stroke of their
selectors without a report, the node fill of each sheet is its theme's `mainBkg`, neither sheet centers the SVG text
of a node, and the entity relationship sheet's root text is 16 pixels; and that for the pie and gantt charts above the
directive rules `.pieOuterCircle { stroke: #ff0000 }` and `.task0 { fill: #0000ff }` make the outer circle red and the
fill a plain bar computes blue without a report.

#### CanvasNetMermaid-Configuration-Declarations: CSS Declarations Take Effect

**Tests**: `ConfigurationTests.Configuration_ResolveThemeAndStyles_FullText`

Asserts that the `stroke-dasharray` declaration of a `themeCSS` rule given in a directive is read and resolved for its
selector.

#### CanvasNetMermaid-Configuration-Detection: The Text Configuration Chooses The Detector

**Tests**: `ConfigurationTests.Configuration_Document_TextConfigurationDrivesDetection`

Asserts that a flowchart whose frontmatter sets `flowchart.defaultRenderer: elk` is detected as `flowchart-elk`, is a
supported flowchart, and that the same diagram without the configuration is detected as `flowchart-v2`.

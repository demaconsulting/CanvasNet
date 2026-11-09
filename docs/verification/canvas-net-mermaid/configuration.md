## Configuration Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Configuration` subsystem (internal
namespace `DemaConsulting.CanvasNet.Mermaid.Configuration`), containing four units: `MermaidConfigResolver` (the
effective configuration of a call and its located reports, with the typed view `MermaidConfig`), `ThemeResolver` (the
five built-in themes and the flowchart style sheet `FlowchartStyles`), `ColorMath` (the port of the khroma color
functions), and `StyleDeclarations` (the CSS subset of style statements and `themeCSS`).

### Verification Approach

The `Configuration` subsystem is verified at two tiers. The unit tests (see the unit verification designs under
`configuration/`) verify each unit in isolation, most of them against vectors recorded from the pinned Mermaid and
khroma. The subsystem-level scenarios in `ConfigurationTests` verify the units working together on whole diagram
texts: the `Preprocessor` of the `Syntax` subsystem reads the frontmatter and the directives,
`MermaidConfigResolver.Resolve` merges them over the site configuration and computes the theme with `ThemeResolver` and
`ColorMath`, and `FlowchartStyles.Create` builds the flowchart's paint from the theme variables and the `themeCSS`
parsed by `StyleDeclarations`. Two scenarios go through the public `MermaidDocument` to verify that the configuration
of the text drives the diagram detection and that its warnings are delivered by a render call.

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
`ConfigurationTests.Configuration_ThemeOption_NodeFill`

Asserts that a text whose frontmatter names the `forest` theme and whose directive names `dark`, rendered with the theme
option `neutral`, resolves to the `dark` theme with the frontmatter's flowchart curve, and that the theme option alone
selects each of the five themes.

#### CanvasNetMermaid-Configuration-Reports: Configuration Problems Are Reported And Ignored

**Tests**: `ConfigurationTests.Configuration_ResolveThemeAndStyles_FullText`,
`ConfigurationTests.Configuration_Document_RenderDeliversConfigurationWarnings`,
`ConfigurationTests.Configuration_Document_UnsupportedType_ConfigurationSilent`

Asserts that a frontmatter value of the wrong type (`rankSpacing: wide`) is reported as `mermaid-config-invalid` at
its key and the default is kept, that a secure key in the frontmatter of a flowchart is delivered by a render call as
`mermaid-config-secure-key` at its location, and that the secure and unknown keys of a class diagram (not drawn by
this version) produce no warning, only the unsupported diagram report.

#### CanvasNetMermaid-Configuration-Themes: Five Themes With Mermaid's Variables

**Tests**: `ConfigurationTests.Configuration_ThemeOption_NodeFill`,
`ConfigurationTests.Configuration_ResolveThemeAndStyles_FullText`

Asserts the node fill of each of the five themes (`#ECECFF`, `#fff4dd`, `#1f2020`, `#cde498`, `#eee`) and that the
dark theme named by a directive gives its `mainBkg` (`#1f2020`) while the directive overrides `primaryColor` and
`lineColor`.

#### CanvasNetMermaid-Configuration-StyleSheet: Flowchart Paint From The Theme And ThemeCSS

**Tests**: `ConfigurationTests.Configuration_ResolveThemeAndStyles_FullText`,
`ConfigurationTests.Configuration_ThemeOption_NodeFill`

Asserts that the flowchart's edge and marker colors follow the overridden `lineColor`, that the node fill of the
style sheet is the theme's, and that a `themeCSS` rule for `.cluster rect` is applied to that selector's declarations.

#### CanvasNetMermaid-Configuration-Declarations: CSS Declarations Take Effect

**Tests**: `ConfigurationTests.Configuration_ResolveThemeAndStyles_FullText`

Asserts that the `stroke-dasharray` declaration of a `themeCSS` rule given in a directive is read and resolved for its
selector.

#### CanvasNetMermaid-Configuration-Detection: The Text Configuration Chooses The Detector

**Tests**: `ConfigurationTests.Configuration_Document_TextConfigurationDrivesDetection`

Asserts that a flowchart whose frontmatter sets `flowchart.defaultRenderer: elk` is detected as `flowchart-elk`, is a
supported flowchart, and that the same diagram without the configuration is detected as `flowchart-v2`.

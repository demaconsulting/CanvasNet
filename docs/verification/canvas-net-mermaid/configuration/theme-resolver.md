### ThemeResolver Unit Verification Design

This document describes the unit-level verification strategy for the `ThemeResolver` unit in the
`DemaConsulting.CanvasNet.Mermaid.Configuration` namespace: the internal static `ThemeResolver` (`GetThemeVariables`
and `IsTheme`), its JavaScript object model `ThemeObject`, and the flowchart style sheet `FlowchartStyles`.

#### Verification Approach

The themes are verified against the theme variables recorded from the pinned Mermaid in
`Fixtures/upstream-vectors/themes.json`: for each of the five themes, without overrides, with overrides of base and
derived variables, and in dark mode, every variable must have the same text as Mermaid's (colors are compared as
strings, because the text reaches the style sheet). Hand-written tests verify the derivation of an overridden base
color, the override that wins, the bounds, the theme names, and the JavaScript semantics of `ThemeObject`. The style
sheet is verified by building `FlowchartStyles` from configurations resolved as Mermaid resolves them and asserting the
paints of the default theme (as Mermaid's flowchart CSS gives them), the effect of `themeCSS`, and the dropping of an
invalid declaration. The unit is reached through the test assembly's `InternalsVisibleTo` access; `ColorMath`,
`StyleDeclarations`, and `MermaidConfigResolver` are real, so no mocking or stubbing is required.

Unit tests reside in `ThemeResolverTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Configuration` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fixtures**: `Fixtures/upstream-vectors/themes.json` (the site theme cases), generated from the pinned Mermaid by
  `benchmarks/visual-diff/mermaid/gen-theme-vectors.mjs`
- **Isolation**: Pure computations; the fixture is read only

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any theme variable whose text differs from Mermaid's, and any paint other than the asserted one, constitutes
a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Configuration-ThemeResolver-FiveThemes: Every Variable Equals Mermaid's

**Tests**: `ThemeResolverTests.ThemeResolver_GetThemeVariables_MatchesMermaid`,
`ThemeResolverTests.ThemeResolver_GetThemeVariables_DefaultTheme_FlowchartColors`

Asserts, for the 15 recorded site cases (five themes, each plain, with theme variables, and in dark mode), that every
variable equals Mermaid's, and the well-known flowchart colors of the default theme (`#ECECFF`, `#9370DB`, `#ffffde`,
and the derived `hsl(240, 60%, 86.2745098039%)`).

##### CanvasNetMermaid-Configuration-ThemeResolver-Overrides: Overrides Are Derived And Kept

**Tests**: `ThemeResolverTests.ThemeResolver_GetThemeVariables_BaseOverride_DerivesAndKeepsOverrides`,
`ThemeResolverTests.ThemeResolver_GetThemeVariables_NonObjectOverrides_Ignored`

Asserts that a `primaryColor` override of the base theme becomes `mainBkg` and is derived into
`primaryBorderColor` (`hsl(0, 60%, 40%)`) while an overridden `nodeBorder` is kept, and that a string given as the
overrides gives the same variables as no overrides.

##### CanvasNetMermaid-Configuration-ThemeResolver-JavaScriptSemantics: The Theme Object Behaves As JavaScript

**Tests**: `ThemeResolverTests.ThemeObject_JavaScriptSemantics`

Asserts that `||` replaces a zero, `??` replaces undefined but keeps an empty string, a missing key and a member of a
string are undefined, the keys keep their insertion order, and the JSON form holds the values.

##### CanvasNetMermaid-Configuration-ThemeResolver-ThemeNames: Exactly Five Themes

**Tests**: `ThemeResolverTests.ThemeResolver_IsTheme_KnowsFiveThemes`

Asserts that every public theme name is a theme, that the newer Mermaid themes, `Dark`, and a number are not, and that
computing `neo` throws `ArgumentException`.

##### CanvasNetMermaid-Configuration-ThemeResolver-Bounded: Bounded Loops And Identified Failures

**Tests**: `ThemeResolverTests.ThemeResolver_GetThemeVariables_HugeColorLimit_IsBounded`,
`ThemeResolverTests.ThemeResolver_GetThemeVariables_NotAColor_ThrowsWithValue`

Asserts that a `THEME_COLOR_LIMIT` of 1e12 returns promptly with a `ColorFormatException` on the undefined scale color
(as khroma fails in Mermaid), and that a `primaryColor` of `nonsense` fails with that value.

##### CanvasNetMermaid-Configuration-ThemeResolver-FlowchartStyles: Mermaid's Flowchart Paint

**Tests**: `ThemeResolverTests.FlowchartStyles_Create_DefaultTheme_ResolvesPaint`

Asserts the default theme's node paint (`#ECECFF` fill, `#9370DB` stroke, width 1), cluster paint, edge and marker
color (`#333333`), edge widths (1 and 3.5), node label and title fonts (16 and 18 pixels), the edge label background,
and the faded `.labelBkg` background.

##### CanvasNetMermaid-Configuration-ThemeResolver-ThemeCss: ThemeCSS Overrides Known Selectors

**Tests**: `ThemeResolverTests.FlowchartStyles_Create_ThemeCss_OverridesKnownSelectors`

Asserts that a `.node rect` rule of `themeCSS` replaces the fill and stroke width and keeps the stroke, that an
important theme declaration of `.root .anchor path` stays in effect, and that the unknown selector `.mine` is reported
once as `mermaid-theme-css-unsupported` at the directive.

##### CanvasNetMermaid-Configuration-ThemeResolver-InvalidDeclarations: Invalid Declarations Are Dropped

**Tests**: `ThemeResolverTests.FlowchartStyles_Create_InvalidVariable_DropsDeclaration`

Asserts that a site theme variable `nodeBorder` of `nonsense` is kept as a variable, but leaves the node stroke
undeclared while the node fill is unchanged.

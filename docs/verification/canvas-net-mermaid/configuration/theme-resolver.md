### ThemeResolver Unit Verification Design

This document describes the unit-level verification strategy for the `ThemeResolver` unit in the
`DemaConsulting.CanvasNet.Mermaid.Configuration` namespace: the internal static `ThemeResolver` (`GetThemeVariables`
and `IsTheme`), its JavaScript object model `ThemeObject`, and the style sheets `FlowchartStyles` and `SequenceStyles`
with their shared base `DiagramStyles`.

#### Verification Approach

The themes are verified against the theme variables recorded from the pinned Mermaid in
`Fixtures/upstream-vectors/themes.json`: for each of the five themes, without overrides, with overrides of base and
derived variables, and in dark mode, every variable must have the same text as Mermaid's (colors are compared as
strings, because the text reaches the style sheet). Hand-written tests verify the derivation of an overridden base
color, the override that wins, the bounds, the theme names, and the JavaScript semantics of `ThemeObject`. The style
sheet is verified by building `FlowchartStyles` from configurations resolved as Mermaid resolves them and asserting the
paints of the default theme (as Mermaid's flowchart CSS gives them), the effect of `themeCSS`, and the dropping of an
invalid declaration. The sequence style sheet is verified the same way with `SequenceStyles`: the shared global rules,
the cascade over the attributes Mermaid writes, every theme's variables, `themeCSS`, and the drop-shadow filters. The
unit is reached through the test assembly's `InternalsVisibleTo` access; `ColorMath`,
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

##### CanvasNetMermaid-Configuration-ThemeResolver-SequenceStyleSheet: Mermaid's Sequence Paint

**Tests**: `ThemeResolverTests.DiagramStyles_Create_GlobalRules_SharedByFlowchartAndSequence`,
`ThemeResolverTests.SequenceStyles_Create_DefaultTheme_ResolvesCascade`,
`ThemeResolverTests.SequenceStyles_Create_EachTheme_UsesThemeVariables`,
`ThemeResolverTests.SequenceStyles_Create_ThemeCss_OverridesKnownSelectors`,
`ThemeResolverTests.SequenceStyles_TryParseFilter_ParsesDropShadow`

Asserts that the flowchart and sequence style sheets start with the same global rules, selector for selector and
declaration for declaration, from the root rule to `p`; that in the default theme a participant takes the theme's
`#ECECFF` fill and `#9370DB` stroke over its attributes, a note `#fff5ad` and `#aaaa33`, and an element of a class
without a rule keeps its attributes; that the `line` rule widens the lifeline and the block lines to 2 pixels in
`#9370DB` (the block lines dashed 2, 2), while the message rules give 1.5 pixels in `#333` (dashed 2, 2 when dotted);
that a box gets the drop shadow `1px 2px 2px` in `rgb(185, 185, 185)` and the node border, the menu panel its own
shadow, the label box none, the arrowhead, crosshead, and sequence-number markers `#333`, the sequence numbers white,
and a message text `#333` over its attribute while an unknown class keeps the attribute; that for each of the five
themes (a theory) the participant, lifeline, note, and activation paint and the box shadow come from the theme's
variables; that a `themeCSS` rule for `.actor` and `.actor-line` overrides the participant fill and the lifeline width
while the flowchart selector `.node rect` is reported once as `mermaid-theme-css-unsupported`; and that `none` and one
`drop-shadow()` (with a color first or last, two or three lengths, a negative offset) are read, while four lengths,
one length, non-zero numbers without units, a negative blur, a color between the lengths, a second filter function, a `url()`,
and `blur()` are not (a theory of 14 cases).

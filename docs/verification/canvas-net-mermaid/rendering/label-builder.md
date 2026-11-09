### LabelBuilder Unit Verification Design

This document describes the unit-level verification strategy for the `LabelBuilder` unit in the
`DemaConsulting.CanvasNet.Mermaid.Rendering` namespace: the internal `LabelBuilder` class (the entry point and metrics
in `LabelBuilder.cs`, HTML labels in `LabelBuilder.Html.cs`, markdown and plain strings in `LabelBuilder.Markdown.cs`
and `LabelBuilder.MarkdownLexer.cs`, and SVG labels in `LabelBuilder.Svg.cs`) and its records `LabelRequest` and
`LabelLayout`.

#### Verification Approach

The unit is verified at two levels. The vector tests take the label cases of the upstream vectors
`Fixtures/upstream-vectors/shapes.json` (recorded from the pinned Mermaid in headless Chrome with the bundled Liberation
Sans; the fixture is described in the _ShapeLibrary Unit Verification Design_) and assert that every HTML label breaks
into Mermaid's lines with the same line tops and starts (within 0.02 pixel) and every SVG label into lines with the
same text. The other tests are analytic: the markdown and plain-string functions are compared with the HTML and the
lines Mermaid makes of the same text, and the layout rules are checked with numbers derived from the CSS and the font
metrics (a 16 pixel font gives 24 pixel HTML line boxes and SVG baselines 17.6 pixels apart; a lone picture of natural
size 40 by 30 is 80 by 60 pixels). The builder is created with the font resolver of the painter (the bundled fonts), a
report sink whose warnings are delivered to a list, and, for pictures, a `NodeImages` over a test resolver; no mocking
of the unit's own collaborators is required, and the internal types are reached through the test assembly's
`InternalsVisibleTo` access.

Unit tests reside in `LabelBuilderTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Rendering` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: A test image resolver that returns pictures of a fixed size for the label picture
- **Fonts**: The bundled Liberation fonts
- **Files**: `Fixtures/upstream-vectors/shapes.json` of the test project (copied to the output folder)
- **Isolation**: Each test creates its own builder and sink; the vectors are read once and not changed

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A line break, line position, box, run style, decoded text, or report that differs from Mermaid's or from the
hand-derived value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Rendering-LabelBuilder-HtmlLayout: HTML Labels Lay Out As Mermaid's Labels

**Tests**: `LabelBuilderTests.LabelBuilder_Build_UpstreamVectorHtml_MatchesLines`,
`LabelBuilderTests.LabelBuilder_Build_HtmlLabelWiderThanWrappingWidth_WrapsAtWidth`,
`LabelBuilderTests.LabelBuilder_Build_HtmlLabelWithLongWord_WidensToTheWord`,
`LabelBuilderTests.LabelBuilder_Build_EmptyLabel_HasEmptyBox`,
`LabelBuilderTests.LabelBuilder_ToScene_HtmlLabel_GivesTextLines`

Asserts that every HTML label of the vectors has Mermaid's line tops and starts; that a label wider than the wrapping
width is laid out in a box of exactly 200 pixels, three lines of 24 pixels, each centered; that a word longer than the
wrapping width widens the box to the word and breaks after a hyphen; that an empty label, HTML or SVG, measures an
empty box; and that the lines of an HTML label become scene text on their baselines with the id of their element.

##### CanvasNetMermaid-Rendering-LabelBuilder-SvgLayout: SVG Labels Lay Out As Mermaid's Text Lines

**Tests**: `LabelBuilderTests.LabelBuilder_Build_UpstreamVectorSvg_MatchesLines`,
`LabelBuilderTests.LabelBuilder_Build_SvgLabel_FollowsTspanRules`,
`LabelBuilderTests.LabelBuilder_Build_SvgLabelWithLongWord_SplitsAtCharacters`,
`LabelBuilderTests.LabelBuilder_Build_SvgLabelWithBackground_GrowsTheBox`

Asserts that every SVG label of the vectors breaks into Mermaid's lines with the same text; that the lines of a 16 pixel
SVG label have baselines at 16 and 33.6 pixels, are centered on x 0, and have a box from the rounded ascent to the
rounded descent; that a word wider than the wrapping width is split at characters with every piece kept; and that a
background grows the box by 2 pixels on every side.

##### CanvasNetMermaid-Rendering-LabelBuilder-Markdown: Markdown And Plain Strings Become Mermaid's Text

**Tests**: `LabelBuilderTests.LabelBuilder_MarkdownToHtml_Markdown_GivesMermaidHtml`,
`LabelBuilderTests.LabelBuilder_MarkdownToHtml_NoAutoWrap_UsesNoBreakSpaces`,
`LabelBuilderTests.LabelBuilder_MarkdownToLines_StyledText_GivesStyledWords`,
`LabelBuilderTests.LabelBuilder_NonMarkdownToHtml_PlainText_GivesParagraph`,
`LabelBuilderTests.LabelBuilder_NonMarkdownToLines_TagsAndBreaks_SplitAsMermaid`,
`LabelBuilderTests.LabelBuilder_Dedent_IndentedLines_RemovesCommonIndentation`

Asserts that markdown becomes the HTML Mermaid makes of it, with no-break spaces when it does not wrap automatically;
that markdown becomes lines of styled words for SVG labels (escapes not drawn); that a plain string becomes a paragraph
whose new lines are line breaks (and an empty string nothing) and lines of words with the tags kept as words; and that
the common indentation is removed as `ts-dedent` removes it.

##### CanvasNetMermaid-Rendering-LabelBuilder-HtmlSubset: The HTML Subset Is Styled And The Rest Reported

**Tests**: `LabelBuilderTests.LabelBuilder_Build_StyledElements_StyleTheRuns`,
`LabelBuilderTests.LabelBuilder_Build_UnsupportedElement_ReportsAndKeepsText`,
`LabelBuilderTests.LabelBuilder_Build_HtmlIcon_IsRemovedSilently`

Asserts that `b`, `i`, and a span with a color and a 20 pixel size style their runs (the line box growing to 30
pixels), that two `u` elements are dropped with their text kept and reported once as `mermaid-html-tag-ignored` at the
label's location, and that an icon name is removed from an HTML label without a report.

##### CanvasNetMermaid-Rendering-LabelBuilder-Entities: Entities Are Decoded Before Measuring

**Tests**: `LabelBuilderTests.LabelBuilder_DecodeEntities_Placeholders_BecomeReferences`,
`LabelBuilderTests.LabelBuilder_Build_HtmlLabelWithReferences_DecodesThem`

Asserts that Mermaid's entity placeholders become character references, and that the references of an HTML label are
decoded and measured as characters on one line of 24 pixels.

##### CanvasNetMermaid-Rendering-LabelBuilder-MissingGlyphs: Missing Glyphs Are Reported Once

**Tests**: `LabelBuilderTests.LabelBuilder_Build_MissingGlyph_ReportsOnce`

Asserts that a label with a character from a private use plane, twice, is reported once as
`mermaid-text-missing-glyph`, naming the code point and located at the label's line.

##### CanvasNetMermaid-Rendering-LabelBuilder-LabelImages: A Lone Label Picture Is Five Font Sizes Wide

**Tests**: `LabelBuilderTests.LabelBuilder_Build_LoneImage_IsFiveEmWide`

Asserts that a picture alone in an HTML label at a 16 pixel configured font size, with a natural size of 40 by 30, is
80 by 60 pixels, is the label's box, and is part of the label's scene with its link.

### LabelBuilder

![Rendering Structure](MermaidRenderingView.svg)

The `LabelBuilder` unit is the fourth software unit in the `Rendering` subsystem. It is the internal sealed partial
class `LabelBuilder` in six source files - `LabelBuilder.cs` (the entry point, the fonts, the metrics, the
missing-glyph report, and the records `LabelFont`, `LabelRequest`, `LabelRun`, `LabelLine`, `LabelImage`, and
`LabelLayout`), `LabelBuilder.Html.cs` (HTML labels), `LabelBuilder.Markdown.cs` (markdown and plain strings),
`LabelBuilder.MarkdownLexer.cs` (the part of the `marked` lexer that Mermaid's markdown labels use),
`LabelBuilder.Svg.cs` (SVG labels), and `LabelBuilder.TextDimensions.cs` (Mermaid's text utilities, with the record
struct `TextDimensions`, which the sequence diagram measures and wraps its texts with). All types are in the
`DemaConsulting.CanvasNet.Mermaid.Rendering` namespace.

#### Purpose

`LabelBuilder` lays out and measures label text as Mermaid's `createText` does (`rendering-util/createText.ts`,
`handle-markdown-text.ts`, and `splitText.ts`), with the browser's layout of Mermaid's label emulated over the bundled
fonts: plain and markdown strings, HTML labels (a `foreignObject` whose box the browser measures), and SVG labels
(`tspan` lines). The measured box decides the size of every node, subgraph title, and edge label, and the lines are
drawn as scene text. It also measures and wraps text as Mermaid's text utilities do (`calculateTextDimensions`,
`wrapLabel`, `breakString`, `parseFontSize`, and `splitBreaks` of `utils.ts` and `common.ts`), which decide the sizes
of the participants, messages, notes, and frames of a sequence diagram, the width at which the texts of a class box
wrap, and the width of an entity box text and of a narrow entity name. The unit's behavior is specified by the
`CanvasNetMermaid-Rendering-LabelBuilder-*` requirements in
`docs/reqstream/canvas-net-mermaid/rendering/label-builder.yaml`.

#### Data Model

- `LabelFont(Family, Size, Bold, Italic, Color)` - the text style a label starts from (the CSS family list, the size in
  pixels, and the color).
- `LabelRequest(Text, Font)` - the label text and the options of `createText` and `labelHelper`: `Markdown`,
  `HtmlLabels`, the wrapping `Width` (200 by default; infinity never wraps), `IsNode`, `AddBackground`,
  `ParagraphPadding`, `MarkdownAutoWrap`, `Prepare` (decode the entity placeholders and sanitize first, as for node
  labels), `ConfigFontSize` (which sizes a picture alone in a label), `RootFontSize` (the font size the label inherits,
  in which SVG lines are wrapped and, when the label's own size differs, the background of an SVG label is measured),
  `CenterSvgText`, `SvgInheritsWeight` (the lines of an SVG label take the weight of `Font`, as the name of a class box
  inherits the bold weight of its group because Mermaid removes the `font-weight` of its `tspan` elements; otherwise
  only strong markdown is bold), and the `Location` of the element for reports.
- `LabelRun(Text, Font, Color, Width)` and `LabelLine(X, Baseline, Width, Box, Runs)` - the styled runs of a line and
  the line with its left end, baseline, advance, and box (the line box of an HTML label, the glyph box of an SVG label).
- `LabelImage(Box, Href)` - a picture of an HTML label.
- `LabelLayout(IsHtml, Box, Lines, Images)` - the measured label in the coordinates of its own group: an HTML label's
  box starts at the origin; an SVG label's first baseline is one font size below the origin and its box has a negative
  x. `OffsetX` and `OffsetY` are Mermaid's `bbox.x - (bbox.left ?? 0)` and `bbox.y - (bbox.top ?? 0)`, which the shapes
  use to place the label; `Of(isHtml, box)` is a label with nothing to draw.
- The constants `HtmlLineHeight` (1.5), `SvgLineHeight` (1.1), `SvgBackgroundPadding` (2), and `LoneImageFactor` (5),
  the run grid of SVG text (`SvgLayoutUnits`, 64 layout units to the pixel, with the tolerance `SvgLayoutTolerance` of
  a thousandth of a layout unit for the single-precision advances), and a cache of the resolved faces by family,
  weight, and style.
- `TextDimensions(Width, Height, LineHeight)` - the size `calculateTextDimensions` gives a text, in whole pixels.
- For the text utilities, an optional `Meter` (the `LayoutWorkMeter` that measuring and hyphenating charge), the
  memoized results of `wrapLabel` and `breakString` of one render, the `lastIndex` of Mermaid's global
  `lineBreakRegex`, which carries over from one test to the next within a render as it does in Mermaid, and the
  Mermaid memoize keys of the texts measured so far: the first measurement of a key resets that `lastIndex`, as
  Mermaid's drawing of the measured text (its `replace` with the same regex) does on a memoize miss.

#### Key Methods

- `LabelBuilder(FontResolver fonts, MermaidReportSink? sink, NodeImages? images)` - a builder for one call, with the
  font resolver that the painter also uses.
- `Build(LabelRequest request)` - for a node label first `labelHelper`'s treatment (`DecodeEntities`, then the
  sanitizer of the `Syntax` subsystem), then an HTML or an SVG label.
- HTML labels (`LabelBuilder.Html.cs`) - markdown becomes HTML (`MarkdownToHtml`) and plain text a paragraph
  (`NonMarkdownToHtml`); entity placeholders are decoded, `fa:` icon names are replaced by an empty element
  (`ReplaceIcons`), and the HTML is sanitized and read into styled inline items (`b`, `strong`, `i`, `em`, `br`, `img`,
  `span` with a style, and `p`). The items are laid out with the rules of Mermaid's label element: white space
  collapsed and no wrapping, line boxes of 1.5 font sizes, centered lines, and a baseline at
  `top + floor((L - (A + D)) / 2) + A` with the rounded ascent and descent; run widths are rounded up to the browser's
  1/64 pixel layout unit. When the text and the paragraph padding reach the wrapping width, the items are laid out
  again with the spaces kept in a box of exactly that width (wider for an unbreakable piece), breaking after spaces and
  hyphens with the core `WordWrap`, as the browser does after Mermaid's `break-spaces` retry.
- SVG labels (`LabelBuilder.Svg.cs`) - Mermaid's lines (`MarkdownToLines` or `NonMarkdownToLines`, with tags kept as
  words and `fa:` names kept as text), split to the wrapping width measured in the inherited root font size
  (`splitLineToFitWidth`, a word wider than it split at characters), and placed by `PlaceSvgLines` in a font size:
  each word a run of its own (as `createFormattedText` writes each word as its own `tspan`), advanced by its width on
  the browser's run grid (`SvgRunAdvance`), baselines at `(1 + 1.1 i)` font sizes, centered on x 0 for node and edge
  labels, and a box from the rounded ascent above the first baseline to the rounded descent below
  the last, widened to the boxes of the glyphs at the ends of each run (`SvgLineExtent`, the rule of the sequence
  texts). With a background, the box grows by 2 pixels on every side (`createFormattedText`'s background rectangle).
  Mermaid measures that rectangle before the label is moved under the rules that give it its own font size, so when
  the label's `Font.Size` differs from its `RootFontSize` the lines are placed a second time in the root size and the
  rectangle is their box grown by the padding, while the drawn lines keep the label's own size; the label's box is
  then the union of the rectangle and the drawn text. An entity relationship role of 14 pixels in a 16 pixel diagram
  thus has the background and the layout size of the 16 pixel text, as in Mermaid.
  A word is bold when it is strong markdown or, with `SvgInheritsWeight`, when the label's font is bold, and is
  measured in that weight, so an inherited bold weight widens the line.
- `MarkdownToHtml`, `MarkdownToLines`, `NonMarkdownToHtml`, `NonMarkdownToLines`, and `Dedent` - the functions of
  `handle-markdown-text.ts` over the markdown lexer: paragraphs, `**strong**` and `__strong__`, `*em*` and `_em_` with
  CommonMark's delimiter rules, backslash escapes, code spans, inline HTML, the common indentation removed as
  `ts-dedent` does, and no-break spaces when `markdownAutoWrap` is off; any other block is kept as written.
- `DecodeEntities(text)` and `DecodeHtmlEntities(text)` - Mermaid's entity placeholders and the character references
  (through the sanitizer's table).
- `ContainsFontAwesome(text)` (static) - whether a label holds a Font Awesome icon as Mermaid finds one: the prefix
  `fa`, `fab`, `fak`, `fal`, `far`, or `fas`, then `:fa-` and a word character or a dash; null holds none. The diagrams
  that report such icons (the flowchart and the mindmap) share this test, which moved here from `FlowchartModel` so
  that the `Mindmap` subsystem does not depend on the `Flowchart` subsystem.
- `Face`, `Advance`, and `VerticalMetrics` - the face of a family list, the advance of a text with the core
  `TextRenderer`, and the ascent and descent rounded to whole pixels as the browser uses them.
- `LabelLayout.ToScene(dx, dy, role, id, location)` - the text lines (anchored at their start) and the pictures of the
  label as scene items, translated by the label's offset.
- Text utilities (`LabelBuilder.TextDimensions.cs`):
  - `ParseFontSize(ConfigScalar)` - Mermaid's `parseFontSize`: a number is pixels; a string gives its `parseInt` value
    and is itself the CSS size, with `px` added when it is that integer alone; `FontPixels(css)` gives the pixels a
    browser draws the inline CSS size of an SVG text at (a length with a unit, `em` and `rem` of 16 pixels, a
    non-negative number without a unit as pixels, which Chrome accepts on SVG text, or the inherited 16 pixels for a
    size the browser does not accept).
  - `SplitBreaks(text)` - Mermaid's `splitBreaks`: the lines between `<br>` tags in any letter case, with white space
    and an optional slash, empty lines kept.
  - `CalculateTextDimensions(text, MermaidFontConfig)` and `CalculateTextWidth` - Mermaid's `calculateTextDimensions`:
    each line measured as the browser's `getBBox` of an SVG text (`MeasureLine`: the white space collapsed as SVG text
    collapses it, the advance grown to the rasterizer's boxes of the first and last glyphs by `SvgLineExtent`, and the
    rounded ascent plus the rounded descent), rounded to whole pixels; the widest line's width and the sum of the
    line heights.
  - `WrapLabel(label, maxWidth, font, joinWith)` - Mermaid's `wrapLabel`: a label with a `<br>` tag (tested with the
    stateful `TestLineBreak`) is kept, a label that fits is kept, and otherwise words are added to a line while it
    stays narrower than the width, a word wider than the width is hyphenated by `BreakString` (characters, whole code
    points, added until the line reaches the width, each line but the last ending with a hyphen), and the lines are
    joined with `<br/>`; results are memoized as Mermaid memoizes them.
  - `SvgLineExtent`, `XHeight`, and `CollapseSvgWhiteSpace` - the horizontal box of an SVG text line (its advance on
    the run grid, and the boxes of its first and last glyphs, the last placed at its exact origin inside the run), the
    top of the font's `x` (for the `middle` baseline), and SVG's white-space collapsing, which the sequence renderer
    uses to place and box its texts; `SvgLineExtent` also gives the box of the diagram title and of the lines that
    `CalculateTextDimensions` measures.
  - `SvgRunAdvance(advance)` - the advance the browser gives a run of SVG text (a `text` or a `tspan` of its own): the
    exact advance rounded up to a 64th of a pixel (an advance within a thousandth of a grid unit above a grid line
    stays on it), so each run of a line starts on that grid while the glyphs inside a run keep their exact positions.
    `waiting for the bank` at 16 pixels is 140.766 pixels wide as four word runs and 140.75 pixels as one run;
    3187.5 / 64 pixels becomes 3188 / 64, and 1.5 pixels stays.

**Preconditions and postconditions**: a builder belongs to one call on one thread; the result is a deterministic
function of the request and the bundled fonts.

#### Error Handling

- An HTML element outside the supported subset is dropped with its text kept and reported as
  `mermaid-html-tag-ignored` (once per element name of a label).
- A character that no bundled font has is reported once per label as `mermaid-text-missing-glyph` (listing up to 8
  code points), and the font's missing-glyph box is drawn.
- An empty label measures an empty box; a picture of a label that cannot be loaded is reported by `NodeImages`.
- The image limit failure of `NodeImages` propagates unchanged; a null request throws `ArgumentNullException`.
- The text utilities charge their work to the `Meter` when one is set; beyond the layout work limit the meter throws a
  `MermaidRenderException` with code `mermaid-limit-exceeded` at its location, and a canceled token throws
  `OperationCanceledException`. A text of font size zero, and a line of only spaces between `<br>` tags, fail with
  `mermaid-render-failed`, as Mermaid's render fails ("svg element not in render tree").

#### Dependencies

- **Rendering subsystem** - `NodeImages` (natural sizes of label pictures)
- **Configuration subsystem** - `MermaidFontConfig` and `ConfigScalar` (the fonts of the text utilities)
- **Dagre subsystem** - `LayoutWorkMeter` (the work budget of the text utilities)
- **Syntax subsystem** - `HtmlSanitizer` (Mermaid's sanitizing and the character references, with the `HtmlEntities`
  table)
- **Scenegraph subsystem** - `SceneText`, `SceneTextRun`, `SceneImage`, `SceneFont`, `SceneRect`, `SceneItem`
- **Package root** - `MermaidReportSink`, `MermaidDiagnosticCodes`, `MermaidLocation`
- **Core `CanvasNet`** - `FontResolver` and `TrueTypeFont` (`Fonts`), `TextRenderer`, `WordWrap`, and `GlyphFallback`
  (`Rendering`), and `Rgba32` (`Canvas`)

#### Callers

`ShapeLibrary` measures node labels, subgraph titles, the texts of a class box, the titles and descriptions of a state,
and the texts of an entity box through `ShapeContext.MeasureLabel`, which `FlowchartModel` (the `Flowchart` subsystem),
`ClassModel` (the `Class` subsystem), `StateModel` (the `State` subsystem), and `ErModel` (the `Er` subsystem) set to
`Build`; `ClassModel` and `ErModel` also set `ShapeContext.TextWidth` to `CalculateTextWidth` in the configured font, at
whose width plus 50 pixels the class box texts wrap (plus 100 pixels for the entity box texts) and with which an entity
without attributes is tested against its minimum width, and charge the builder's measuring to the render's work meter.
`ClusterLayout.RenderDiagram` uses `SvgLineExtent` and `VerticalMetrics` for the box of the diagram title. `EdgeBuilder`
measures edge and terminal labels through the same delegate (an edge label with the sheet's root font size as its
`RootFontSize`) and draws them with `LabelLayout.ToScene`. In the `Sequence` subsystem, `SequenceLayout` creates a
builder for each render (charged to the render's work meter) and measures and wraps with the text utilities, and
`SequenceRenderer` boxes and places its text lines with them. `PieRenderer` of the `Pie` subsystem and `GanttRenderer`
of the `Gantt` subsystem create a builder for each render (charged to the render's work meter) and measure their SVG
texts with `SvgLineExtent` and `VerticalMetrics` (the title, the percentage labels, and the legend texts of a pie chart;
the task texts, section titles, tick labels, and title of a gantt chart) and `ParseFontSize`, split texts at `<br>` with
`SplitBreaks`, and decode the texts they draw with `DecodeEntities` and `CollapseSvgWhiteSpace`. `MindmapRenderer` of
the `Mindmap` subsystem creates a builder for each render (charged to the render's work meter) and sets its `Build` as
the `ShapeContext.MeasureLabel` of the mindmap's nodes, decodes each label with `DecodeEntities` before it checks the
label's length, and reports Font Awesome labels with `ContainsFontAwesome`, which `FlowchartModel` also uses. The tests
in `LabelBuilderTests.cs` call the unit directly through `InternalsVisibleTo`.

#### Known Differences From Mermaid

- Text is measured with the bundled fonts only: Liberation Sans stands in for Mermaid's default family names, and a
  character outside the bundled fonts is drawn as the missing-glyph box (a browser would take a system fallback font,
  so such a label can be narrower than in Mermaid).
- The box of an SVG label emulates the browser's `getBBox` (rounded metrics and outline rounding); the residual is
  typically 0.1 pixel and at most 1.6 pixels. The SVG texts of a class box differ by up to 0.882 pixel in the class
  references, because Chrome takes the box of SVG text from the glyphs it rasterizes at their place on the device pixel
  grid, so the same text measures differently where it is drawn. HTML labels match to the layout unit.
- Only the HTML elements `b`, `strong`, `i`, `em`, `br`, `img`, `span` (with a style), and `p` are understood; any
  other element (`sup`, `code`, `u`, `a`, and so on) is dropped with its text kept and reported, and its size changes
  are not modeled. Mixed font sizes on one line use the line box of the tallest font.
- The named character references are those of HTML 4 (252) and `apos`, `equals`, `num`, `semi`, and `colon`; any other
  HTML 5 name stays literal.
- An `<img>` beside text in an HTML label is as wide as the label's content (approximating `width: 100%`); alone it is
  5 times the configured font size wide, as Mermaid's `configureLabelImages` makes it.
- Markdown is the subset of `marked` that Mermaid's labels use (paragraphs, strong and emphasis with CommonMark's
  delimiter rules, escapes, inline HTML, and code spans kept as written); other blocks are kept as written, as Mermaid
  does, and links, tables, strikethrough, and hard breaks are plain text.
- The text utilities emulate `getBBox` with the advance grown to the rasterizer's boxes of the end glyphs; this matches
  every text of the sequence goldens, but for other glyphs or sizes a rounded width can differ by 1 pixel. Chrome's
  snapping of text baselines to 1/64 pixel is not modeled. Hyphenating is bounded by the layout work limit, which
  Mermaid does not have.

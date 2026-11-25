## Layout

![Layout Structure](MathMlLayoutView.svg)

### Overview

The `Layout` subsystem (namespace `DemaConsulting.CanvasNet.MathMl.Layout`, folder
`src/DemaConsulting.CanvasNet.MathMl/Layout/`) lays the equation tree out into a tree of boxes as MathML Core does, in
CSS pixels at the font size of the call, over the bundled math font. It is **internal**: its types are visible only to
the other parts of the system and, through `InternalsVisibleTo`, to the test assembly. It contains six units:

- **LayoutEngine** - the internal `LayoutEngine` (`LayoutEngine.cs`): the walk of the tree, the dispatch of each
  element, the elements without a box, the reports of the elements and attributes this version does not draw, and the
  bounds; see *LayoutEngine Unit Design*
- **StyleResolver** - the internal `StyleResolver` with the record `MathStyle` (`StyleResolver.cs`): the computed style
  of each element; see *StyleResolver Unit Design*
- **MathFont** - the internal `MathFont` with the enum `MathConstant` (`MathFont.cs`): the bundled Noto Sans Math, its
  `MATH` constants, its glyph metrics, and the bundled fallback; see *MathFont Unit Design*
- **MathBox** - the box model (`MathBox.cs`): `MathBox`, `MathRect`, the paint items, `EmbellishedOperator`, and
  `PlacedBox`; see *MathBox Unit Design*
- **TokenLayout** - the internal `TokenLayout` (`TokenLayout.cs`) with the `mathvariant` tables `MathVariants` and
  `MathVariant` (`MathVariants.cs` and the generated `MathVariants.Data.cs`): the token elements; see *TokenLayout Unit
  Design*
- **RowLayout** - the internal `RowLayout` (`RowLayout.cs`): rows, the grouping rules, `mpadded`, and `merror`; see
  *RowLayout Unit Design*

The subsystem's namespace documentation is in `Layout/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetMathMl-Layout-*` requirements in `docs/reqstream/canvas-net-math-ml/layout.yaml`. The SysML2 model names the
subsystem `MathMlLayout` (see *CanvasNetMathMl System Design*).

### Interfaces

**Exposed** (internal only): `new LayoutEngine(MathFont, MathMlLimits, MathMlReportSink, CancellationToken)` with
`Styles`, `Work`, and `Layout(MathElement, MathStyle)`; `StyleResolver.ResolveRoot`, `Resolve`, `ResolveLength`,
`ScaleFactor`, and `TryParseDisplay`; `MathFont.Default` with its constants and metrics; the `MathBox` tree with its
`ContentRect` and `InkBounds` and the paint items `GlyphItem`, `RuleItem`, `BackgroundItem`, and `BorderItem`;
`TokenLayout.LayoutToken`, `LayoutText`, and `Transform`; `MathVariants.TryParse`, `Map`, and `AutoItalic`;
`RowLayout.LayoutRow`, `LayoutPadded`, and `LayoutError`.

**Consumed**: the `MathTree` and `AttributeValues` units of the Syntax subsystem; the public `MathMlLimits`
(`MaxNestingDepth`, `MaxLayoutWork`), `MathMlLocation`, `MathMlRenderException`, `MathMlDiagnosticCodes`, and the
internal `MathMlReportSink` of the root namespace; the core `Fonts` subsystem (`TrueTypeFont` with its internal
`TryGetMathTable`, `SystemFontCatalog.LoadBundledFallbackCore`, and `GlyphFallback`), `Geometry` (the outlines whose
bounds are the ink), and `Canvas` (`Rgba32`).

**Consumers**: `MathMlDocument` creates one engine per measure or render call, resolves the style of the `math`
element, lays the tree out, and frames the picture from the box; `MathMlPainter` paints the box tree.

### Design

The document creates a `LayoutEngine` per call, which creates the `StyleResolver`, `TokenLayout`, and `RowLayout` of
the call over the shared `MathFont.Default` and the report sink of the call. The engine walks the tree depth-first: for
each element it computes the style from the parent's, skips the elements without a box, and lays the element out with
the token layout or as a row of its laid-out children with the row layout, which builds `MathBox`es bottom-up. The
boxes carry their paint items (glyphs, backgrounds, rules, and borders), the embellished-operator data and space-like
flag that rows read, the element (for the reports and the painter), and the phantom flag. All lengths are CSS pixels
at the font size of the call; the scale of the picture is applied only by the painter. Every unit records its reports
into the sink of the call, and each instance serves one call on one thread; `MathFont.Default` is shared and safe for
concurrent use.

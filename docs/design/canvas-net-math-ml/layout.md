## Layout

![Layout Structure](MathMlLayoutView.svg)

### Overview

The `Layout` subsystem (namespace `DemaConsulting.CanvasNet.MathMl.Layout`, folder
`src/DemaConsulting.CanvasNet.MathMl/Layout/`) lays the equation tree out into a tree of boxes as MathML Core does, in
CSS pixels at the font size of the call, over the bundled math font. It is **internal**: its types are visible only to
the other parts of the system and, through `InternalsVisibleTo`, to the test assembly. It contains twelve units:

- **LayoutEngine** - the internal `LayoutEngine` (`LayoutEngine.cs` and `LayoutEngine.Elements.cs`): the walk of the
  tree, the dispatch of each element, the elements without a box, the child-count rules, `movablelimits`, the reports
  of the elements and attributes this version does not draw, and the bounds; see *LayoutEngine Unit Design*
- **StyleResolver** - the internal `StyleResolver` with the records `MathStyle` and `ChildStyleRule` and the enum
  `MathDepthRule` (`StyleResolver.cs`): the computed style of each element and the user agent rules of the children of
  layout elements (fractions, radicals, and scripted elements); see *StyleResolver Unit Design*
- **MathFont** - the internal `MathFont` with the enum `MathConstant` and the construction records `StretchVariant`,
  `StretchPart`, and `StretchConstruction` (`MathFont.cs`): the bundled Noto Sans Math, its `MATH` constants and
  constructions, its glyph metrics, and the bundled fallback; see *MathFont Unit Design*
- **MathBox** - the box model (`MathBox.cs`): `MathBox`, `MathRect`, the paint items, `EmbellishedOperator`,
  `BlockStretchConstraint`, `OperatorContext`, and `PlacedBox`; see *MathBox Unit Design*
- **TokenLayout** - the internal `TokenLayout` (`TokenLayout.cs`) with the `mathvariant` tables `MathVariants` and
  `MathVariant` (`MathVariants.cs` and the generated `MathVariants.Data.cs`): the token elements, the properties of
  operators, and stretched (vertically and horizontally) and large operators; see *TokenLayout Unit Design*
- **RowLayout** - the internal `RowLayout` (`RowLayout.cs`): rows, the stretching of their operators, the grouping
  rules, `mpadded`, and `merror`; see *RowLayout Unit Design*
- **FractionLayout** - the internal `FractionLayout` (`FractionLayout.cs`): `mfrac` with a bar or as a stack, and its
  `linethickness`; see *FractionLayout Unit Design*
- **RadicalLayout** - the internal `RadicalLayout` (`RadicalLayout.cs`): `msqrt` and `mroot`; see *RadicalLayout Unit
  Design*
- **ScriptLayout** - the internal `ScriptLayout` with the record `ScriptPair` (`ScriptLayout.cs`): `msub`, `msup`,
  `msubsup`, `mmultiscripts`, and the limits of a movable operator outside display style; see *ScriptLayout Unit
  Design*
- **UnderOverLayout** - the internal `UnderOverLayout` (`UnderOverLayout.cs`): `munder`, `mover`, and `munderover`,
  and the inline stretching of their operators; see *UnderOverLayout Unit Design*
- **OperatorDictionary** - the internal `OperatorDictionary` with the enums `OperatorForm`, `StretchAxis`, and
  `OperatorCategory` and the record `OperatorProperties` (`OperatorDictionary.cs` and the generated
  `OperatorDictionary.Data.cs`): the operator dictionary of MathML Core; see *OperatorDictionary Unit Design*
- **StretchyGlyphs** - the internal `StretchyGlyphs` with `StretchKind`, `StretchGlyph`, and `StretchResult`
  (`StretchyGlyphs.cs` and the generated `StretchyGlyphs.Fallback.cs`): size variants, glyph assemblies, the
  Unicode-based assemblies, and the rule fallback; see *StretchyGlyphs Unit Design*

The subsystem's namespace documentation is in `Layout/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetMathMl-Layout-*` requirements in `docs/reqstream/canvas-net-math-ml/layout.yaml`. The SysML2 model names the
subsystem `MathMlLayout` (see *CanvasNetMathMl System Design*).

### Interfaces

**Exposed** (internal only): `new LayoutEngine(MathFont, MathMlLimits, MathMlReportSink, CancellationToken)` with
`Styles`, `Work`, and `Layout(MathElement, MathStyle)`; `StyleResolver.ResolveRoot`, `Resolve`, `ResolveChild`,
`ScriptRule`, `IsTrue`, `ResolveLength`, `ScaleFactor`, and `TryParseDisplay`; `MathFont.Default` with its constants,
metrics, and constructions; the `MathBox` tree with its `ContentRect` and `InkBounds` and the paint items `GlyphItem`,
`RuleItem`, `BackgroundItem`, and `BorderItem`; `TokenLayout.LayoutToken`, `LayoutOperator`, `LayoutText`, and
`Transform`; `MathVariants.TryParse`, `Map`, and `AutoItalic`; `RowLayout.LayoutRow`, `Stretch`, `LayoutPadded`, and
`LayoutError`; `FractionLayout.ResolveThickness` and `Layout`; `RadicalLayout.LayoutSquareRoot` and `LayoutRoot`;
`ScriptLayout.Layout`, `LayoutMultiscripts`, and `ItalicCorrections`; `UnderOverLayout.StretchInline` and `Layout`;
`OperatorDictionary.Category`, `Lookup`, `Properties`, `IsFence`, `IsSeparator`, `Axis`, `InferForm`, and
`TryParseForm`; `StretchyGlyphs.Stretch` (both axes), `Glyph`, `LargeVariant`, `PreferredInlineSize`, `SolveAssembly`,
and `UnicodeAssembly`.

**Consumed**: the `MathTree` and `AttributeValues` units of the Syntax subsystem; the public `MathMlLimits`
(`MaxNestingDepth`, `MaxLayoutWork`, `MaxAssemblyParts`), `MathMlLocation`, `MathMlRenderException`,
`MathMlDiagnosticCodes`, and the internal `MathMlReportSink` of the root namespace; the core `Fonts` subsystem
(`TrueTypeFont` with its internal `TryGetMathTable`, `SystemFontCatalog.LoadBundledFallbackCore`, and `GlyphFallback`),
`Geometry` (the outlines whose bounds are the ink), and `Canvas` (`Rgba32`).

**Consumers**: `MathMlDocument` creates one engine per measure or render call, resolves the style of the `math`
element, lays the tree out, and frames the picture from the box; `MathMlPainter` paints the box tree.

### Design

The document creates a `LayoutEngine` per call, which creates the `StyleResolver`, `TokenLayout`, `RowLayout`,
`FractionLayout`, `RadicalLayout`, `ScriptLayout`, and `UnderOverLayout` of the call over the shared `MathFont.Default`
and the report sink of the call. The engine walks the tree depth-first: for each element it computes the style from the
parent's (with the user agent rule of its position under a fraction, a root, or a scripted element), skips the elements
without a box, and lays the element out with the token layout, as a row of its laid-out children with the row layout,
or from the boxes of its children with the fraction, radical, script, or under- and overscript layout, which build
`MathBox`es bottom-up. A fraction, a root, or a scripted element without the in-flow children MathML Core requires is a
row with a `mathml-child-count` warning. Before
the children of a row are laid out, the engine infers the form of each embellished operator from its position among
the children that are not space-like (MathML Core 3.2.4.2); the token layout resolves the properties of an `mo` from
the operator dictionary and its attributes. After the children are laid out, the row layout asks the engine to lay its
stretchy operators out again with a block stretch size constraint (3.3.1.1), and the token layout stretches the glyph
with `StretchyGlyphs`; an embellished grouping element, or a fraction or a scripted element whose first child is an
embellished operator, passes the form and the constraint on to its operator. The scripts of a scripted element are
postfix operators; after the children of an under- or overscript element are laid out, the under- and overscript
layout asks the engine to lay its operators stretchy along the inline axis out again with an inline stretch size
constraint (3.4.2.2), which the token layout stretches with `StretchyGlyphs` as well; outside display style, the limits
of an operator with `movablelimits` are laid out by the script layout. The radical layout stretches the radical symbol
with `StretchyGlyphs` too. The
boxes carry their paint items (glyphs, backgrounds, rules, and borders), the embellished-operator data and space-like
flag that rows read, the element (for the reports and the painter), and the phantom flag. All lengths are CSS pixels
at the font size of the call; the scale of the picture is applied only by the painter. Every unit records its reports
into the sink of the call, and each instance serves one call on one thread; `MathFont.Default` is shared and safe for
concurrent use, and `OperatorDictionary` and `StretchyGlyphs` are stateless.

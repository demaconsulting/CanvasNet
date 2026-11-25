### MathFont

![Layout Structure](MathMlLayoutView.svg)

The `MathFont` unit is a software unit of the `Layout` subsystem. Its source file is `MathFont.cs`, which defines the
internal enum `MathConstant`, the record structs `StretchVariant` and `StretchPart`, the record `StretchConstruction`,
and the internal sealed class `MathFont`.

#### Purpose

A browser draws MathML with a math font whose OpenType `MATH` table drives the layout. `MathFont` is that font for
CanvasNet: the bundled Noto Sans Math with its `MATH` table, scaled to a font size, with the deterministic per-character
fallback of the core for the characters it lacks, so that the result is the same on every machine. The unit's behavior
is specified by the `CanvasNetMathMl-Layout-MathFont-*` requirements in
`docs/reqstream/canvas-net-math-ml/layout/math-font.yaml`.

#### Data Model

- `MathConstant` - the constants of the `MATH` table that the layout reads now: `ScriptPercentScaleDown`,
  `ScriptScriptPercentScaleDown`, `AxisHeight`, `FractionRuleThickness`, `DisplayOperatorMinHeight`,
  `DelimitedSubFormulaMinHeight`, and `OverbarRuleThickness` (the thickness of a rule without ink to copy); the
  fraction constants (`FractionNumeratorShiftUp`, `FractionNumeratorDisplayStyleShiftUp`,
  `FractionDenominatorShiftDown`, `FractionDenominatorDisplayStyleShiftDown`, `FractionNumeratorGapMin`,
  `FractionNumDisplayStyleGapMin`, `FractionDenominatorGapMin`, `FractionDenomDisplayStyleGapMin`); the stack constants
  (`StackTopShiftUp`, `StackTopDisplayStyleShiftUp`, `StackBottomShiftDown`, `StackBottomDisplayStyleShiftDown`,
  `StackGapMin`, `StackDisplayStyleGapMin`); and the radical constants (`RadicalVerticalGap`,
  `RadicalDisplayStyleVerticalGap`, `RadicalRuleThickness`, `RadicalExtraAscender`, `RadicalKernBeforeDegree`,
  `RadicalKernAfterDegree`, and the percentage `RadicalDegreeBottomRaisePercent`); the script layouts of a later
  version add theirs.
- `StretchVariant(Glyph, Advance)`, `StretchPart(Font, Glyph, StartConnector, EndConnector, FullAdvance, IsExtender)`,
  and `StretchConstruction(Variants, Parts, AssemblyItalicCorrection)` - a construction of the `MathVariants` table in
  CSS pixels at a font size (the part records also serve the Unicode-based assemblies of `StretchyGlyphs`, whose parts
  may come from a fallback font).
- `BundledFileName` - `NotoSansMath-Regular.ttf`.
- `Primary` - the primary `TrueTypeFont` and its `MathTable` (read once by the core); `UnitsPerEm`.
- The ink cache - a `ConcurrentDictionary` of the outline bounds in font units per font and glyph, filled on demand
  with a flattening tolerance of half a design unit.
- `Default` - the process-wide instance, created on first use (`Lazy`, execution and publication thread-safe) over
  `SystemFontCatalog.LoadBundledFallbackCore(BundledFileName)`, the cached bundled font of the core.

#### Key Methods

- `MathFont(font)` - takes the `MATH` table of the font through `TrueTypeFont.TryGetMathTable` (friend access).
- `RawConstant(constant)` - the value in design units (a percentage for the scale-down constants);
  `Constant(constant, fontSize)` - scaled to CSS pixels.
- `Resolve(codePoint)` - `GlyphFallback.Resolve(Primary, codePoint)`: the primary font when it has the glyph, else the
  first bundled fallback font that has it (Noto Sans, then Noto Sans Symbols 2), else glyph 0 of the primary font.
- `Advance(font, glyph, fontSize)` - the advance width in CSS pixels.
- `InkBounds(font, glyph, fontSize)` - the bounds of the outline, cached, scaled, with y growing downward from the glyph
  origin on the baseline; empty for a glyph without contours.
- `ItalicCorrection` and `TopAccentAttachment` - from the `MATH` glyph information of the primary font, in CSS pixels;
  zero and null for a glyph of a fallback font.
- `XHeight(fontSize)` - the ink top of the letter x, the base of the `ex` unit.
- `MinConnectorOverlap(fontSize)` - the minimum overlap of two parts of an assembly in CSS pixels.
- `TryGetConstruction(font, glyph, axis, fontSize, out construction)` - the vertical (block axis) or horizontal (inline
  axis) construction of a glyph of the primary font, scaled; false for a glyph of a fallback font or without one.

#### Error Handling

A font without a `MATH` table cannot be a math font and throws `InvalidDataException`; a malformed `MATH` table of a
font throws `InvalidDataException` from the core on first access, which `MathMlDocument` turns into a located
`mathml-render-failed` error. A character without a glyph is not an error here: `TokenLayout` reports it.

#### Dependencies

The core `Fonts` subsystem (`TrueTypeFont` with the internal `TryGetMathTable` and `MathTable`,
`SystemFontCatalog.LoadBundledFallbackCore`, and `GlyphFallback`) and `Geometry` subsystem (the bounds of the
outlines).

#### Callers

`MathMlDocument` creates every layout over `MathFont.Default`; `LayoutEngine`, `StyleResolver`, `TokenLayout`,
`FractionLayout`, and `RadicalLayout` read its constants and metrics; `StretchyGlyphs` reads its constructions.

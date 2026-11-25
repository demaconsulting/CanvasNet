### StretchyGlyphs

![Layout Structure](MathMlLayoutView.svg)

The `StretchyGlyphs` unit is a software unit of the `Layout` subsystem. Its source files are `StretchyGlyphs.cs`, which
defines the internal enum `StretchKind`, the internal record struct `StretchGlyph`, the internal record
`StretchResult`, and the internal static partial class `StretchyGlyphs`, and the generated `StretchyGlyphs.Fallback.cs`,
which holds the Unicode-based glyph assemblies of MathML Core Appendix B.4.

#### Purpose

`StretchyGlyphs` stretches the glyph of a character to a target size along the block or the inline axis, as MathML Core
5.3 specifies: the base glyph, a size variant, or a glyph assembly of the `MATH` table of the math font, with two
fallbacks for the characters that the font cannot stretch; and it finds the large variant of an operator in display
style. The unit's behavior is specified by the `CanvasNetMathMl-Layout-StretchyGlyphs-*` requirements in
`docs/reqstream/canvas-net-math-ml/layout/stretchy-glyphs.yaml`.

#### Data Model

- `StretchKind` - `Base`, `Variant`, `Assembly`, `UnicodeAssembly`, and `Rule`.
- `StretchGlyph(Font, Glyph, X, Baseline)` - a glyph of a result at its pen position and baseline.
- `StretchResult(Kind, Width, Ascent, Descent, ItalicCorrection, Glyphs, Rule)` with `Height` - a stretched glyph in
  CSS pixels relative to its origin. A glyph or a variant has the ink ascent and descent of its glyph rounded out to
  whole CSS pixels, as the token layout rounds them; a vertical assembly stands on the baseline (ascent = size,
  descent = 0) and its caller moves it to its target; a horizontal assembly starts at the origin.
- `RuleCharacters` - the line characters U+005F, U+00AF, U+0332, U+2015, and U+203E.
- `UnicodeAssemblyTable` (generated) - the 41 rows of B.4: the base character, the axis, the extender, and the bottom
  (left), middle, and top (right) characters, or -1 for an absent part; looked up through a frozen dictionary built
  once.

#### Key Methods

- `Stretch(font, codePoint, axis, target, fontSize, maxParts, location)` - the algorithm to shape a stretchy glyph of
  MathML Core 5.3.2. With no construction for the axis in the math font, no rule (a line character), and no B.4 entry,
  it fails (null). Otherwise: the base glyph when its height (block) or advance (inline) covers the target; else the
  first variant whose advance measurement covers it; else the assembly; else the last of these that was tried. A
  character without a construction in the math font stretches as a rule when it is a line character, else with its
  Unicode-based assembly; the rule comes first because the B.4 entries of the line characters repeat a glyph with side
  bearings, which leaves gaps.
- `SolveAssembly(parts, minOverlap, target, maxParts, location)` - MathML Core 5.3.1: every extender is repeated r times
  and one overlap o joins every pair of adjacent glyphs. The connectors that join two glyphs (every start connector but
  that of a first non-extender, every end connector but that of a last non-extender) bound o from above; when one is
  shorter than `MinConnectorOverlap`, it lowers the minimum instead of invalidating the assembly as MathML Core would
  (the arrows of Noto Sans Math join their extenders without overlap). r is the smallest number of repetitions whose
  size at the minimum overlap reaches the target (with a tolerance of 1e-9); more than `maxParts` glyphs throw before
  any glyph is placed. o spreads the extra size evenly, clamped between the minimum overlap and the connectors. An
  assembly without an extender, or whose extenders do not grow it, is invalid (null).
- `Assemble` - places the glyphs from the bottom (or the left), each the overlap before the end of the previous one: a
  vertical part with the bottom of its ink on that position, a horizontal part with its origin there. The width of a
  vertical assembly is the largest advance of its parts; the ascent and descent of a horizontal one are the largest of
  the ink of its parts. The italic correction is the assembly's.
- `UnicodeAssembly(font, codePoint, axis, fontSize)` - the parts of a B.4 entry: the bottom character, the extender,
  optionally the middle character and the extender again, and the top character, each resolved through the bundled
  fallback, with zero connectors and the ink height (block) or the advance (inline) as its size; null when a part has
  no glyph.
- `RuleResult` - a rule of the target length, as thick as the ink of the glyph of the character and where it is
  (horizontal), or as wide (vertical); `OverbarRuleThickness` on the math axis when the glyph has no ink.
- `Glyph(font, codePoint, fontSize)` - the base glyph as a result, which an operator keeps when nothing stretches it.
- `LargeVariant(font, codePoint, minHeight, fontSize)` - MathML Core 3.2.4.3: the first size variant other than the
  base glyph at least `minHeight` tall, else the largest; null without one.
- `PreferredInlineSize(font, codePoint, fontSize)` - MathML Core 5.3.2: the largest advance of the base glyph, the
  vertical variants, and the parts of the assembly (or of the B.4 assembly).

#### Error Handling

An assembly that needs more glyphs than `maxParts` (`MathMlLimits.MaxAssemblyParts`) throws a `MathMlRenderException`
with the code `mathml-limit-exceeded`, located at the operator and naming the property, before any glyph is placed; an
infinite target is refused the same way and a target that is not a number is treated as zero. A character that cannot
stretch is not an error here: the token layout keeps its glyph and reports it.

#### Dependencies

The `MathFont` unit (glyphs, ink, advances, constructions, constants), the `MathBox` unit (`MathRect`), the
`OperatorDictionary` unit (`StretchAxis`), the root namespace's `MathMlRenderException`, `MathMlLocation`, and
`MathMlDiagnosticCodes`, and the core `Fonts` subsystem (`TrueTypeFont`).

#### Callers

`TokenLayout` stretches operators along the block axis and finds the large variants; the under-over layout of a later
stage will stretch along the inline axis.

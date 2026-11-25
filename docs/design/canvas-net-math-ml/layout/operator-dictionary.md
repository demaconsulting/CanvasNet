### OperatorDictionary

![Layout Structure](MathMlLayoutView.svg)

The `OperatorDictionary` unit is a software unit of the `Layout` subsystem. Its source files are
`OperatorDictionary.cs`, which defines the internal enums `OperatorForm`, `StretchAxis`, and `OperatorCategory`, the
internal record `OperatorProperties`, and the internal static partial class `OperatorDictionary`, and the generated
`OperatorDictionary.Data.cs`, which holds the tables of MathML Core Appendix B.1.

#### Purpose

`OperatorDictionary` is the operator dictionary of MathML Core: it gives an operator its category from its text and its
form, the values of the category (spacing and the stretchy, symmetric, largeop, and movablelimits properties), the
fence and separator defaults, and the stretch axis of a character, and it infers the form of an embellished operator
from its position in its row. The unit's behavior is specified by the `CanvasNetMathMl-Layout-OperatorDictionary-*`
requirements in `docs/reqstream/canvas-net-math-ml/layout/operator-dictionary.yaml`.

#### Data Model

- `OperatorForm` - `Infix` (the default), `Prefix`, and `Postfix`.
- `StretchAxis` - `Block` (vertically, in a row) and `Inline` (horizontally, under and over a base).
- `OperatorCategory` - `Default`, `ForceDefault`, and `A` to `M` of Figure 26.
- `OperatorProperties(Category, LeadingSpace, TrailingSpace, Stretchy, Symmetric, LargeOp, MovableLimits)` - the values
  of a category, with the spacing in em.
- The generated tables (properties, read once, so that the order of the static initializers of the two files does not
  matter): `TwoAsciiOperators` (the 18 strings of Operators_2_ascii_chars), `FenceRanges` and `SeparatorRanges`
  (Operators_fence and Operators_separator as ranges), `InfixRanges`, `PrefixRanges`, and `PostfixRanges` (the mapping
  of Figure 25 from a code point to a category, as sorted, disjoint ranges per form), `CategoryValues` (Figure 26), and
  `InlineAxisCodePoints` (Figure 28, ascending). Reading Figure 25 directly is equivalent to the encoded Figure 27 and
  easier to review.

#### Key Methods

- `Category(content, form)` - the algorithm to determine the category of an operator: a text of one or two UTF-16
  characters is reduced to a key - one character, except the reserved range U+0320 to U+03FF; the first character when
  the second is U+0338 or U+20D2; U+0320 plus the index of a two-character ASCII operator - and looked up by binary
  search in the ranges of the form. The postfix U+1EEF0 and U+1EEF1 are `I`; the infix U+007C and U+223C are
  `ForceDefault`; anything else is `Default`.
- `Lookup(content, form, formIsExplicit)` - the algorithm for determining the properties of an embellished operator:
  the category of the form, or, when the form was inferred and the category is `Default`, of the first of the infix,
  postfix, and prefix forms that has an entry; then `Properties(category)`.
- `IsFence(content)` and `IsSeparator(content)` - the fence and separator defaults (no effect on the layout).
- `Axis(content)` - inline for a text of a single character of the inline list, block otherwise.
- `InferForm(position, count)` - the first of several in-flow children that are not space-like is prefix, the last of
  several is postfix, any other is infix (MathML Core 3.2.4.2).
- `TryParseForm(value, out form)` - the value of the `form` attribute, ASCII case-insensitive.
- `TablesAreWellFormed()` - checks that the generated tables are sorted and disjoint, for the tests.

#### Error Handling

N/A - the lookups accept any text and return `Default` for anything without an entry; a null text throws
`ArgumentNullException`, and an unknown category value throws `ArgumentOutOfRangeException` (a programming error).

#### Dependencies

None beyond the .NET base class library.

#### Callers

`TokenLayout` resolves the properties of every `mo`; `LayoutEngine` infers the forms of the embellished operators of a
row; `RowLayout`, `MathFont`, `StretchyGlyphs`, and the `MathBox` records use `StretchAxis` and `OperatorForm`.

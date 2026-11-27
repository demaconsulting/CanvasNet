# CanvasNet MathML Package

## API Reference

### MathMlDocument

`MathMlDocument` (namespace `DemaConsulting.CanvasNet.MathMl`) is distributed via the separate
`DemaConsulting.CanvasNet.MathMl` NuGet package, whose only dependency is the core `DemaConsulting.CanvasNet` package.
It reads equations written in **MathML Core**, the part of MathML that Chrome and Firefox draw, and lays them out as
MathML Core and Chrome lay them out, with the **Noto Sans Math** font that the core package bundles, using only managed
code: no browser, no TeX, and no network. An equation is drawn as one still picture. The package derives some of its
data tables with attribution from W3C and Unicode data (see *MathML Third-Party Attribution*).

Install it next to the core package (see also the Installation section of the project README); the `canvasnet` tool and
the `DemaConsulting.CanvasNet.Viewer` package already include it:

```bash
dotnet add package DemaConsulting.CanvasNet.MathMl
```

A document is **immutable and thread-safe**: it is read and validated once and can then be measured and rendered any
number of times, from several threads at once, each call giving the same result for the same options.

```csharp
public sealed partial class MathMlDocument
{
    public static MathMlDocument Parse(string text, MathMlOpenOptions? options = null);
    public static MathMlDocument Open(string path, MathMlOpenOptions? options = null);
    public static MathMlDocument Open(Stream stream, MathMlOpenOptions? options = null);

    public string? SourceName { get; }
    public MathMlDisplay Display { get; }                      // the display attribute: Block or Inline (the default)
    public int ElementCount { get; }                           // the MathML elements, the math element included
    public string? AltText { get; }                            // the alttext attribute of the math element
    public IReadOnlyList<MathMlAnnotation> Annotations { get; } // annotation and annotation-xml, never drawn

    public MathMlSize Measure(
        MathMlRenderOptions? options = null,
        CancellationToken cancellationToken = default);

    public Surface Render(
        MathMlRenderOptions? options = null,
        CancellationToken cancellationToken = default);

    public Surface Render(
        int width,
        int height,
        MathMlRenderOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

- `Parse` and the two `Open` overloads read one `math` element, in the MathML namespace or in no namespace (as HTML
  writes it). The named character references of the full W3C HTML and MathML set (`&alpha;`, `&minus;`,
  `&InvisibleTimes;`) and numeric references are decoded without a document type; a document type declaration is
  refused. A file or stream is read as strict UTF-8 (a byte order mark is removed, invalid UTF-8 is rejected), and an
  XML declaration that names another encoding (US-ASCII included) is refused with a located error.
  `Open(Stream)` reads from the current position and leaves the stream open. Reading never uses the network and never
  reads another file: links are reported, not followed.
- The descriptive properties come from the text only. `ElementCount` counts every MathML element, the `math` element
  included and the content of annotations excluded; `Annotations` holds the `encoding` and the text of each
  `annotation` and `annotation-xml` (a TeX source, for example), in document order.
- `Measure` lays the equation out and returns the size that `Render` produces for the same options, and the row of the
  alphabetic **baseline** (`MathMlSize(Width, Height, Baseline)`), so a caller can place the picture in a line of text.
  It delivers the same reports as `Render`.
- `Render()` draws the equation onto a new `Surface` at its **natural size**: the equation at
  `MathMlRenderOptions.FontSize` (16 CSS pixels, the default size of a browser), multiplied by `Scale`. The equation is
  laid out again at the final size, so it is sharp at every scale. The picture is the content box of the `math` element
  united with the ink of everything painted and a margin of one CSS pixel around the ink for antialiasing, rounded out
  to whole pixels, so ink that overhangs its box (an italic *f*, a stretched fence, the tail of a radical) is never
  clipped or cut at the edge of the picture. The caller owns, and must dispose, the returned surface.
- `Render(width, height)` draws onto a surface of exactly that size: the equation is scaled uniformly to fit and
  centered, and the margins take the background. `Scale` is not used.

**Exceptions** (`Measure` and both `Render` overloads):

- `ArgumentOutOfRangeException`: the explicit size is outside 1 to `Surface.MaxDimension` (8192) on a side.
- `MathMlRenderException`: a limit of `MathMlLimits` was met while the equation was laid out or painted, including a
  picture larger than `Surface.MaxDimension` on a side (`mathml-limit-exceeded`), or the layout failed
  (`mathml-render-failed`, with the original exception as the inner exception).
- `MathMlUnsupportedFeatureException` (an `IOException`): the first unsupported feature when `ThrowOnUnsupportedFeature`
  is set.
- `OperationCanceledException`: the token was canceled before or during the work.

`Parse` and `Open` throw `MathMlSyntaxException` (a `FormatException`) for text that is not well-formed MathML
(`mathml-syntax-error`) or meets a structural limit (`mathml-limit-exceeded`), and `InvalidDataException` for text
longer than `MaxTextSize` allows. `Open(string)` also throws `FileNotFoundException` and `DirectoryNotFoundException`
for a missing file.

Every exception and report carries a stable **code** (the constants of `MathMlDiagnosticCodes`), a **location**, and,
where one helps, a **hint**. A `MathMlLocation` is the `SourceName` and the one-based `Line` and `Column` in the
original text (before the named references are decoded); an empty or blank text is located at line 1, column 1.
`ToString()` gives `file:line:column`:

```text
bad.mml:2:10: mathml-syntax-error: the text is not well-formed XML: The 'mi' start tag on line 2 position 4 does ...
Hint: check that every element is closed and that a literal '&' or '<' in text is written as &amp; or &lt;
```

### MathMlOpenOptions and MathMlRenderOptions

Both classes are immutable; every property is init-only and validated when it is set, so one instance can be shared
between threads and calls.

The `MathMlOpenOptions` properties:

- `SourceName` (default `null`, the path for `Open(string)`): the name shown in locations and messages.
- `Limits` (default `null`, `MathMlLimits.Default`): the limits checked while the text is read.

The `MathMlRenderOptions` properties:

- `FontSize` (default 16): the font size of the `math` element in CSS pixels; every length of the layout follows from
  it. At most 4,096.
- `Scale` (default 1): the factor by which the natural size is multiplied; finite, greater than zero, and at most
  8,192.
- `Display` (default `null`): `MathMlDisplay.Block` or `MathMlDisplay.Inline` replaces the `display` attribute of the
  `math` element; `null` keeps it.
- `Color` (default opaque black): the color of the text and the lines, which `mathcolor` overrides.
- `Background` (default `null`, transparent): the color the surface is filled with.
- `Limits` (default `MathMlLimits.Default`): the resource limits of the call.
- `WarningHandler` (default `null`): receives each `MathMlDiagnostic` (a warning); `null` drops them.
- `UnsupportedFeatureHandler` (default `null`): receives each `MathMlUnsupportedFeature`; `null` drops them.
- `ThrowOnUnsupportedFeature` (default `false`): turns the first unsupported feature into a
  `MathMlUnsupportedFeatureException` (strict mode).

Reports are delivered once, in the order they were found, on the calling thread, after the work and before the method
returns or throws.

### MathMlLimits

MathML text is untrusted input, so every resource a call can use is limited, and exceeding a limit fails closed with a
message that names the property. Browsers have no such limits, so a very large but valid equation that a browser draws
can fail here; raise the limit when you trust the text. The limits bound the work, not the clock: the cancellation
token is the only wall-clock bound.

The limits, with their defaults and what they bound:

- `MaxTextSize` (default 1,048,576): the text as supplied, in UTF-16 characters. Exceeding it throws
  `InvalidDataException`.
- `MaxElements` (default 100,000): the MathML elements of the equation, each skipped element (of another namespace, or
  inside a token) and every element inside it, and every element inside an annotation, counted too: one skipped
  element cannot hide more elements than the limit.
- `MaxNestingDepth` (default 256, at most 1,024): elements inside each other, the elements inside a skipped element or
  an annotation included. The reader is iterative and the layout recursion is bounded by this limit, so a deep text
  never overflows the stack.
- `MaxTokenLength` (default 10,000): the characters of one token.
- `MaxLayoutWork` (default 10,000,000): the metered steps of the layout (every element and character, again for a
  stretched operator).
- `MaxPaintedGlyphs` (default 1,000,000): the glyphs one render paints.
- `MaxDiagnostics` (default 1,000): the reports kept per call; the rest become one `mathml-diagnostics-suppressed`
  warning. The `MaxDiagnostics` of the limits of `MathMlOpenOptions` also bounds the reports kept while the text is
  read.
- `MaxAssemblyParts` (default 1,000): the glyphs of one stretched operator or radical sign.
- `MaxTableCells` (default 100,000): the grid slots of one table, checked before the grid is allocated.

A limit met while the text is read is a located `MathMlSyntaxException`, and one met while the equation is laid out or
painted a located `MathMlRenderException`; the code of both is `mathml-limit-exceeded`. For `CanvasNetViewer.Render`,
`ViewerRenderOptions.MathMlLimits` of the Viewer package sets both, for the rendered equation and every equation it
embeds.

### Writing a MathML File

An equation file holds one `math` element, saved as **UTF-8** with the extension **`.mml`** (the only extension that is
recognized; the media type of MathML is `application/mathml+xml`). Write the namespace, the display mode, and a text
alternative:

```xml
<math xmlns="http://www.w3.org/1998/Math/MathML" display="block" alttext="E equals m c squared">
  <mi>E</mi>
  <mo>=</mo>
  <mi>m</mi>
  <mo>&InvisibleTimes;</mo>
  <msup><mi>c</mi><mn>2</mn></msup>
</math>
```

- `display="block"` lays the equation out in display style: the limits of a sum under and over it, large operators,
  full-size fractions. Without it the equation is inline (compact, as in a line of text). The `#display=block` and
  `#display=inline` fragment of a link or an input, and `MathMlRenderOptions.Display`, replace it.
- `alttext` describes the equation in words for documents that need alternative text; a probe of the Viewer reports
  it. `semantics` with `<annotation encoding="application/x-tex">` keeps the LaTeX an equation came from; annotations
  are never drawn.
- Numbers are `mn`, identifiers `mi` (italic when they have one letter, upright when they have several, so
  `<mi>sin</mi>` is a function name), operators `mo`, and text `mtext`. White space inside a token is trimmed and
  collapsed; white space between elements is ignored.
- `mrow` groups children. A fraction, a root with an index, and every script take an exact number of children, so
  wrap a part of several children in `mrow`.

The help cards of the Viewer's `CanvasNetHelp` (and `canvasnet help mathml`) teach each authoring task with worked
examples that are rendered in strict mode by the tests: `mathml/basics`, `fractions`, `scripts`, `large-operators`,
`fences`, `matrices`, `aligned`, `units`, `symbols`, `accents`, `formulas`, `style`, `pitfalls`, and `composition`.

### Supported MathML Elements

Every element of MathML Core is drawn; an element outside MathML Core is reported (see *MathML Unsupported Features
and Strict Mode*). These items are also the bullets of the help card `mathml/elements`; a test keeps them the same.

- **Tokens**: `mi` (one letter italic, several upright), `mn`, `mo` (spaced, stretched, and sized by the operator
  dictionary of MathML Core), `mtext`, `ms` (drawn without quotes), and `mspace` (an empty box of the given width,
  height, and depth).
- **Rows and grouping**: `math`, `mrow`, `mstyle`, `mpadded` (with its width, height, depth, lspace, and voffset),
  `mphantom` (laid out, not painted), and `merror` (a red border on a light yellow background).
- **Fractions**: `mfrac`, with or without a bar (`linethickness`), the bar on the math axis.
- **Radicals**: `msqrt` and `mroot`, with a radical sign that stretches over the base and a raised index.
- **Scripts**: `msub`, `msup`, and `msubsup`, placed by the script constants of the math font.
- **Under and over**: `munder`, `mover`, and `munderover`, with limits, accents, stretchy horizontal operators (arrows,
  bars, braces), and movable limits that become scripts outside display style.
- **Prescripts**: `mmultiscripts` with `mprescripts` and `none`.
- **Tables**: `mtable`, `mtr`, and `mtd`, with spans, centered on the math axis.
- **Semantics and actions**: `semantics` and `maction` draw their first child; `annotation` and `annotation-xml` are
  kept, reported by a probe, and never drawn.
- **Invalid markup**: an element without the children MathML Core requires (an `mfrac` or `mroot` without exactly two, a
  script without its base and scripts) is drawn as a row of its children with the warning `mathml-child-count`, as
  browsers draw it.

### Supported MathML Attributes

An attribute with a value it cannot use is ignored with the warning `mathml-attribute-invalid`; an attribute outside
this list is ignored and reported as `mathml-attribute-unsupported`. These items are also the bullets of the help card
`mathml/attributes`; a test keeps them the same.

- **Global attributes**: `displaystyle`, `scriptlevel` (a number, +N, or -N), `mathvariant`, `mathsize`, `mathcolor`,
  `mathbackground`, `style`, and `dir="ltr"` on every element; `id`, `class`, `data-*`, `intent`, `arg`, `nonce`,
  `tabindex`, `autofocus`, and event handlers are accepted and change nothing in the picture.
- **mathvariant**: every MathML 3 value (normal, bold, italic, bold-italic, double-struck, script, bold-script, fraktur,
  bold-fraktur, sans-serif, bold-sans-serif, sans-serif-italic, sans-serif-bold-italic, monospace, initial, tailed,
  looped, and stretched) maps the characters to the Mathematical Alphanumeric Symbols, as Firefox draws them.
- **math**: `display` (block or inline) and `alttext`.
- **mo**: `form` (prefix, infix, postfix), `lspace`, `rspace`, `stretchy`, `symmetric`, `minsize`, `maxsize`, `largeop`,
  and `movablelimits` override the operator dictionary; `fence`, `separator`, and `accent` are accepted and change
  nothing.
- **mfrac**: `linethickness` (a length; 0 for no bar).
- **munder, mover, and munderover**: `accentunder` and `accent` (true or false).
- **mspace and mpadded**: `width`, `height`, and `depth`, and on mpadded also `lspace` and `voffset`.
- **Tables**: `columnalign`, `rowalign`, `columnspacing`, `rowspacing`, `framespacing`, `columnlines`, `rowlines`, and
  `frame` on mtable, `columnalign` and `rowalign` on mtr and mtd, and `columnspan` and `rowspan` on mtd, drawn as
  Firefox draws them (Chrome ignores the table attributes).
- **maction and semantics**: `actiontype`, `selection`, `encoding`, and `definitionURL` are accepted; the first child is
  drawn.
- **Lengths**: a number with em, ex, px, pt, pc, in, cm, or mm, a bare 0, and the named spaces veryverythinmathspace to
  veryverythickmathspace (and their negative forms where a negative length is allowed, as in lspace); the width of
  mspace cannot be negative, and a percentage is ignored on mspace and mpadded.
- **Colors**: CSS colors - names, #RGB, #RGBA, #RRGGBB, #RRGGBBAA, rgb(), rgba(), hsl(), hsla(), and transparent.
- **Inline style**: `color`, `background-color`, `font-size`, `font-style`, `math-style`, `math-depth`, and `display:
  none`.

### Operators, Spacing, and Stretching

Every `mo` takes its spacing and its properties from the **operator dictionary** of MathML Core, by its character and
its **form**: the first child of a row is a prefix, the last a postfix, and any other an infix operator. So `=`, `+`,
and `&minus;` are spaced as relations and binary operators without any `mspace`, and a fence at the start or the end
of an `mrow` is a fence. The attributes of `mo` override the dictionary.

- **Stretchy operators.** Parentheses, brackets, braces, bars, and arrows stretch: a fence to the height of its row,
  symmetrically about the math axis, and an arrow, a bar, or a brace over or under a base to its width. Stretching uses
  the size variants and glyph assemblies of the font's `MATH` table, then the Unicode-based assemblies of MathML Core,
  then a rule for the line characters. A stretched operator is centered in the width of its widest size, as Chrome
  draws it.
- **Large operators.** A sum, a product, or an integral is drawn large in display style. A sum or a product with
  `munderover` has its limits under and over it in display style and as scripts in an inline equation (movable limits);
  write the limits of an integral with `msubsup`, so they stay beside the sign.
- **Invisible operators.** `&InvisibleTimes;` (U+2062) between factors and `&ApplyFunction;` (U+2061) after a function
  name take no space and draw nothing; they tell the layout and a screen reader what the juxtaposition means.

A sum with limits, a stretchy fence around a fraction, and an infix relation, drawn in display style:

```xml
<math xmlns="http://www.w3.org/1998/Math/MathML" display="block">
  <munderover>
    <mo>&sum;</mo>
    <mrow><mi>k</mi><mo>=</mo><mn>1</mn></mrow>
    <mi>n</mi>
  </munderover>
  <msup>
    <mrow><mo>(</mo><mfrac><mn>1</mn><mn>2</mn></mfrac><mo>)</mo></mrow>
    <mi>k</mi>
  </msup>
  <mo>=</mo>
  <mn>1</mn>
  <mo>&minus;</mo>
  <msup>
    <mrow><mo>(</mo><mfrac><mn>1</mn><mn>2</mn></mfrac><mo>)</mo></mrow>
    <mi>n</mi>
  </msup>
</math>
```

### Writing Good-Looking Equations

These are the mistakes that make an equation look wrong, and the markup to write instead (the help card
`mathml/pitfalls` lists them too):

- **Minus.** Write `&minus;` (U+2212); the hyphen `-` is drawn as a short hyphen.
- **Negative numbers.** Wrap a sign and its number in their own row, `<mrow><mo>&minus;</mo><mn>40</mn></mrow>`: after
  an `=` a bare minus is an infix operator and is spaced like a subtraction (T = - 40).
- **Continuation rows.** A `+` that starts a continuation row of an aligned table is first in its row, so it is a tight
  prefix sign; write `<mo form="infix">+</mo>`.
- **Primes.** Write a prime after its base as an operator, `<mi>f</mi><mo>&prime;</mo>`. The prime glyph is already
  raised; `<msup><mi>f</mi><mo>&prime;</mo></msup>` draws it tiny and too high.
- **Hats.** Write a hat as `<mover accent="true"><mi>x</mi><mo>&#x2C6;</mo></mover>`; `^` is drawn large and offset.
- **Function names.** After `&ApplyFunction;` and before a bare argument add `<mspace width="0.1667em"/>`; without it
  x touches sin.
- **Short arguments.** Write `<mo stretchy="false">(</mo>` for the parentheses of f(x): a stretchy fence is centered in
  the width of its widest size and looks loose around one letter.
- **Units.** Write a unit upright, with a thin space after the number: `<mn>9.81</mn><mspace width="0.1667em"/>`
  `<mi mathvariant="normal">m</mi>`. A one-letter unit needs `mathvariant="normal"`.
- **Long equations.** There is no automatic line breaking: split a long equation into the rows of a table aligned at
  the equals sign (`<mtable columnalign="right left" columnspacing="0">`).

This equation follows the advice; it is drawn in strict mode without a diagnostic:

```xml
<math xmlns="http://www.w3.org/1998/Math/MathML" display="block">
  <mi>f</mi>
  <mo>&prime;</mo>
  <mo stretchy="false">(</mo>
  <mi>x</mi>
  <mo stretchy="false">)</mo>
  <mo>=</mo>
  <mi>cos</mi>
  <mo>&ApplyFunction;</mo>
  <mspace width="0.1667em"/>
  <mi>x</mi>
  <mo>&minus;</mo>
  <mi>sin</mi>
  <mo>&ApplyFunction;</mo>
  <mspace width="0.1667em"/>
  <mi>x</mi>
</math>
```

### MathML Unsupported Features and Strict Mode

A construct outside MathML Core is reported as a `MathMlUnsupportedFeature` with a stable `Feature` code, a message, a
location, and a hint, and the rest of the equation is still drawn. `ThrowOnUnsupportedFeature` turns the first one into
a `MathMlUnsupportedFeatureException`. A **warning** (`MathMlDiagnostic`) is a questionable construct that still
renders; strict mode does not fail on a warning.

The codes, with their kind and when each is reported:

- **`mathml-element-unsupported`**: Unsupported: An element outside MathML Core (an element of MathML 4 Full such as
  `mfenced`, `menclose`, `mglyph`, `mlabeledtr`, or `mstack`, or of Content MathML such as `apply`), drawn as a row of
  its children; an element of another namespace or inside a token, skipped; or text directly inside a layout element,
  drawn like an `mtext`.
- **`mathml-attribute-unsupported`**: Unsupported: An attribute that has no effect in MathML Core, such as `linebreak`,
  `columnwidth`, `bevelled`, the quotes of `ms`, `dir="rtl"` (the equation is laid out left to right), or a link
  (`href`, `xlink:href`), which is never followed; it is ignored.
- **`mathml-css-unsupported`**: Unsupported: A declaration of the `style` attribute outside the supported subset (such
  as `border` or `padding`); it is ignored.
- **`mathml-syntax-error`**: Error (mathml-error): the text is not valid UTF-8 or not well-formed XML, declares a
  document type or an encoding other than UTF-8, uses a named reference that is not in the W3C set, or its root is not
  `math`; the message gives the file, the line, and the column (an empty file is line 1, column 1), and the hint
  suggests the fix.
- **`mathml-limit-exceeded`**: Error (budget-exceeded): a limit of the library (elements, nesting, token length, table
  cells, layout work, painted glyphs, or a picture larger than the largest image); see the card `limits`.
- **`mathml-render-failed`**: Error (mathml-error): the equation cannot be laid out; please report it.
- **`mathml-attribute-invalid`**: Warning: an attribute value its attribute cannot use (a length without a unit, a
  negative width, an unknown keyword); the attribute is ignored.
- **`mathml-child-count`**: Warning: an element without the children MathML Core requires (an `mfrac` or `mroot` without
  exactly two); it is drawn as a row of its children.
- **`mathml-text-missing-glyph`**: Warning: a character that neither the math font nor the bundled fallback fonts have;
  it is drawn as a box.
- **`mathml-stretch-unavailable`**: Warning: an operator that must stretch, but that neither the font nor a fallback can
  stretch; it keeps its normal size.
- **`mathml-diagnostics-suppressed`**: Warning: more reports than the diagnostics limit; the rest are not listed.

The codes of this list are also the bullets of the help cards `mathml/unsupported` and `mathml/diagnostics` (of the
Viewer's `CanvasNetHelp`, and `canvasnet help mathml unsupported` and `diagnostics`); a test keeps them the same. In the
`canvasnet` tool and the Viewer, the errors are `mathml-error` (with the file, the line, the column, and the specific
code), a limit is `budget-exceeded`, a text over the size limit is `corrupt-input`, and a warning is a note with the
source `mathml` and the code `mathml-warning`, the specific code being in its `detail`.

Not read or not drawn at all:

- Line breaking: an equation is drawn on one line however long it is.
- LaTeX: a LaTeX formula is not read; write MathML (a LaTeX front end is planned in issue #198), and keep the LaTeX in
  an annotation.
- Content MathML (`apply`, `ci`, `cn`, ...): reported as `mathml-element-unsupported` and drawn as rows of its
  children.
- Right-to-left math (`dir="rtl"`): reported and laid out left to right.
- Style sheets: only the inline `style` attribute (its supported subset) and the MathML attributes style an equation.
- Interaction: `maction` draws its first child, and links are never followed; the picture is static.

### Fonts, Determinism, and Known Differences

Rendering is **deterministic**: two renders of one document with the same options give identical pixels, for every
degree of parallelism, on every machine, and in every culture. The package reads no clock, environment variable,
locale, or installed font, runs no script, and never uses the network. Every glyph comes from the bundled Noto Sans
Math, and a character it lacks from the bundled fallback fonts of the core (Noto Sans first), with the warning
`mathml-text-missing-glyph` when none has it.

The layout is checked against Chrome: a set of original reference equations is drawn by Chrome (and the table
attributes by Firefox) with the same font, and the boxes of every element agree within one CSS pixel and one layout unit
(`tools/visual-diff` draws the references again; the build never runs a browser). The known differences are:

- **Glyph shaping.** Glyphs are placed by their advances, without the script variants (`ssty`), the dotless forms
  (`dtls`), or the kerning of the font.
- **Weights.** Noto Sans Math has one regular weight and `font-weight` is not supported; letters and digits are bold
  with `mathvariant="bold"`.
- **Stretched operators.** A stretched operator is centered in the width of its widest size, as Chrome draws it; an
  accent or an overscript is centered over its base instead of placed at the top-accent point of a slanted letter.
- **Mathvariant and tables.** Every `mathvariant` value is drawn, and the table attributes (`columnalign`,
  `rowspacing`, `columnlines`, `frame`, ...) are drawn as Firefox draws them; Chrome draws only `mathvariant="normal"`
  and ignores the table attributes.
- **ms and percentages.** `ms` is drawn without quotes, as MathML Core and Chrome draw it, and the percentages of
  `mspace` and `mpadded` are ignored, as MathML Core says.
- **Picture box.** The picture is the content box united with the ink and a one-pixel margin around the ink, so an
  overhanging glyph, a stretched fence, or the tail of a radical is never clipped; a browser's box may be a pixel or
  two smaller.
- **Slanted letters.** A slanted letter gets its italic correction as clearance before a fence or a superscript where
  its ink would otherwise collide with it (the 2 of ζ², the k of y to the k, a parenthesis after *f*); Chrome lets them
  touch.
- **Integrals with limits.** A scripted element ends after its scripts as they are placed (the lower limit of an
  integral is pulled back under the sign), so the integrand follows the limits about a quarter em after them, close to
  TeX; Chrome counts the whole lower limit after the sign and leaves nearly 1em there.
- **Subscripts of letters.** The subscript of a single character stays at the normal subscript shift of the font
  whatever the descender of the character, as TeX sets it, so the subscript n of γ sits as low as the subscript n of
  x; MathML Core and Chrome lower it below the descender.
- **Errors.** For invalid text a browser draws what it can; this package throws a located `MathMlSyntaxException`.

The design documentation lists every difference of the layout in detail.

### MathML Third-Party Attribution

The code of `DemaConsulting.CanvasNet.MathMl` is original: the layout is implemented from the text of MathML Core, and
no source, example, or test of a browser or of the specifications is copied. Five generated data tables are **derived
with attribution**: the 2125 named character references from the W3C *XML Entity Definitions for Characters* (HTML and
MathML set), the italic mapping, the operator dictionary, and the Unicode-based glyph assemblies of *MathML Core*
(W3C Software and Document License), and the `mathvariant` mappings from the Unicode Character Database (Unicode
License v3). The license texts ship in the NuGet package under `licenses/LICENSE-w3c.txt` and
`licenses/LICENSE-unicode.txt` (also in the `canvasnet` tool, which bundles the assembly), and
`src/DemaConsulting.CanvasNet.MathMl/third-party/README.md` records the sources, their versions, and the generator.
The package is **not affiliated with, endorsed by, or promoted by** the W3C or Unicode, Inc.; the names are used only to
describe the language that is implemented and the data that is used. The bundled Noto Sans Math font and its license
belong to the core package.

### Using As An Embedded Image

`MathMlImageSource` is the image source of this package for `.mml` files. With it a document that links to an equation
(an SVG `<image href="eq.mml">`, the picture of a Mermaid node, the `image` mark of a Vega-Lite chart) draws the
equation through a `FolderImageResolver` of the core package, or `CanvasNetViewer.CreateImageResolver` of the Viewer
package, which includes it. The equation is laid out afresh at the size of its placement (scaled uniformly and
centered), on a transparent background, from the content of the request alone: it reads no other file. Warnings are
returned as notes of the source `mathml` with the code `mathml-warning` (the specific `mathml-*` code is in `Detail`),
and unsupported features as notes whose code is the feature.

The source declares one **fragment parameter**: `display=block` or `display=inline` (in any letter case) sets the
display mode, so `sum.mml#display=inline` draws a sum with its limits as scripts. Any other value refuses the link with
`image-fragment-invalid` before the file is read, and another parameter is reported as
`image-fragment-unknown-parameter`. An equation reports no feature positions.

```csharp
// An SVG document that links to equations, drawn through the resolver
var resolver = new FolderImageResolver("assets", [new MathMlImageSource()]);
var options = new SvgLoadOptions { ImageResolver = resolver };
using var surface = SvgCodec.Load("assets/figure.svg", 800, 600, fonts: null, options);
```

Place each equation at its natural size times one factor for the whole document, so every equation has the same text
size: the quadratic formula of Example 78 is 138 by 44 pixels at its natural size, so a placement of 276 by 88 draws it
at twice that size.

## Examples

### Example 78: Rendering an Equation

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.MathMl;

// MathMlDocument.Open("quadratic.mml") would read the same text from a file
var equation = MathMlDocument.Parse("""
    <math xmlns="http://www.w3.org/1998/Math/MathML" display="block"
          alttext="x equals minus b plus or minus the square root of b squared minus 4 a c, all over 2 a">
      <mi>x</mi>
      <mo>=</mo>
      <mfrac>
        <mrow>
          <mo>&minus;</mo>
          <mi>b</mi>
          <mo>&PlusMinus;</mo>
          <msqrt>
            <msup><mi>b</mi><mn>2</mn></msup>
            <mo>&minus;</mo>
            <mn>4</mn>
            <mo>&InvisibleTimes;</mo>
            <mi>a</mi>
            <mo>&InvisibleTimes;</mo>
            <mi>c</mi>
          </msqrt>
        </mrow>
        <mrow>
          <mn>2</mn>
          <mo>&InvisibleTimes;</mo>
          <mi>a</mi>
        </mrow>
      </mfrac>
    </math>
    """);

// The document describes the text
Console.WriteLine($"{equation.Display}, {equation.ElementCount} elements");  // Output: Block, 22 elements

// Measure lays the equation out without drawing; Render draws it sharply at three times the size
MathMlSize natural = equation.Measure();
Console.WriteLine($"{natural.Width}x{natural.Height}, baseline {natural.Baseline}");
                                                                             // Output: 138x44, baseline 32
using var picture = equation.Render(new MathMlRenderOptions { Scale = 3, Background = new Rgba32(255, 255, 255, 255) });
PngCodec.Save(picture, "quadratic.png");                                     // 413x131 pixels, white background

// The same equation inline: the fraction is compact
MathMlSize inline = equation.Measure(new MathMlRenderOptions { Display = MathMlDisplay.Inline });
Console.WriteLine($"{inline.Width}x{inline.Height}");                        // Output: 96x33

// Fit the equation into a fixed box over a transparent background: it is scaled uniformly and centered
using var thumbnail = equation.Render(300, 100);
PngCodec.Save(thumbnail, "quadratic-thumbnail.png");
```

The Viewer draws a file in one call, over white, and probes it for its facts:

```csharp
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasNet.Viewer.Imaging;

var info = SourceLoader.Probe(InputReference.Parse("quadratic.mml#display=inline"));
Console.WriteLine($"{info.Equation!.Display}, {info.Width}x{info.Height}");  // Output: inline, 96x33

using var image = CanvasNetViewer.Render("quadratic.mml", new ViewerRenderOptions { Width = 414, ThrowOnUnsupportedFeature = true });
Console.WriteLine($"{image.Format} {image.Surface.Width}x{image.Surface.Height}");
```

### Example 79: MathML Errors, Unsupported Features, and Strict Mode

```csharp
using DemaConsulting.CanvasNet.MathMl;

// Markup that is not well-formed is located in the original text and carries a hint
try
{
    MathMlDocument.Parse("<math>\n  <mi>x</mo>\n</math>", new MathMlOpenOptions { SourceName = "bad.mml" });
}
catch (MathMlSyntaxException ex)
{
    Console.WriteLine($"{ex.Code} at {ex.Location}");  // Output: mathml-syntax-error at bad.mml:2:10
    Console.WriteLine(ex.Hint);                        // Output: check that every element is closed and that ...
}

// A misspelled named reference is a syntax error too
try
{
    MathMlDocument.Parse("<math><mi>&alfa;</mi></math>", new MathMlOpenOptions { SourceName = "greek.mml" });
}
catch (MathMlSyntaxException ex)
{
    Console.WriteLine(ex.Message);
    // Output: greek.mml:1:11: mathml-syntax-error: '&alfa;' is not a named character reference of the W3C ...
}

// An element outside MathML Core is drawn as a row and reported; strict mode makes it an error
var fenced = MathMlDocument.Parse("<math>\n  <mfenced><mi>x</mi></mfenced>\n</math>",
    new MathMlOpenOptions { SourceName = "fenced.mml" });
try
{
    using var strict = fenced.Render(new MathMlRenderOptions { ThrowOnUnsupportedFeature = true });
}
catch (MathMlUnsupportedFeatureException ex)
{
    Console.WriteLine($"{ex.Feature} at {ex.Location}");  // Output: mathml-element-unsupported at fenced.mml:2:4
    Console.WriteLine(ex.Hint);                           // Output: write the fences and separators as <mo> ...
}

// A value an attribute cannot use is a warning; the equation is still drawn
var warnings = new List<MathMlDiagnostic>();
var spaced = MathMlDocument.Parse("<math>\n  <mi>x</mi>\n  <mspace width=\"3\"/>\n</math>",
    new MathMlOpenOptions { SourceName = "space.mml" });
using var drawn = spaced.Render(new MathMlRenderOptions { WarningHandler = warnings.Add });
Console.WriteLine(warnings[0]);
// Output: space.mml:3:11: mathml-attribute-invalid: '3' is not a valid value of the attribute 'width'; it is ignored
```

Through the Viewer and the tool the same failures are `ViewerException`s: the syntax error is `mathml-error` with the
`Location` and the `DetailCode` `mathml-syntax-error`, the element in strict mode is `unsupported-feature` with the
`DetailCode` `mathml-element-unsupported`, and the warning is a note with the code `mathml-warning` and the detail
`mathml-attribute-invalid`.

### Example 80: An Equation in an SVG Figure, a Mermaid Node, and a Vega-Lite Chart

Save the quadratic formula of Example 78 as `quadratic.mml` and link it like any picture. An SVG figure places it at
twice its natural size (138 by 44):

```xml
<svg xmlns="http://www.w3.org/2000/svg" width="480" height="160" viewBox="0 0 480 160">
  <rect width="480" height="160" fill="#f8fafc"/>
  <text x="24" y="40" font-family="sans-serif" font-size="18">The roots of a quadratic</text>
  <image href="quadratic.mml" x="102" y="56" width="276" height="88"/>
</svg>
```

A node of a Mermaid flowchart shows it at one and a half times its natural size:

```text
flowchart LR
    eq@{ img: "quadratic.mml", label: "Roots", w: 207, h: 66 }
    eq --> check[Check the discriminant]
```

An `image` mark of a Vega-Lite chart shows it through its `url` channel:

```json
{
  "width": 300, "height": 120, "padding": 0,
  "data": { "values": [ { "x": 5 } ] },
  "mark": { "type": "image", "width": 207, "height": 66 },
  "encoding": {
    "x": { "field": "x", "type": "quantitative", "scale": { "domain": [0, 10] }, "axis": null },
    "y": { "value": 60 },
    "url": { "value": "quadratic.mml" }
  }
}
```

Each of the three is drawn by `CanvasNetViewer.Render` (or `canvasnet render`) in strict mode without a diagnostic, and
lists the equation once among its embedded images. The equation stays transparent, so the page color shows around it.
Flattening (`SvgFlattener`, `canvasnet flatten`) replaces the linked `.mml` with a PNG picture for SVG readers that
cannot draw MathML.

### Example 81: Drawing an Equation in a Pandoc Document

A document built with Pandoc and the CanvasNet filter (see *Using the Tool in Pandoc Documents* in the tool guide) draws
an equation written in a fenced code block of class `mathml`, or linked as a `.mml` picture such as
`![The roots of a quadratic](quadratic.mml)`. The equation is drawn at twice its natural size, so it stays sharp in
print, and shown at the size of the text around it: when the author gives no `size`, `width`, or `height`, the filter
adds its natural width in em (`width="Nem"`, the natural width over the 16 pixel font size), which browsers and PDF
renderers such as WeasyPrint honor. An equation in a sentence, such as `the roots ![x](quadratic.mml) of the
equation`, also sits on the line of text with its baseline on the text's; an equation alone in its paragraph is shown
on its own. With a `caption` it becomes a figure, like the one below, which this guide draws as it is built:

````markdown
```{.mathml caption="Ohm's law and the power it dissipates"}
<math xmlns="http://www.w3.org/1998/Math/MathML" display="block">
  <mi>P</mi><mo>=</mo><mi>V</mi><mo>&InvisibleTimes;</mo><mi>I</mi>
  <mo>=</mo><msup><mi>I</mi><mn>2</mn></msup><mo>&InvisibleTimes;</mo><mi>R</mi>
  <mo>=</mo><mfrac><msup><mi>V</mi><mn>2</mn></msup><mi>R</mi></mfrac>
</math>
```
````

```{.mathml caption="Ohm's law and the power it dissipates"}
<math xmlns="http://www.w3.org/1998/Math/MathML" display="block">
  <mi>P</mi><mo>=</mo><mi>V</mi><mo>&InvisibleTimes;</mo><mi>I</mi>
  <mo>=</mo><msup><mi>I</mi><mn>2</mn></msup><mo>&InvisibleTimes;</mo><mi>R</mi>
  <mo>=</mo><mfrac><msup><mi>V</mi><mn>2</mn></msup><mi>R</mi></mfrac>
</math>
```

An equation file is linked like any other picture. The quadratic formula of Example 78 is the file
`examples/mathml-quadratic.mml` of this guide, and `![The roots of a quadratic](examples/mathml-quadratic.mml)` draws
it, also at the size of the text:

![The roots of a quadratic](examples/mathml-quadratic.mml)

A `theme` attribute is refused (an equation has none), and `size`, `scale`, `width`, `height`, and `style` work as for
any other picture of the filter (a `width` or `height`, as an attribute or in the `style`, replaces the natural size).

### Example 82: The MathML Gallery

The gallery is a set of 16 equations from algebra, analysis, statistics, physics, and control engineering (the quadratic
formula of Example 81 is one of them) that together use the major constructs of the package: fractions, square roots and
roots with an index, scripts and prescripts, sums, products, and integrals with their limits, stretchy fences, matrices,
cases, aligned derivations, accents, braces, and units. Every equation is **original to this repository**, lies in
`docs/user_guide/examples` as `mathml-*.mml`, follows the advice of *Writing Good-Looking Equations*, has a text
alternative, and is drawn below by the Pandoc filter at its natural width, so the pictures are always those of the
version of the library that built this guide. A test (`MathMlGalleryTests`) renders every file with
`ThrowOnUnsupportedFeature = true` and fails on any warning, so an equation of the gallery cannot use a construct that
is only reported. The same files can be drawn with the tool, for example
`canvasnet render docs/user_guide/examples/mathml-maxwell.mml --strict --scale 3 -o maxwell.png`.

*Fractions and roots.* The golden ratio as a continued fraction (`mfrac` with `displaystyle="true"` in the nested
levels) and in closed form with `msqrt`:

![The golden ratio](examples/mathml-golden-ratio.mml)

*A root with an index and a product.* `mroot` with the index n, a product with `munderover`, and a stretchy fence with a
superscript:

![The geometric mean](examples/mathml-geometric-mean.mml)

*A sum with limits.* `munderover` with an infinite upper limit, a fraction, and a condition in `mtext`:

![The geometric series](examples/mathml-geometric-series.mml)

*An integral with limits.* `msubsup` on the integral sign, a fraction with a square root, and an exponent that holds a
fraction:

![The normal distribution is normalized](examples/mathml-normal-distribution.mml)

*A series with function names.* `cos` and `sin` with `&ApplyFunction;` and a thin space before a fraction argument,
inside fences that stretch around the fractions:

![A Fourier series](examples/mathml-fourier-series.mml)

*An aligned derivation.* A table with `columnalign="right left"` and `columnspacing="0"`, aligned at the equals signs,
with tight fences (`stretchy="false"`) around the short arguments:

![Expanding a square](examples/mathml-derivation.mml)

*Cases.* A stretchy brace before a table with `columnalign="left left"`, and `mtext` for the conditions:

![The sign function](examples/mathml-cases.mml)

*Matrices.* Three tables inside stretchy parentheses, primes written as operators after their base, and function names:

![A rotation by an angle](examples/mathml-rotation-matrix.mml)

*Tensor indices.* `mmultiscripts` with `none` to stagger the upper and lower indices of the connection coefficients of
a metric, and partial derivatives with subscripts:

![The connection coefficients of a metric](examples/mathml-tensor.mml)

*Prescripts and accents.* `mmultiscripts` with `mprescripts` for the mass and atomic numbers, a superscript sign, and a
bar accent over a Greek letter:

![The beta decay of carbon 14](examples/mathml-isotopes.mml)

*Braces and labels.* A sum under a stretchy underbrace (U+23DF) with a text label, and a bar accent for the mean:

![The sample variance](examples/mathml-sample-variance.mml)

*Contour integrals.* Two of Maxwell's equations in integral form, aligned in a table in display style, with bold
vectors (`mathvariant="bold"`) and upright differentials:

![Gauss's law and Faraday's law](examples/mathml-maxwell.mml)

*A transfer function.* A PID controller with upright subscripts (`mathvariant="normal"`) and tight parentheses:

![A PID controller](examples/mathml-pid-controller.mml)

*A state-space model.* A discrete-time model aligned in a table, with bold matrices and vectors and compound
subscripts:

![A discrete-time state-space model](examples/mathml-discrete-state-space.mml)

*Units.* Upright units after a thin space, a dot operator, and a unit inside a fence with an exponent:

![Kinetic energy](examples/mathml-kinetic-energy.mml)

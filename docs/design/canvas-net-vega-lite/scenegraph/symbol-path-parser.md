### SymbolPathParser

![Scenegraph Structure](VegaLiteScenegraphView.svg)

The `SymbolPathParser` unit is the fifth software unit in the `Scenegraph` subsystem. It is one source file,
`SymbolPathParser.cs`, in the `DemaConsulting.CanvasNet.VegaLite.Scenegraph` namespace, and defines one `internal`
sealed class, `SymbolPathParser`: a parsed SVG path with static entry points that parse and cache (`TryParse`, `TryGet`,
`IsValid`) and an instance method that draws (`Render`). The file carries the attribution header comment for the Vega
behavior it follows.

#### Purpose

`SymbolPathParser` parses and draws the **custom symbol shape** of a chart: a `shape` that is not a symbol name, that is
the `shape` of a point mark, a `shape` channel value or `scale.range` entry, and the `symbolType` of a legend, as SVG
path data (`"M0,-1L1,1L-1,1Z"` for a triangle, for example). It is a
port of the way vega-scenegraph draws such a path, so that a chart with a custom shape has the same outline, the same
size, and the same legend bounds as Vega's. It exists as a unit of its own because the path grammar, the canvas
rules, and the arc conversion are a closed body of logic that tests can assert analytically, without pixels.

The unit's behavior is specified by the `CanvasNetVegaLite-Scenegraph-SymbolPathParser-*` requirements in
`docs/reqstream/canvas-net-vega-lite/scenegraph/symbol-path-parser.yaml`: `-Parse`, `-RenderCanvasRules`, `-Bounds`,
`-Cache`, and `-Scaling`. The code follows vega-scenegraph, re-implemented from its public description (see
_CanvasNetVegaLite System Design_, Third-Party Provenance and Attribution).

#### Data Model

A parsed path is an immutable list of commands (a letter and its numbers, after the implicit repetition) and a small
memo of the last drawing. The memo is guarded by a lock, so one parsed path may be drawn from several threads; the
parsed paths are shared through a cache that is guarded by a lock too.

**Parse** (`TryParse`, vega-scenegraph `src/path/parse.js`). The text is read as vega reads it:

- Text before the first command letter is skipped; a text that has **no command letter** parses to an empty path
  (`CommandCount` is 0), which draws nothing.
- The command letters are `m l h v z c s q t a` in either case, with 2, 2, 1, 1, 0, 6, 4, 4, 2, and 7 numbers. A
  character that is not a command letter belongs to the numbers of the command before it, so an unknown letter is an
  error, as in vega.
- A number is `[+-]?(digits.digits | digits. | .digits | digits)` with an optional exponent; numbers are separated by
  white space and an optional comma (the white space of JavaScript's `\s`), and the two flags of an arc are a single `0`
  or `1`, so `a1 1 0 00.5 .5` reads the flags without a separator.
- More numbers than one repetition **repeat the command** (a move-to repeats as a line-to of the same case); a count of
  numbers that is not a multiple of the parameter count, or a missing or malformed number, is an error.
- A relative smooth quadratic curve (`t`) as the first command is an error (vega reads the previous command and throws),
  and so is a path of more than `MaxCommands` (4,096) commands after the repetition.

**Render** (`Render(size, out SceneBounds)`, vega-scenegraph `src/path/symbols.js`, `render.js`, `arc.js`). The path is
drawn the way `customSymbol` draws it: the coordinates are **scaled by the symbol radius**, `sqrt(size) / 2`, on both
axes, so a coordinate of 1 in the path is one radius from the center of the symbol (a scale of exactly 1 is not scaled at
all, as in vega). The scale applies to the
numbers of a command as `render.js` applies it: the radii and the end point of an arc (not its rotation or flags), the
one number of `h` and `v`, and every other number. The drawing keeps the current point, the start of the sub-path, and
the persisted control points, and follows `render.js` including its **quirks**:

- `s` and `S` reflect the persisted control point whatever the previous command was (the SVG rule would use the current
  point after a command that is not a curve).
- When the path is scaled, a `t` always sees `t` as the previous command (the scaled copy of the command is the array
  that vega later reads as the previous command), so its control point is NaN: the canvas skips the curve, while the
  bounds still add its end point.
- An arc is drawn as cubic curves of at most a quarter turn each (`arc.js`: the center parameterization, the radii that
  are raised when they are too small, the rotation, and the flags).
- The canvas rules apply to the recording of the path: a drawing command without a current point starts the sub-path at
  its own point, a command after a close starts a new sub-path at the start of the closed one, and **an operation that
  has a number that is not finite is skipped** (the bounds still see it, as in vega).

**Bounds** (vega-scenegraph `src/bound/boundContext.js`). The same calls feed a bounds context: every end point is added,
and the **extrema of a quadratic or cubic curve** (the parameter values where a derivative is zero) are added too, so the
bounds are those of the drawn curve and not of its control polygon. `Render` returns them in the coordinates of the
symbol's center; they are empty when the path draws no point. The legend uses them for the space of the symbol.

**Cache.** `TryGet` keeps parsed paths by their exact text (ordinal comparison) in a process-wide cache that is guarded
by a lock and bounded three ways: at most 256 entries, at most 262,144 characters of path text in total, and no text
longer than 8,192 characters. The cache is cleared when an entry would exceed the entry or the character budget. A
text that does not parse is cached too, as a failure (so a shape field that repeats one malformed path is scanned once,
not once per row), when its text is within the same length bound; a text longer than 8,192 characters is never cached
and is parsed again on each call. Every cached text counts its characters toward the budget, so a long-lived process
that renders many different specifications retains a bounded amount of path text. `Render`
remembers the last size (a symbol mark mostly has one size) and returns the same `Path` and bounds for it.

**Limits** (every loop or allocation of the unit): the number of commands is at most `MaxCommands` (4,096) and the
numbers read are bounded by seven times that; the parse and the drawing are linear in the number of commands, an arc
adds at most four cubic curves, and the cache holds at most 256 entries and 262,144 characters of text, so the work
and the memory are bounded.

**Determinism.** The unit uses only its arguments; numbers are read with the invariant culture, and no clock or random
source is used. The same text and size give the same path on every call; the path stores single precision numbers, as
the core `Path` does.

#### Key Methods

- `SymbolPathParser.TryParse(string text, out string? error)` - the parsed path, or null and the reason.
- `SymbolPathParser.TryGet(string text)` - the same through the cache; `IsValid(text)` is `TryGet(text) is not null`.
- `SymbolPathParser.Render(double size, out SceneBounds bounds)` - the core `Path` scaled to the size and its bounds.
- `SymbolPathParser.CommandCount` - the number of commands after the repetition.

#### Error Handling

The unit does not throw for a text that is not a path: `TryParse` returns null with a message, and a null text is a
programming error that throws `ArgumentNullException`. **Deviation from Vega**: vega throws for a path that does not
parse (and the chart is not rendered); the renderer instead reports the warning `vl-shape-path-invalid` (see
_FeatureCatalog_), and the painter draws a circle for the shape (`ScenePainter.SymbolPath`). A path with no command is
valid and draws nothing; the catalog reports it too.

#### Dependencies

- **`Path`** and **`PathBuilder`** - the core CanvasNet path types (`Geometry`)
- **`SceneBounds`** - the bounds type of the scene model, in the same subsystem
- **.NET base class library** - `System.Math`, `System.Globalization`, and `System.Numerics` (`Vector2`)

#### Callers

`ScenePainter.SymbolPath` draws a custom shape with `TryGet(shape)` and `Render(size, ...)` and falls back to a circle;
`ScenePainter.IsDrawableShape` and `ScaleResolver.IsKnownShape` use `IsValid`; `LegendBuilder` (the symbol bounds)
uses the
bounds of `Render`; and the `FeatureCatalog` calls `TryParse` to report `vl-shape-path-invalid`. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Scenegraph/SymbolPathParserTests.cs` call the type through
`InternalsVisibleTo`.

## ArgumentReader

<!-- cspell:ignore canvasnet Postconditions rgba dpi rrggbbaa -->

![CanvasNetTool Structure](CanvasNetToolView.svg)

### Purpose

`ArgumentReader` is the command-line grammar of the tool. Its single responsibility is to turn the argument list of
one command (everything after the command name except the global options, which `Context` has already removed)
into options and positional arguments using the command's `OptionSpec` table, and to parse the typed values that
options and operands carry. Putting the grammar in one unit means every command accepts the same spellings, gives
the same error messages, and cannot drift from the options it declares in its help.

### Data Model

**ParsedOption** (`readonly record struct`): `Name` (the canonical `OptionSpec.Name`, even when an alias was typed)
and `Value` (`string?`; `null` for a flag).

**ArgumentReader** (`sealed partial class`, files `ArgumentReader.cs` and `ArgumentReader.Values.cs`) holds the
command name (for messages), the set of declared option names, the ordered list of `ParsedOption`, and the list
of positional arguments. It is immutable once `Parse` has returned.

**Supporting value types** (declared in `ArgumentReader.cs`, documented here rather than as units):

- `Dimension(double Value, bool IsPercent)` - a length in pixels or percent.
- `GeometrySpec(Dimension X, Dimension Y, Dimension Width, Dimension Height)` - a rectangle; percentages of `X` and
  `Width` are relative to the image width, of `Y` and `Height` to the image height.
- `PadSpec(int Left, int Top, int Right, int Bottom)` - border widths in pixels.
- `FlipAxis` - `Horizontal`, `Vertical`, or `Both`.

### Key Methods

**Parse(command, args, specs)**: reads the argument list and returns the reader.

- *Parameters*: `string command` (canonical command name, for messages); `IReadOnlyList<string> args`;
  `IReadOnlyList<OptionSpec> specs`.
- *Preconditions*: no argument is null.
- *Postconditions*: every option occurrence is recorded in command-line order under its canonical name; every other
  token is a positional argument.

Tokens are classified as follows, in this order:

<!-- markdownlint-disable MD013 -->
| Token | Meaning |
| ----- | ------- |
| any token after a bare `--` | positional |
| `--` | ends option parsing |
| a token not starting with a dash, a lone `-`, or a dash followed by a digit (or `.` and a digit), such as `-90` | positional |
| `--name=value` | the long option `--name` with an inline value (the first `=` splits; the value may contain `=`) |
| `--name` or `-a` (exact name or alias) | the option; a value option takes the next token as its value, whatever it looks like |
| `-aValue`, `-a=Value` (a single-letter alias that takes a value, with the value attached) | the option with the value `Value` |
| anything else | unknown option: a `usage-error` |
<!-- markdownlint-enable MD013 -->

A flag that is given an inline value, and a value option that is the last token, are `usage-error`s. An unknown
option is a `usage-error` whose hint suggests the closest declared name or alias: the one within an edit distance of
two (the distance function is shared with `CommandRegistry`), or the one the typed text is a prefix of; with no
near match the hint points at the command's help.

**Has / Get / GetAll(name)**: query by canonical name. `Get` returns the last occurrence's value; `GetAll` returns
every occurrence's value in order. Asking for a name the command did not declare throws `KeyNotFoundException`: it
is a defect of the calling command, not a user error, and so deliberately is not an expected-failure type.

**GetInt / GetDouble(name, min, max)**: the option's value as an integer or finite number within a range; `null`
when the option was not given.

**RequirePositionals(min, max, expected)**: throws a `usage-error` when the number of positional arguments is
outside the range; when `max` is 0 the message says the command takes no arguments.

**Value grammars** (`static`, in `ArgumentReader.Values.cs`). Each takes the text and the option or operand name
used in the message. Numbers always use the invariant culture.

<!-- markdownlint-disable MD013 -->
| Method | Accepts | Result |
| ------ | ------- | ------ |
| `ParseInt(text, option, min, max)` | optional sign and digits within the range | `int` |
| `ParseNumber(text, option, min, max)` | a finite decimal number within the range | `double` |
| `ParsePage(text, option)` | a whole number of at least 1 (1-based page) | `int` |
| `ParseDpi(text, option)` | a number above 0 and at most 2400 | `float` |
| `ParseSize(text, option)` | `WxH` (or `WXH`) in whole pixels, each at least 1 | `(int Width, int Height)` |
| `ParseGeometry(text, option)` | `x,y,w,h` or `WxH+X+Y` (`WxH` alone is origin 0,0); each value whole pixels (optionally `px`) or a percent with `%` (fraction allowed, at most 100); the width and height are above 0 | `GeometrySpec` |
| `ParseColor(text, option)` | `#RGB`, `#RGBA`, `#RRGGBB`, `#RRGGBBAA` (CSS order, alpha last; `#` optional), a CSS color name (any case), or `transparent` | `Rgba32` (straight alpha) |
| `ParseDegrees(text, option)` | a finite number, optionally followed by `deg` | `double` normalized to at least 0 and below 360 |
| `ParseFlip(text, option)` | `h`/`horizontal`, `v`/`vertical`, `both` (any case) | `FlipAxis` |
| `ParsePad(text, option)` | `N` or `l,t,r,b` in whole pixels | `PadSpec` |
| `ParseDefines(defs)` | `name=value` entries: a finite number is a `double`, `true`/`false` a `bool`, anything else a `string` (one pair of quotes removed) | `IReadOnlyDictionary<string, object?>` |
<!-- markdownlint-enable MD013 -->

`ParseColor` delegates the names and the hexadecimal digits to the core `CssColor` (`TryGetNamedColor`, which knows
all 148 CSS color names and `transparent`, and `TryParseHex`); it adds only the optional leading `#`. The `rgb()` and
`hsl()` function forms are deliberately not offered on the command line. `ParseColor` is deliberately not
`Rgba32.Parse`, which reads the alpha pair first. The `#` is optional because a shell treats an unquoted `#` at the
start of a word as a comment. A definition name is an identifier (letters, digits, `_`, and `$`, not starting with a
digit); a value starting with `[` (a vector) is rejected rather than silently turned into text.

### Error Handling

Every failure caused by the user's input is a `ToolException` with the code `usage-error`, a message that names the
option and the offending text, and a hint (an example, a suggestion, or a pointer to the help), so that it is
reported as an expected failure. A size or border wider than `Surface.MaxDimension` is the exception: its code is
`size-limit`. Null arguments throw `ArgumentNullException`; an undeclared option name passed to `Has`, `Get`, or
`GetAll` throws `KeyNotFoundException`.

### Dependencies

- **CommandRegistry** (Commands subsystem) - the `OptionSpec` record, and `EditDistance` for suggestions.
- **ToolException** and **ErrorCodes** (Program unit) - the failures.
- **CanvasNet core** - `Rgba32`, `CssColor` (the color names and hexadecimal digits behind `ParseColor`), and
  `Surface.MaxDimension`.

### Callers

- **Commands** - each command constructs a reader from its arguments and its option table, then asks for values;
  the value grammars are also called directly for positional operands.

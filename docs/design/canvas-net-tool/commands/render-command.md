### RenderCommand

<!-- cspell:ignore canvasnet jpeg tiff tif jpg -->

![Commands Structure](CommandsView.svg)

#### Purpose

`RenderCommand` is the `render` command and its alias `convert`: it loads an image or drawing and writes it as PNG,
JPEG, BMP, or TIFF. Its single responsibility is to turn a command line into one pass of load, transform, and
write, and to report the result. The unit is the partial class `RenderCommand` in `RenderCommand.cs` (the run and
the reporting) and `RenderCommand.Options.cs` (the option table and the reading of the settings).

#### Data Model

**OptionTable** (`IReadOnlyList<OptionSpec>`, also the command's `Options`): `--scale <factor>`, `--width
<pixels>`, `--height <pixels>`, `--size <WxH>`, `--background <color>`, `--no-auto-orient`, `-o`/`--output <file>`,
`--format <format>`, `--quality <1-100>`, and `--no-clobber`, in the order they are listed in the help. The options
of further source kinds (page, resolution, SCAD view) and of the image pipeline (crop, resize, rotate, ...) are
added to this table as they are implemented.

**Settings** (record): the `Input` path, the `Output` path, the `SourceOptions`, and the `WriteOptions`, produced
by `ReadSettings`.

The command's name is `render`, its alias is `convert`, and its usage text has examples.

#### Key Methods

**ReadSettings(reader)**: requires exactly one positional argument (the input); requires `-o`; reads the size
options (`--width` and `--height` as whole numbers of at least 1, `--scale` as a number from 0.001 to 8192, `--size`
with `ArgumentReader.ParseSize`, which refuses a side beyond the maximum with `size-limit`); rejects `--size`
combined with `--scale`, `--width`, or `--height`, and `--scale` combined with `--width` or `--height`
(`usage-error`); reads `--background` with `ParseColor`; reads `--quality` as 1 to 100 (default 90); and validates
`--format` early. A JPEG is flattened over the background color when it has some opacity, otherwise over white.
The option values reach `SourceLoader` and `ImageWriter` through `SourceOptions` and `WriteOptions`.

**Run(context, args)**: parses the arguments, reads the settings, then reports every mistake about the input and
the output before decoding anything: `SourceLoader.Probe` (missing, unreadable, unsupported, or corrupt header),
`ImageWriter.ResolveFormat`, and `ImageWriter.CheckTarget`. Then loads the source (`SourceLoader.Load(info,
options)`), writes it (`ImageWriter.Write`), and disposes the pixels. The result is written through
`context.WriteLine`: as text when `context.Json` is false, or as one JSON document.

**Pipeline hook**: between the load and the write, the image pipeline (crop, resize, rotate, flip, pad) is applied
by later releases; the operations it applies are appended to the list reported in the result.

**Text result**: `wrote {output} ({width}x{height} {format}, {bytes} bytes) from {input}`, for example
`wrote out.png (816x1056 png, 41208 bytes) from in.svg`.

**JSON result**: `ResultWriter.Success("render", ...)` with these fields after the envelope, in this order:

<!-- markdownlint-disable MD013 -->
| Key | Value |
| --- | ----- |
| `tool` | object: `name` (`canvasnet`), `version` |
| `input` | object: `path`, `format`, `natural` (object: `width`, `height`; the stored or intrinsic size) |
| `operations` | array of objects in order: `op` and the operation's parameters. `render` (`width`, `height`) for an SVG, `auto-orient` (`orientation`), `resize` (`width`, `height`), `flatten` (`background`) when transparency was flattened by the loader or for JPEG output |
| `outputs` | array with one object: `path`, `format`, `width`, `height`, `bytes` |
| `diagnostics` | array of objects: `source`, `severity`, `code`, `message` (empty for the formats of this release) |
| `echo` | array of strings (empty for the formats of this release) |
<!-- markdownlint-enable MD013 -->

#### Error Handling

Command-line mistakes (no input or more than one, no output, an unknown option, contradictory or malformed sizes,
a quality out of range, an unknown format) are `ToolException` with `usage-error`; a size larger than the maximum
is `size-limit`. Input failures come from `SourceLoader` (`input-not-found`, `input-unreadable`,
`unsupported-format`, `corrupt-input`, `unsupported-feature`, `size-limit`) and output failures from `ImageWriter`
(`same-input-output`, `output-exists`, `output-write-failed`); none of them leaves an output file. Null arguments
throw `ArgumentNullException`. The registry records the command name on every `ToolException`.

#### Dependencies

- **ArgumentReader** - parsing and the value grammars.
- **ResultWriter** - the JSON document.
- **Context** - the `Json` flag and `WriteLine`.
- **Program** - the tool version for the JSON result.
- **SourceLoader** and **ImageWriter** (Imaging) - everything that touches files and pixels.

#### Callers

- **CommandRegistry** - `CreateDefault` registers the command under `render` and `convert`.

# The canvasnet Command-Line Tool

<!-- cspell:ignore canvasnet Visio RRGGBBAA -->

The `DemaConsulting.CanvasNet.Tool` package is a .NET tool whose command, `canvasnet`, exposes the CanvasNet
libraries on the command line. It inspects, renders, converts, and transforms images, drawings, document pages, and
OpenSCAD models. It is built to be driven by programs as well as by people: a command's standard output holds only
its own result, every result can be written as one JSON document, a failure is reported with a stable error code, and
the exit code is always 0 or 1. This chapter describes the whole contract, so that a script or an AI agent can use the
tool without reading its source.

## Installing the Tool

The tool needs the .NET 8, 9, or 10 runtime (the `dotnet tool` commands need the SDK). It bundles every CanvasNet
library it uses, so no other CanvasNet package has to be installed.

```bash
# Install for the current user (the canvasnet command is then on the PATH)
dotnet tool install --global DemaConsulting.CanvasNet.Tool

# Or install in a folder, or in the tool manifest of a repository
dotnet tool install DemaConsulting.CanvasNet.Tool --tool-path ./tools
dotnet new tool-manifest
dotnet tool install DemaConsulting.CanvasNet.Tool

# Check the installation
canvasnet --version
canvasnet --help
canvasnet --validate
```

Use `dotnet tool update --global DemaConsulting.CanvasNet.Tool` to update the tool and
`dotnet tool uninstall --global DemaConsulting.CanvasNet.Tool` to remove it. In a repository that uses a tool
manifest, run it as `dotnet tool run canvasnet ...`.

## Command Line Conventions

A command line has the form `canvasnet [global options] <command> [arguments and options]`. The first word that is
not an option (or the value of an option) is the command name.

- **Option forms.** A value may follow its option after a space (`--width 800`) or an equals sign (`--width=800`).
  A short alias is written the same way (`-o out.png`). Options may be placed before or after the positional
  arguments, and an option marked repeatable may be given more than once.
- **Global options** are accepted in any position, before or after the command.
- **Mistakes.** An unknown option or command is an error that names the closest valid one (`Did you mean '--fit'?`).
- **Help.** `canvasnet --help` lists the commands, `canvasnet help <command>` and `canvasnet <command> --help` show
  the usage, examples, and every option of a command.
- **Files.** Paths are relative to the current directory. Wildcards are not expanded by the tool.

### Global Options

| Option | Meaning |
| --- | --- |
| `-v`, `--version` | Print the version (only the version) and exit |
| `-?`, `-h`, `--help` | Print the help of the tool or of the named command |
| `--json` | Write the result of the command as one JSON document on standard output |
| `--silent` | Suppress all console output, errors included (files are still written; `--log` still records) |
| `--log <file>` | Also write everything that is printed, errors included, to this file (replaced if it exists) |
| `--validate` | Run the self-validation (see _Self-Validation_) |
| `--results <file>` | With `--validate`: write the results as a `.trx` (TRX) or `.xml` (JUnit) file; alias `--result` |
| `--depth <1-6>` | With `--validate`: the markdown heading depth of the report (default 1) |

## Output, Errors, and Exit Codes

- **Standard output** carries only the result: the lines a command prints, or, with `--json`, one JSON document on one
  line. The banner (`CanvasNet Tool version ...`) is printed only for `--help`, `--validate`, and an empty command line,
  so a command's output can be piped and parsed.
- **Standard error** carries diagnostics: `Error: <code>: <message>` and an optional `Hint: <advice>` line for an
  expected failure. There is never a stack trace for an expected failure. A mistake in the global options that is
  found before the command is known (for example `canvasnet --nope`) is reported without a code, as
  `Error: <message>`; its `--json` failure document uses the code `usage-error`. With `--silent` the report is not
  printed (the exit code 1 still tells that the command failed), and with `--log` it is also written to the log
  file. The exceptions are a failure of the global options themselves, such as a malformed option or a log file that
  cannot be opened: no log or silent setting is in force yet, so it is always printed.
- **Exit codes** are the same for every command: **0** for success and **1** for any failure. The finer distinction
  is the stable error code, which is available only in the `Error:` line on standard error and in the `--json`
  failure document (see _Error Codes_); there is no other exit code to interpret.
- **Unexpected exceptions** are bugs, not user errors: a failure the tool did not anticipate (for example a defect in
  a library) is reported as `Unexpected error: <message>` on standard error and then re-thrown. The process then ends
  with the .NET unhandled-exception output (and an event log entry on Windows) and the runtime's own non-zero exit
  code. No JSON document is written for it. Please report it as a bug at
  <https://github.com/demaconsulting/CanvasNet/issues> with the command line and, if you can share it, the input file.

An example of an expected failure, in text and with `--json`:

```text
$ canvasnet render missing.png -o out.png
Error: input-not-found: 'missing.png' does not exist.
Hint: Check the path.
$ echo $?
1
```

With `--json` (and without `--silent`) the same failure is also written to standard output, so that a program can
parse standard output alone in both cases; the text report on standard error is unchanged:

<!-- markdownlint-disable MD013 -->

```json
{"schemaVersion":1,"ok":false,"command":"render","error":{"code":"input-not-found","message":"'missing.png' does not exist.","hint":"Check the path."}}
```

<!-- markdownlint-enable MD013 -->

## Commands

| Command | Purpose |
| --- | --- |
| `formats` | List the formats the tool reads and writes, and its limits |
| `info` | Show the format, size, and properties of a file without rendering it |
| `render` (alias `convert`) | Render or convert a file to PNG, JPEG, BMP, or TIFF, with optional transformations |
| `crop` | Cut a rectangle out of an image |
| `resize` | Resample an image to an exact size |
| `rotate` | Rotate an image by 90, 180, or 270 degrees |
| `flip` | Mirror an image |
| `pad` | Add a border around an image |

`crop`, `resize`, `rotate`, `flip`, and `pad` are `render` with one transformation placed first, so each of them
takes every option of `render` and works on every input that `render` reads (an SVG, a PDF page, an OpenSCAD model, and
so on, not only rasters).

### Formats Read and Written

| Input | Extensions | Notes |
| --- | --- | --- |
| PNG | `.png` | Non-interlaced (Adam7 is refused as `unsupported-feature`) |
| JPEG | `.jpg`, `.jpeg` | Baseline and progressive, gray and YCbCr; CMYK and arithmetic-coded files are refused |
| BMP | `.bmp` | Uncompressed 24-bit and 32-bit |
| TIFF | `.tif`, `.tiff` | 8-bit RGB, RGBA, and gray; tiled files, palettes, and CMYK are refused |
| GIF | `.gif` | The first frame only (`info` reports the frame count) |
| JPEG 2000 | `.jp2`, `.j2k`, `.j2c` | Decoding only |
| SVG | `.svg` | A common subset of SVG; the size is the intrinsic size of the drawing |
| PDF | `.pdf` | Multi-page; text uses the embedded font or a substituted font |
| PowerPoint | `.pptx` | Multi-page (slides) |
| Visio | `.vsdx` | Multi-page (drawing pages) |
| OpenSCAD | `.scad` | A model, drawn from one viewpoint |

| Output | Extensions | Notes |
| --- | --- | --- |
| PNG | `.png` | 8-bit RGBA |
| JPEG | `.jpg`, `.jpeg` | Baseline, no alpha (flattened), quality 1-100 (default 90) |
| BMP | `.bmp` | 32-bit with alpha |
| TIFF | `.tif`, `.tiff` | Uncompressed, with alpha |

The format of an input is recognized from its content (signature bytes, or the document structure inside a ZIP
package), and the extension is used only when the content is not conclusive. The output format is selected by the
output extension, or by `--format png|jpeg|jpg|bmp|tiff|tif`, which overrides the extension. An output with neither a
known extension nor `--format` is a `usage-error`. The largest image side the tool reads or writes is **8192
pixels**; a larger size is refused with `size-limit` before any pixels are allocated.

### formats

```text
canvasnet formats [--json]
```

Lists the input formats (with their kind, extensions, whether they have several pages, and whether they are
scalable), the output formats, the limits, and the versions of the tool and of the library. Use it to discover what
the installed version supports.

```text
$ canvasnet formats
Input formats:
  Format    Kind      Extensions      Pages     Scalable
  png       raster    .png            single    no
  ...
  scad      model     .scad           single    yes

Output formats:
  Format  Extensions  Alpha  Quality
  png     .png        yes    no
  jpeg    .jpg .jpeg  no     yes
  ...

Limits:
  Maximum image dimension: 8192 pixels
  JPEG quality: 1-100 (default 90)

Versions:
  canvasnet tool: <version>
  CanvasNet library: <version>
```

### info

```text
canvasnet info <input> [--page N | --all-pages] [--json]
```

Reads only the header of a file (a model is parsed but not evaluated) and reports its properties without rendering
it, which is cheap even for a large file.

- A **raster image or SVG** reports its format, size in pixels (the intrinsic size for an SVG), number of channels,
  whether it has transparency, whether the tool can decode it, the frame count of a GIF, and the EXIF orientation of a
  JPEG.
- A **PDF, PPTX, or VSDX document** reports its page count and the size of a page: in pixels at 96 dpi and in points,
  plus the rotation of a PDF page and the name of a VSDX page. `--page N` selects the page (default 1, counted from 1)
  and `--all-pages` lists every page.
- An **OpenSCAD model** reports the modules and functions it defines and its `include` and `use` directives.

```text
$ canvasnet info shapes.svg
shapes.svg: svg, 100x80 px (intrinsic size), 4 channels with alpha, 303 bytes
$ canvasnet info report.pdf --all-pages
report.pdf: pdf, 2 pages, 675 bytes
  page 1: 96x64 px at 96 dpi (72x48 pt), rotation 0
  page 2: 96x64 px at 96 dpi (72x48 pt), rotation 0
$ canvasnet info part.scad
part.scad: scad, 1 module, 1 function, 0 include/use directives, 376 bytes
  module block
  function half
```

### render and convert

```text
canvasnet render <input> -o <output> [options]
canvasnet convert <input> -o <output> [options]
```

`render` loads one input, optionally sizes it, applies the transformations in the order written, and writes one
output file (or one per page). `convert` is another name for the same command. The processing order is always:

1. **Load and size the source**: decode or draw it at its natural size, at the size set by `--dpi`, `--scale`,
   `--width`, `--height`, or `--size`, turn a JPEG upright by its EXIF orientation, and flatten a drawing over the
   background.
2. **Apply the pipeline operations** `--crop`, `--resize`, `--fit`, `--rotate`, `--flip`, and `--pad`, in the order
   they are written on the command line. The same operation may be repeated.
3. **Write** the output atomically (through a temporary file in the same folder).

The options of `render` are listed in the following tables.

| Source option | Meaning |
| --- | --- |
| `--page <N>` | The page or slide of a document to draw, counted from 1 (default 1) |
| `--all-pages` | Draw every page or slide (see _Several Pages_) |
| `--dpi <D>` | Resolution of a PDF, PPTX, or VSDX page (default 96, above 0 and at most 2400; whole numbers for VSDX) |
| `--scale <factor>` | Multiply the natural size by a factor (0.001 to 8192), for example `2` or `0.5` |
| `--width <px>`, `--height <px>` | Target width or height; the other side keeps the aspect ratio unless both given |
| `--size <WxH>` | Exact target size, for example `800x600` (shorthand for both `--width` and `--height`) |
| `--background <color>` | The color that transparency is flattened over (see _Colors_ and _Defaults_) |
| `--no-auto-orient` | Do not turn a JPEG upright by its EXIF orientation |
| `--strict` | Fail with `unsupported-feature` instead of skipping a picture or shape that cannot be drawn |

`--scale`, `--width`, `--height`, and `--size` conflict with one another where they overlap (`--scale` with any other,
`--size` with `--width` or `--height`); a conflict is a `usage-error`. For a vector source (SVG, document page,
model) the size is applied when drawing, so the drawing is sharp, never enlarged afterwards. For a raster source it
is a resampling, using `--filter`. When `--width` and `--height` are both given for an SVG, a document page, or a
model, the drawing is fitted inside that box, centered, and never stretched; the rest of the box is filled with the
background.

| Pipeline option | Meaning |
| --- | --- |
| `--crop <geometry>` | Cut out a rectangle (see _Geometry_); repeatable |
| `--resize <WxH>` | Resample to exactly this size; repeatable |
| `--fit <WxH>` | Shrink to fit inside the box, keeping the aspect ratio; an image that already fits is unchanged |
| `--allow-upscale` | Let `--fit` also enlarge an image that is smaller than the box |
| `--rotate <degrees>` | Rotate clockwise by 90, 180, or 270 degrees (-90 is 270) |
| `--flip <h\|v\|both>` | Mirror left to right (`h`), top to bottom (`v`), or both |
| `--pad <N\|l,t,r,b>` | Add a border of N pixels on every side, or `l,t,r,b` pixels on the left, top, right, bottom |
| `--pad-color <color>` | The color of the border added by `--pad` (see _Defaults_) |
| `--filter <name>` | Resampling filter: `auto` (default), `nearest`, `bilinear`, `bicubic`, `lanczos3`, or `area` |

`--filter`, `--pad-color`, and `--allow-upscale` are settings of the whole command (the last one wins), not steps.
`--allow-upscale` without `--fit` and `--pad-color` without `--pad` are a `usage-error`, because they would do nothing.
A result larger than 8192 pixels on a side (for example by padding) is refused with `size-limit`.

| Output option | Meaning |
| --- | --- |
| `-o`, `--output <file>` | The output file (required); its extension selects the format |
| `--format <format>` | Output format `png`, `jpeg` (or `jpg`), `bmp`, `tiff` (or `tif`); overrides the extension |
| `--quality <1-100>` | JPEG quality (default 90) |
| `--no-clobber` | Fail with `output-exists` instead of replacing an existing output file |

An existing output file is replaced unless `--no-clobber` is given. The output may never be the input file
(`same-input-output`). The folder of the output is created when it does not exist.

On success `render` prints one line per output, for example
`wrote shapes.png (100x80 png, 309 bytes) from shapes.svg`, or the JSON document described in _JSON Output_.

```bash
# Rasterize a drawing (an SVG is drawn at its intrinsic size by default)
canvasnet render drawing.svg -o drawing.png
canvasnet render drawing.svg --width 1200 -o drawing.png

# Convert between raster formats (the JPEG is flattened over white)
canvasnet convert scan.tif -o scan.png --no-clobber
canvasnet render shot.png -o shot.jpg --quality 85

# Crop a region by percent, then shrink the result to at most 800x600
canvasnet render photo.jpg --crop 10%,10%,80%,80% --fit 800x600 -o small.png

# A transparent icon: shrink into 256x256, then add an 8 pixel white border
canvasnet render logo.png --fit 256x256 --pad 8 --pad-color white -o icon.png
```

#### Several Pages

A PDF, PPTX, or VSDX document draws one page at a time: the page selected with `--page` (default 1). `--all-pages`
draws every page; the output must then either contain `{page}`, which is replaced by the page number (1, 2, ...,
without zero padding), or be a directory (an existing folder, or a path that ends with `/`), in which case the files
are named `<input name>-<page>.<extension>`. Any other output name is a `usage-error`, because every page would be
written to the same file. `{page}` is also replaced when a single `--page` is given. All the outputs are checked
before the first page is drawn, so a mistake in one of them is reported before any file is written. The pages are
drawn and encoded to temporary files first, and no output is touched until every page has been drawn and encoded: if a
page fails to draw or encode (for example an unsupported feature with `--strict`), no new output is written and
existing outputs are left untouched. The outputs are then moved into place one at a time, which is best-effort: if a
move itself fails (for example an output is no longer writable), the outputs already moved stay in place,
each one complete, and the error is reported. The JSON result
lists one entry per page in `outputs`. An input that is not a document accepts `--all-pages` and writes its one output
as given.

```bash
canvasnet render report.pdf --page 2 --dpi 150 -o page2.png
canvasnet render slides.pptx --all-pages -o 'out/slide-{page}.png'
canvasnet render slides.pptx --all-pages -o out/
```

The quotes around `{page}` keep a shell from expanding the braces. The size of a document page is its size in points
times the resolution divided by 72 (a 72 by 48 point PDF page is 96 by 64 pixels at 96 dpi and 192 by 128 at 192 dpi).

#### OpenSCAD Models

An OpenSCAD model (`.scad`) is evaluated and drawn from one viewpoint as a still image, at **800 by 800 pixels**
unless `--scale`, `--width`, `--height`, or `--size` is given (the picture is never stretched: the camera fits the
aspect ratio). Only a single view is drawn per command; run the command once per view to get several.

| Model option | Meaning |
| --- | --- |
| `--view <name>` | The named viewpoint: `iso` (default), `front`, `back`, `left`, `right`, `top`, or `bottom` |
| `--azimuth <degrees>` | Replace the azimuth of the view: degrees about the Z axis, 0 from the front |
| `--elevation <degrees>` | Replace the elevation of the view: degrees above the XY plane, -90 to 90 |
| `--projection <type>` | `ortho` (default; `orthographic` is accepted) or `perspective` |
| `-D <name=value>`, `--define` | Set a variable of the model, as `-D width=20`; repeatable (see below) |
| `--time <T>` | The value of `$t`, the animation time (default 0) |
| `--default-color <color>` | The color of surfaces the model does not color (default gray) |
| `--max-triangles <N>` | The most triangles the model may produce (default 5,000,000) |

The named views are orthographic and look at the model from the stated side with Z pointing up:

| View | Azimuth | Elevation | Looks at the model from |
| --- | --- | --- | --- |
| `iso` | 45 | 35.264 | A front corner, above (isometric) |
| `front` | 0 | 0 | The front |
| `back` | 180 | 0 | The back |
| `right` | 90 | 0 | The right |
| `left` | 270 | 0 | The left |
| `top` | 0 | 90 | Above |
| `bottom` | 0 | -90 | Below |

`--azimuth` and `--elevation` replace the angles of the selected view (or of `iso`), so `--azimuth 30 --elevation 20`
gives a free viewpoint. Model options given for an input that is not a model are a `usage-error`.

The value of `-D name=value` is a **number** (`-D width=20`, `-D ratio=0.5`), a **boolean** (`-D debug=true`), or
**text** (`-D label=hello`; the text may be put in quotes). Vectors and other expressions are not accepted. If a name
is given twice, the later definition wins. A name that is not a valid variable name is a `usage-error`.

The `include <file>` and `use <file>` directives of a model are read relative to the file that names them, and only
inside the folder of the model and below it; symbolic links are not followed. An include that names a missing file, a
file outside the model's folder, or a circular include is an error (`scad-error`) rather than being skipped, because
a picture drawn without a file it asked for would be silently wrong. A syntax error is also a `scad-error`, with the
file, line, and column of the mistake. A model that would produce more triangles than `--max-triangles` fails with
`budget-exceeded`.

A construct the model uses but the renderer does not support (for example `text`, `import`, or `surface`) is left
out and reported in the `diagnostics` of the JSON result. With `--strict` it is a failure instead
(`unsupported-feature`). The `echo` output of a model is returned in the `echo` list of the JSON result.

```bash
canvasnet render part.scad --view top -D width=30 -o part-top.png --json
canvasnet render part.scad --azimuth 30 --elevation 20 --projection perspective -o part.png
canvasnet render part.scad --view front --size 400x400 --background transparent -o front.png
```

#### Diagnostics and --strict

PPTX and VSDX documents and OpenSCAD models report every picture, shape, or feature the library recognizes but cannot
draw. By default the page is drawn without it and the report is returned in `diagnostics` in the JSON result.
`--strict` turns the report into a failure (`unsupported-feature`) and writes no output. SVG and PDF have no such
report: whatever the library cannot draw is simply not drawn, and `--strict` has no effect on them.

A PPTX shape that the library does not implement at all (for example an unknown preset shape) is always a failure
(`unsupported-feature`), with or without `--strict`; `--strict` additionally upgrades the skipped EMF and WMF
pictures of a PPTX and the approximated constructs of a VSDX.

### crop, resize, rotate, flip, and pad

```text
canvasnet crop   <input> <geometry>      -o <output> [options]
canvasnet resize <input> <WxH>           -o <output> [options]
canvasnet rotate <input> <90|180|270>    -o <output> [options]
canvasnet flip   <input> <h|v|both>      -o <output> [options]
canvasnet pad    <input> <N|l,t,r,b>     -o <output> [options]
```

The input comes first, then the operand of the command. Each command is `render` with its operation placed first,
followed by any operations written as options, so `canvasnet crop in.png 10,10,100,100 -o out.png` and
`canvasnet render in.png --crop 10,10,100,100 -o out.png` write identical files. The JSON `command` of the result is
the name of the command that was used.

```bash
canvasnet crop photo.png 10,10,200,100 -o part.png
canvasnet crop photo.png 200x100+10+10 -o part.png
canvasnet crop slide.pdf 0,0,50%,100% --page 2 --dpi 150 -o left-half.png
canvasnet resize photo.png 800x600 --filter lanczos3 -o photo-small.png
canvasnet rotate scan.png 90 -o scan-upright.png
canvasnet flip logo.png h -o logo-mirrored.png
canvasnet pad icon.png 16 --pad-color '#336699' -o icon-framed.png
canvasnet pad icon.png 0,0,32,32 -o icon-padded.png
```

The semantics of the operations are:

- **crop** keeps the rectangle. The edges (x, y, x+w, y+h) are rounded to whole pixels (halves away from zero). A part
  of the rectangle outside the image is cut off, which is reported by `"clamped":true` in the JSON result; a rectangle
  that starts at or beyond the right or bottom edge, or is smaller than one pixel, is a `usage-error`. A percentage
  is a percentage of the image at that point of the pipeline.
- **resize** produces exactly the given size (the aspect ratio is not kept). Use `--fit` on `render` to keep it.
- **rotate** turns the image clockwise by a right angle. Any other angle (0, 360, 45, ...) is a `usage-error`:
  rotation by other angles is not supported.
- **flip** mirrors the image: `h` left to right, `v` top to bottom, `both` is a rotation by 180 degrees.
- **pad** adds a border of the given widths, in pixels. The color is `--pad-color`, which defaults as described in
  _Defaults_.

### Values: Sizes, Geometry, and Colors

<!-- markdownlint-disable MD013 -->

| Value | Grammar | Examples |
| --- | --- | --- |
| Size | `WxH`, both whole pixels, 1 to 8192 | `800x600` |
| Geometry | `x,y,w,h` or `WxH+X+Y`; each value in pixels (`10`, `10px`) or percent (`10%`); `WxH` alone is at 0,0 | `10,10,200,100`, `200x100+10+10`, `10%,10%,80%,80%` |
| Color | `#RGB`, `#RGBA`, `#RRGGBB`, `#RRGGBBAA` (alpha **last**), the same without `#`, a basic CSS name, or `transparent` | `#336699`, `#33669980`, `white`, `transparent` |
| Border | One number for every side, or `l,t,r,b` | `8`, `0,0,32,32` |
| Degrees | A number; for `rotate` a right angle | `90`, `-90` |

<!-- markdownlint-enable MD013 -->

The basic color names are `black`, `silver`, `gray` (`grey`), `white`, `maroon`, `red`, `purple`, `fuchsia`
(`magenta`), `green`, `lime`, `olive`, `yellow`, `navy`, `blue`, `teal`, `aqua` (`cyan`), and `orange`. In a shell,
quote
a color that starts with `#` (`'#336699'`) or use the form without `#`, because many shells treat `#` as a comment.
A value that does not match its grammar is a `usage-error` that quotes the value and shows an example.

## JSON Output

With `--json` a command writes one JSON document on one line to standard output (and nothing else there). The
document is deterministic: fixed key order, invariant culture numbers, ASCII only (other characters are written as
`\uXXXX`), and a `schemaVersion` that is `1` and changes only when a document changes incompatibly. Every document
starts with the same envelope.

| Key | Meaning |
| --- | --- |
| `schemaVersion` | The version of the document layout (1) |
| `ok` | `true` for a result, `false` for a failure |
| `command` | The command that ran (`null` in a failure when no command was selected) |

### Success Documents

`formats --json` adds `tool` and `library` (`{"name","version"}`), `inputs` (one `{"format","kind","extensions",
"multiPage","vector"}` per input format), `outputs` (`{"format","extensions","alpha","lossy"}`), `quality`
(`{"min","max","default"}`), and `maxDimension`.

<!-- markdownlint-disable MD013 -->

`info --json` adds `input` (`{"path","format","bytes"}`) and one of these objects:

```json
{"schemaVersion":1,"ok":true,"command":"info","input":{"path":"shapes.svg","format":"svg","bytes":303},"image":{"width":100,"height":80,"channels":4,"hasAlpha":true,"canDecode":true,"frameCount":1}}
```

```json
{"schemaVersion":1,"ok":true,"command":"info","input":{"path":"report.pdf","format":"pdf","bytes":675},"document":{"pageCount":2,"pages":[{"page":1,"width":96,"height":64,"widthPt":72,"heightPt":48,"rotation":0},{"page":2,"width":96,"height":64,"widthPt":72,"heightPt":48,"rotation":0}]}}
```

```json
{"schemaVersion":1,"ok":true,"command":"info","input":{"path":"inc.scad","format":"scad","bytes":63},"model":{"modules":[],"functions":[],"directives":[{"kind":"include","path":"lib.scad","line":1}]}}
```

- `image` (raster, SVG): `width`, `height` in pixels, `channels`, `hasAlpha`, `canDecode`, `frameCount`, and
  `exifOrientation` (a JPEG that has one, 1-8).
- `document` (PDF, PPTX, VSDX): `pageCount` and `pages`, each with `page` (from 1), `width` and `height` in pixels at
  96 dpi, `widthPt` and `heightPt` in points, and, when there is one, `rotation` (PDF) and `name` (VSDX). Without
  `--page` or `--all-pages` only the first page is listed.
- `model` (SCAD): `modules` and `functions` (names) and `directives` (`kind` `include` or `use`, `path`, `line`).

`render`, `convert`, and the transformation commands add:

```json
{"schemaVersion":1,"ok":true,"command":"crop","tool":{"name":"canvasnet","version":"<version>"},"input":{"path":"shapes.png","format":"png","natural":{"width":100,"height":80}},"operations":[{"op":"crop","x":0,"y":0,"width":50,"height":80,"clamped":false}],"outputs":[{"path":"left.png","format":"png","width":50,"height":80,"bytes":205}],"diagnostics":[],"echo":[]}
```

- `tool`: the name and version of the tool.
- `input`: `path`, `format`, and `natural` (the size of what was loaded: the size of the file, or of the first page
  drawn). For a document, `pageCount`, and `page` (the page drawn; omitted with `--all-pages`).
- `operations`: every step that was applied, in order (see the following table).
- `outputs`: one entry per file written: `path` (as given), `format`, `width`, `height`, `bytes`, and `page` for a
  document.
- `diagnostics`: the features that were skipped, each `{"source","severity","code","message"}` and `page` for a
  document (`source` is `scad`, `pptx`, or `vsdx`; `code` is the library's stable feature token, such as
  `scad-module-text`).
- `echo`: the text a SCAD model printed with `echo`.

| `op` | Fields besides `op` |
| --- | --- |
| `auto-orient` | `orientation` (the EXIF orientation, 2-8, that was applied) |
| `render` | `width`, `height`; a document adds `page`, `dpi`, `background`; a model adds `view`, `azimuth`, `elevation`, `projection`, `width`, `height`, `background`, and when given `defines`, `time`, `maxTriangles`, `defaultColor` |
| `resize` | `width`, `height`, and `filter` when one was chosen (a raster sized by `--scale`, `--width`, `--height`, or `--size`, or `--resize`) |
| `fit` | The loader's fit of a drawing into a `--width` by `--height` box has `width`, `height` (the box); the `--fit` operation has `width`, `height` (the result), `boxWidth`, `boxHeight`, `filter`, `allowUpscale`, `scaled` |
| `flatten` | `background` (transparency flattened over a color: a drawing over its background, or the JPEG encoder) |
| `crop` | `x`, `y`, `width`, `height`, `clamped` |
| `rotate` | `degrees`, `width`, `height` (the size after rotating) |
| `flip` | `axis` (`horizontal`, `vertical`, or `both`), `width`, `height` |
| `pad` | `left`, `top`, `right`, `bottom`, `color`, `width`, `height` |

The order of `operations` is: the steps of loading and sizing, the pipeline steps in the order written, and finally
the `flatten` of a JPEG output. With `--all-pages` every operation also carries its `page`. The set of operations may
grow in later versions of the tool; a program should ignore an `op` or a key it does not know.

### Failure Documents

```json
{"schemaVersion":1,"ok":false,"command":"render","error":{"code":"page-out-of-range","message":"Page 9 does not exist: 'report.pdf' has 2 pages.","hint":"Pages are numbered from 1."}}
```

`error.code` is one of the codes in the next section, `error.message` describes the problem, and `error.hint`
(omitted when there is none) says what to do about it. The document has no exit-code field, because the exit code of
every failure is 1. A failure document is written only when `--json` is on the command line and `--silent` is not.

## Error Codes

The codes are stable: a code is never renamed once released, so a program may branch on them. The text of a message
and hint may be improved at any time.

| Code | Cause | What to do |
| --- | --- | --- |
| `usage-error` | The command line is wrong: unknown command or option, missing or malformed value, conflicting options, a mistake in the page, geometry, color, angle, or view, or a missing output | Read the hint and `canvasnet <command> --help` |
| `operation-failed` | A command could not complete for a reason that is not one of the others | Read the message |
| `input-not-found` | The input file or folder does not exist | Check the path |
| `input-unreadable` | The input exists but cannot be read (permissions, a folder, a locked file) | Check the permissions |
| `unsupported-format` | The input is not a format the tool reads (for example a DOCX or an unknown extension) | See `canvasnet formats` |
| `corrupt-input` | The content is damaged or is not what its extension says (a truncated or garbage file) | Repair or replace the file |
| `unsupported-feature` | The file is valid but uses a feature the library cannot draw or decode (an interlaced PNG, a PPTX shape the library lacks, or any skipped feature with `--strict`) | Remove the feature, or drop `--strict` if it is only a skipped picture |
| `page-out-of-range` | `--page` is beyond the last page of the document | Pages are counted from 1; use `info` for the count |
| `size-limit` | A requested, declared, or resulting size exceeds 8192 pixels on a side | Use a smaller size, dpi, or border |
| `same-input-output` | The output file is the input file (also reached through a symbolic link) | Name a different output |
| `output-exists` | The output exists and `--no-clobber` was given | Remove `--no-clobber` or choose another output |
| `output-write-failed` | The output could not be written (an invalid path, a file where a folder should be, permissions, a full disk) | Check the output path |
| `scad-error` | A model has a syntax error, an evaluation error, or an include that cannot be read | The message has the file, line, and column |
| `budget-exceeded` | A model exceeds a rendering limit, such as `--max-triangles` | Raise the limit or simplify the model |

## Defaults

| Topic | Default | How to change it |
| --- | --- | --- |
| Existing output file | Replaced | `--no-clobber` makes it an error |
| Output the same as the input | Refused (`same-input-output`) | Choose another output |
| Output format | From the output extension | `--format` |
| JPEG quality | 90 | `--quality` |
| Transparency in a vector source (SVG, document page, model) | Flattened over white | `--background <color>`; `--background transparent` keeps it |
| Transparency in a raster source | Kept (PNG, BMP, TIFF); a JPEG output flattens over `--background` or white | `--background <color>` flattens a raster too |
| JPEG orientation | Turned upright by its EXIF orientation; `info` reports the raw value | `--no-auto-orient` |
| Resolution of a document page | 96 dpi | `--dpi` (at most 2400) |
| Size of a drawing | Its natural size | `--scale`, `--width`, `--height`, `--size`, `--dpi` |
| Size of an OpenSCAD model | 800 by 800 pixels | The size options |
| SCAD view and projection | `iso`, orthographic | `--view`, `--azimuth`, `--elevation`, `--projection perspective` |
| SCAD default surface color | Gray | `--default-color` |
| Maximum triangles of a model | 5,000,000 | `--max-triangles` |
| Resampling filter | `auto` | `--filter` |
| `--fit` | Shrinks only | `--allow-upscale` |
| Pad color | `--pad-color`, else `--background`, else white for a drawing or document page and transparent for a raster | `--pad-color` |
| Page of a document | 1 | `--page`, `--all-pages` |
| Largest side of an image | 8192 pixels | Not adjustable |
| Banner | Only for `--help`, `--validate`, and an empty command line | Not adjustable |

<!-- markdownlint-enable MD013 -->

The size and EXIF steps always run before the first pipeline operation: for example `canvasnet render photo.jpg
--rotate 90 -o out.png` rotates the already upright photo, and `render drawing.svg --width 800 --crop 0,0,50%,100%`
crops the 800-pixel drawing, not the natural-size one.

## Using the Tool from an AI Agent

The tool is meant to be used by an agent that can look at images: it turns anything the agent cannot see directly (a
PDF page, a PowerPoint slide, a Visio drawing, an OpenSCAD model) into a PNG the agent can inspect, and writes
the agent's own drawings (an SVG or a SCAD model) as pictures it can compare with a reference. A few conventions make
this reliable:

- Always pass `--json` and parse the one line on standard output. Check `ok`, and on a failure branch on
  `error.code`. Read `diagnostics` even on success: they list what was left out of the picture.
- Ask before you render: `canvasnet info <file> --json` gives the size, the page count, or the modules of a model
  cheaply, so the agent can plan the sizes and the pages.
- Keep intermediate pictures at moderate sizes (an image the agent looks at rarely needs more than 1600 pixels on a
  side), and use `--crop` with `--resize` to zoom into a detail.
- Treat exit code 1 and a failure document as expected feedback to act on. An `Unexpected error` is a defect to report.
- The tool replaces an existing output file by default; use new names (or `--no-clobber`) to keep earlier attempts.

### Recipe: Redraw a PDF Page as an SVG

1. Learn the pages and their sizes:

   ```bash
   canvasnet info report.pdf --all-pages --json
   ```

2. Render the page that is to be redrawn at a resolution good enough to read, and look at the PNG:

   ```bash
   canvasnet render report.pdf --page 2 --dpi 150 -o ref-page2.png --json
   ```

3. Write `redraw.svg` with the same aspect ratio as the page (a page of `widthPt` by `heightPt` points can be drawn
   with `viewBox="0 0 widthPt heightPt"`, so that SVG coordinates are points).
4. Render the SVG at the same pixel size as the reference and look at it. The `natural` size of the reference result
   tells the target size:

   ```bash
   canvasnet render redraw.svg --size 1250x1750 -o redraw-page2.png --json
   ```

5. Compare the two pictures. To look closely at a detail, cut the same region out of both, enlarged by the same
   factor, and compare the pair:

   ```bash
   canvasnet render ref-page2.png --crop 200x120+100+80 --resize 1000x600 -o ref-detail.png
   canvasnet render redraw.svg --size 1250x1750 --crop 200x120+100+80 --resize 1000x600 -o redraw-detail.png
   ```

6. Fix what differs in the SVG and repeat from step 4. A text-heavy SVG depends on the fonts of the machine, so
   compare layout rather than exact glyph shapes.

### Recipe: From a Napkin Sketch to an OpenSCAD Model

1. Inspect the photo; a phone JPEG is turned upright automatically by its EXIF orientation:

   ```bash
   canvasnet info napkin.jpg --json
   ```

2. Prepare a picture to look at: crop away the table, and shrink it so that it is cheap to view:

   ```bash
   canvasnet render napkin.jpg --crop 5%,10%,90%,80% --fit 1600x1600 -o napkin-view.png --json
   ```

3. Write `bracket.scad`, with the dimensions read from the sketch as variables (`width = 40;`, ...).
4. Render named views of the model and look at them next to the sketch:

   ```bash
   canvasnet render bracket.scad --view front -o bracket-front.png --json
   canvasnet render bracket.scad --view top -o bracket-top.png
   canvasnet render bracket.scad --view right -o bracket-right.png
   canvasnet render bracket.scad --view iso -o bracket-iso.png
   ```

5. Adjust the model, or try a dimension without editing it by overriding a variable:

   ```bash
   canvasnet render bracket.scad --view top -D width=55 -D hole=6 -o bracket-top-wide.png
   ```

6. Before finishing, render once with `--strict --json`: it fails if the model uses something the renderer cannot
   draw (so that the picture would be missing it), and `diagnostics` shows what.

## Limitations and Determinism

- **Determinism.** For the same input, command line, tool version, and machine, the output file is identical byte for
  byte, as is a JSON document. Different machines can differ where text is drawn from the host's fonts (text in SVG,
  PDF, PowerPoint, and Visio files that does not embed its font), so compare such pictures by eye rather than by
  checksum. Pictures made of shapes, rasters, and OpenSCAD models do not depend on fonts.
- **Quality of drawing.** The rendering libraries draw a defined subset of each format. Anything outside it is reported
  where the library can report it (PPTX, VSDX, and SCAD: `diagnostics`). PDF and SVG have no such report, and a
  construct they cannot draw is simply absent from the picture.
- **One frame.** A GIF is read as its first frame.
- **JPEG.** Only gray and YCbCr files can be decoded; CMYK and arithmetic-coded JPEGs are refused. Output JPEG has no
  alpha: transparency is flattened.
- **Models.** Only one view per command, and no animation. A model that needs `text`, `import`, or other unsupported
  constructs is drawn without them.
- **Rotation.** Only 90, 180, and 270 degrees.
- **Size.** 8192 pixels on a side at most; an image that large uses about 256 MB of memory per copy, so large pipelines
  need memory.
- **Concurrency.** Each command is independent, so several may run in parallel as long as they write different files.

## Self-Validation

`canvasnet --validate` runs a set of built-in tests that exercise the installed tool: each test generates its own
input in a temporary folder, runs a command, and checks the output. It proves that the tool and the libraries it
bundles work on this machine and runtime, and it needs no files.

```text
$ canvasnet --validate
CanvasNet Tool version <version>
Copyright (c) DEMA Consulting

# DEMA Consulting CanvasNet Tool

| Information         | Value                                              |
| :------------------ | :------------------------------------------------- |
| Tool Version        | <version>                                          |
...
✓ CanvasNetTool_VersionDisplay - Passed
✓ CanvasNetTool_HelpDisplay - Passed
✓ CanvasNetTool_Formats - Passed
✓ CanvasNetTool_RenderPng - Passed
✓ CanvasNetTool_ConvertFormats - Passed
✓ CanvasNetTool_Info - Passed
✓ CanvasNetTool_RenderSvg - Passed
✓ CanvasNetTool_PdfMinimal - Passed
✓ CanvasNetTool_RenderScad - Passed
✓ CanvasNetTool_CropResize - Passed
✓ CanvasNetTool_RotateFlipPad - Passed

Total Tests: 11
Passed: 11
Failed: 0
```

| Self-test | What it proves |
| --- | --- |
| `CanvasNetTool_VersionDisplay` | `--version` prints the version |
| `CanvasNetTool_HelpDisplay` | `--help` prints the usage |
| `CanvasNetTool_Formats` | `formats` lists the formats, as text and as JSON |
| `CanvasNetTool_RenderPng` | An image is rendered to PNG with the right size and pixels |
| `CanvasNetTool_ConvertFormats` | An image converts between PNG, JPEG, BMP, and TIFF |
| `CanvasNetTool_Info` | `info` reports the size and format of an image |
| `CanvasNetTool_RenderSvg` | An SVG drawing is rasterized |
| `CanvasNetTool_PdfMinimal` | A one-page PDF is read and a page is rendered |
| `CanvasNetTool_RenderScad` | An OpenSCAD model is drawn, and `-D` changes the picture |
| `CanvasNetTool_CropResize` | `crop` and `resize` produce the expected pixels |
| `CanvasNetTool_RotateFlipPad` | `rotate`, `flip`, and `pad` produce the expected pixels |

The exit code is 0 when every test passed and 1 when any failed. Add `--results <file>` to record the outcome as a
TRX file (`.trx`) or a JUnit XML file (`.xml`), for example for a quality system that collects test evidence:

```bash
canvasnet --validate --results validation.trx
canvasnet --validate --results validation.xml --silent --log validation.log
```

A results file with any other extension is an error and no file is written. The first two tests,
`CanvasNetTool_VersionDisplay` and `CanvasNetTool_HelpDisplay`, are the evidence that the tool works on each supported
operating system and .NET runtime, which the CanvasNet build collects by installing the packaged tool on Windows,
Linux, and macOS with .NET 8, 9, and 10 and running `canvasnet --validate`.

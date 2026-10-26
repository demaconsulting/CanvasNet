# The canvasnet Command-Line Tool

<!-- cspell:ignore canvasnet Visio RRGGBBAA psnr rmse ssim -->

The `DemaConsulting.CanvasNet.Tool` package is a .NET tool whose command, `canvasnet`, exposes the CanvasNet
libraries on the command line. It inspects, renders, converts, transforms, compares, and composes images, drawings,
document pages, and OpenSCAD models. It is built to be driven by programs as well as by people: a command's standard
output holds only
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
| `compare` | Measure how much two images differ, write pictures of the differences, and check thresholds |
| `montage` | Compose images and document pages into one contact sheet |

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

### compare

<!-- markdownlint-disable MD013 -->

```text
canvasnet compare <a> <b> [options]
```

`compare` loads two inputs, measures how much they differ, and reports the measures, a grid of per-cell errors, and
optionally pictures of the differences. Either input may be anything `render` reads: a raster, an SVG, a page of a
PDF, PPTX, or VSDX document, or an OpenSCAD model. `a` is the **reference** (by convention) and `b` the candidate;
the measures are symmetric, but `--resize-to a` and `--resize-to b` treat the two differently. Both inputs are
flattened over an opaque background (white by default) before they are compared, so the measures are defined on RGB
and the alpha channel is ignored.

```bash
# How close is the redraw to the reference? Draw the SVG at the size of the reference and show where they differ
canvasnet compare ref.png redraw.svg --resize-to a --grid 4x4 --side-by-side cmp.png --json

# A gate for a script or an agent: exit code 1 (threshold-failed) unless the redraw is at least 90% similar
canvasnet compare ref.png redraw.svg --resize-to a --threshold ssim=0.9 --threshold rmse=25 --diff diff.png

# Page 2 of one document against page 1 of another, at 150 dpi
canvasnet compare old.pdf new.pdf --a-page 2 --b-page 1 --dpi 150 --diff changes.png
```

#### Sizes: --resize-to and --fit

The two inputs must have the same size in pixels. When they do not, the command fails with `size-mismatch` (and the
hint `Use --resize-to a (or b, or WxH) to bring them to one size`), because silently resampling would hide a mistake
of aspect ratio. Choose the size with `--resize-to`:

| `--resize-to` | The size that is compared |
| --- | --- |
| `a` | The size of input `a` as it loads; `b` is loaded into that size |
| `b` | The size of input `b` as it loads; `a` is loaded into that size |
| `WxH` | Exactly this size (each side 1 to 8192, else `size-limit`); both inputs are loaded into it |

"Loaded into a size" depends on the kind of input. A **vector** input (an SVG, a page of a document, an OpenSCAD
model) is drawn directly at that size, so it is sharp and is never resampled: a drawing is fitted inside the box and
centered over the background without stretching (an SVG follows its own `preserveAspectRatio`, and a model fills the
box with its fit-to-model camera). A **raster** input is loaded at its natural size and then fitted by `--fit`:

| `--fit` | Raster input that is not the target size |
| --- | --- |
| `contain` (default) | Scaled, keeping its aspect ratio, so that it fits inside the size (enlarging or shrinking), and centered over the background |
| `stretch` | Resampled to exactly the size, whatever its aspect ratio |

Note that `--fit contain|stretch` of `compare` is not the `--fit WxH` of `render`. The resampling filter is
`--filter` (default `auto`). The JSON result reports what was done to each input in its `operations`.

#### Options

Options that are shared by both inputs, with the grammar of the same options of `render`:

| Option | Meaning |
| --- | --- |
| `--page <N>`, `--dpi <D>` | The page and the resolution of a document input (defaults 1 and 96) |
| `--view`, `--azimuth`, `--elevation`, `--projection`, `-D`/`--define`, `--time`, `--default-color`, `--max-triangles` | The view and variables of an OpenSCAD model input (see _OpenSCAD Models_); given when no input is a model they are a `usage-error` |
| `--background <color>` | The **opaque** color that transparency is flattened over (default white); `transparent` or a color with alpha below 255 is a `usage-error` |
| `--no-auto-orient`, `--strict`, `--filter` | As for `render` |
| `--quality <1-100>`, `--no-clobber` | For the pictures that are written (a JPEG picture is flattened over the background) |

A shared option that does not concern an input is skipped silently for that input (`--page` is ignored for a PNG).
Per-side options override the shared ones for one input, and are a `usage-error` when that input is of the wrong kind
(they state an intent):

| Option | Meaning |
| --- | --- |
| `--a-page`, `--b-page` | The page of input `a` or `b` (a document) |
| `--a-dpi`, `--b-dpi` | The resolution of input `a` or `b` (a document) |
| `--a-view`, `--b-view` | The named view of input `a` or `b` (a model) |
| `--a-define`, `--b-define` | A variable (`name=value`, repeatable) of input `a` or `b` (a model); a per-side definition replaces a shared one of the same name |

`--all-pages`, `--scale`, `--width`, `--height`, and `--size` are not options of `compare` (`--resize-to` sets the
size).

| Measure option | Meaning |
| --- | --- |
| `--tolerance <0-255>` | A pixel counts as differing when its largest channel difference is **above** this (default 8; 0 counts any change) |
| `--grid <CxR>` | The grid of cells for the per-cell errors: columns by rows, each 1 to 64 and not more than the pixels of the image (default `4x4`) |
| `--threshold <metric>=<value>` | A limit to check; repeatable (see _Thresholds_) |

| Picture option | Meaning |
| --- | --- |
| `--diff <file>` | Write the difference picture |
| `--diff-style <heat\|abs>` | `heat` (default): a color ramp of the largest channel difference; `abs`: the difference of each channel |
| `--gain <factor>` | Amplification of the differences in `--diff` and in the third panel of `--side-by-side` (0.1 to 255, default 4) |
| `--side-by-side <file>` | Write the two images and the `heat` picture of the differences side by side |
| `--overlay <file>` | Write a blend of the two images |
| `--overlay-weight <0-1>` | The weight of `b` in the blend (default 0.5) |

The format of each picture comes from its extension (PNG, JPEG, BMP, TIFF). A picture may not be one of the inputs
(`same-input-output`), two pictures may not be the same file (`usage-error`), an existing picture is replaced unless
`--no-clobber` is given, and every picture is created and encoded before any is moved into place: if any picture
cannot be created or encoded, none is written.

#### The Measures

Both images are `W`x`H` pixels, `N = W*H`. For every pixel and every channel `c` of R, G, and B, `d = |a_c - b_c|`
(whole numbers 0 to 255). All sums are exact integers; the only floating-point steps are single divisions, a square
root, and a logarithm at the end, so the same inputs give the same numbers on every run.

| JSON key | Definition |
| --- | --- |
| `mae` | Mean absolute error: `sum(d) / (3N)` (0 to 255) |
| `rmse` | Root mean square error: `sqrt( sum(d^2) / (3N) )` (0 to 255); `rmseNormalized = rmse / 255` |
| `psnrDb` | Peak signal-to-noise ratio: `20 * log10(255 / rmse)`; `null` when `rmse` is 0 (the text form prints `inf`) |
| `maxChannelDiff` | The largest `d` over all pixels and channels (0 to 255) |
| `diffPixels`, `diffPixelRatio` | The number of pixels whose largest channel difference is above `--tolerance`, and that number divided by `N` |
| `identical` | `maxChannelDiff == 0` (it does not depend on the tolerance) |
| `ssim` | The structural similarity, defined below (-1 to 1; 1 for identical images) |

**SSIM.** The luma of a pixel is `Y = (77*R + 150*G + 29*B + 128) >> 8` (integer arithmetic). The image is covered
by windows of `min(8, W)` by `min(8, H)` pixels whose origins are every 4 pixels in x and in y (`0, 4, 8, ...` while
the window fits), plus a final window flush with the right and the bottom edge when the stride does not reach it, so
that every pixel is in at least one window. For a window of `n` pixels with the exact sums `sA = sum(Ya)`,
`sB = sum(Yb)`, `sAA = sum(Ya^2)`, `sBB = sum(Yb^2)`, and `sAB = sum(Ya*Yb)`:

```text
SSIM_window = ( (2*sA*sB + C1*n^2) * (2*(n*sAB - sA*sB) + C2*n^2) )
            / ( (sA^2 + sB^2 + C1*n^2) * (n*(sAA + sBB) - sA^2 - sB^2 + C2*n^2) )

C1 = (0.01 * 255)^2 = 6.5025        C2 = (0.03 * 255)^2 = 58.5225
```

This is the usual `(2*ma*mb + C1)(2*cov + C2) / ((ma^2 + mb^2 + C1)(va + vb + C2))` with uniform windows and
population variance, multiplied through by `n^2`. `ssim` is the arithmetic mean of the window values in row-major
window order. It is a deliberately simple "SSIM-lite": it does not use the Gaussian window of the original paper, so
compare it with other tools' values only roughly, but use it to compare two runs of this tool.

**Grid.** With `--grid CxR` the cell boundaries are `x_i = floor(i*W/C)` and `y_j = floor(j*H/R)` (integer division),
so the cells partition the image exactly. Each cell reports `mae`, `rmse`, and `diffPixelRatio` with the formulas
above restricted to the cell, and its 0-based `column` and `row` and `x`, `y`, `width`, `height`. `worstCells` lists up
to the three cells with the largest `rmse` (only cells whose `rmse` is above 0; ties in row-major order).

**Pictures.** For `--diff`, `delta = max_c d` per pixel and `v = min(255, round(delta * gain))`. In the `heat` style
(integer arithmetic) `R = min(255, 3v)`, `G = min(255, max(0, 3v - 255))`, and `B = min(255, max(0, 3v - 510))`, a
ramp from black through red and yellow to white, so identical pixels are pure black. In the `abs` style each channel
is `min(255, round(d_c * gain))`. `--side-by-side` is `a | b | heat difference` from left to right, with 8 pixel
gaps of `#808080` and no labels; when `3W + 16` exceeds 8192 (or `H` does) the three panels are scaled down uniformly
(the `area` filter) so that the picture fits, and the reported picture size says so. `--overlay` is the blend
`out = (a*(255 - bw) + b*bw + 127) / 255` per channel in integer arithmetic, with `bw = round(weight*255)`.

#### Thresholds

`--threshold <metric>=<value>` checks one measure against a limit; give the option once per metric (a metric given twice
is a `usage-error`). All checks are evaluated with the unrounded values.

| Metric | Passes when | Allowed values |
| --- | --- | --- |
| `mae`, `rmse`, `max-diff` (`maxChannelDiff`), `cell-rmse` (the largest cell `rmse`) | measure `<=` value | 0 to 255 |
| `diff-ratio` (`diffPixelRatio`) | measure `<=` value | 0 to 1 |
| `psnr` | measure `>=` value (identical images, with an infinite PSNR, pass) | 0 to 1000 |
| `ssim` | measure `>=` value | -1 to 1 |

Whether or not a check fails, the command writes the whole result and every picture that was asked for. When a check
fails it then **fails with exit code 1** and the error code `threshold-failed`: standard error has
`Error: threshold-failed: rmse 23.91 is above the limit 10; ssim 0.8123 is below the limit 0.9` and a hint, and with
`--json` the failure document carries the measures too (see below), so that one parse gives the failure and the
numbers. There is no extra exit code: a script that needs to tell a failed gate from another failure checks the
`error.code`.

#### Result

The text result (invariant culture; MAE, RMSE, and PSNR with two decimals, SSIM with four, the ratio as a percentage
with two) is one block on standard output:

```text
compared ref.png with redraw.svg (900x600): MAE 11.20, RMSE 23.91, PSNR 20.55 dB, SSIM 0.8123, 7.04% of pixels differ (tolerance 8)
RMSE per cell (4x4):
   3.1   9.8  61.3   4.0
   ...
wrote cmp.png (2716x600 png, 88211 bytes) [side-by-side]
threshold rmse <= 10: failed (23.91)
```

Identical inputs print only `compared a with b (WxH): identical` (and, with thresholds, their lines); a document
input is named `report.pdf page 2`. One line `wrote ... [diff|side-by-side|overlay]` follows for each picture and
one `threshold ...` line for each threshold.

With `--json` the document is below (shown wrapped here; it is one line). `a` and `b` add `page` and `pageCount` for a
document; `resize` is present only when `--resize-to` was given; `psnrDb` is `null` for identical images; MAE, RMSE,
and PSNR are rounded to 4 decimals and the other fractions to 6.

```json
{"schemaVersion":1,"ok":true,"command":"compare","tool":{"name":"canvasnet","version":"<version>"},
 "a":{"path":"ref.png","format":"png","loadedWidth":900,"loadedHeight":600,"width":900,"height":600,"operations":[]},
 "b":{"path":"redraw.svg","format":"svg","loadedWidth":900,"loadedHeight":600,"width":900,"height":600,"operations":[{"op":"render","width":900,"height":600,"background":"#FFFFFF"}]},
 "size":{"width":900,"height":600},
 "resize":{"to":"a","fit":"contain"},
 "options":{"tolerance":8,"background":"#FFFFFF","gridColumns":4,"gridRows":4},
 "metrics":{"identical":false,"mae":11.2,"rmse":23.9,"rmseNormalized":0.0937,"psnrDb":20.6,"ssim":0.8123,"maxChannelDiff":255,"diffPixels":38016,"diffPixelRatio":0.0704},
 "grid":{"columns":4,"rows":4,"cells":[{"column":0,"row":0,"x":0,"y":0,"width":225,"height":150,"mae":2.1,"rmse":3.1,"diffPixelRatio":0.01}]},
 "worstCells":[{"column":2,"row":1,"rmse":61.3}],
 "thresholds":{"passed":true,"checks":[{"metric":"rmse","comparison":"<=","limit":30,"value":23.9,"passed":true}]},
 "outputs":[{"kind":"sideBySide","path":"cmp.png","format":"png","width":2716,"height":600,"bytes":88211}],
 "diagnostics":[],"echo":[]}
```

`outputs[].kind` is `diff`, `sideBySide`, or `overlay`; `diagnostics` entries are the loader's notes with the `side`
(`a` or `b`) they concern. When a threshold fails, the document is the failure envelope followed by the same fields:
`{"schemaVersion":1,"ok":false,"command":"compare","error":{"code":"threshold-failed",...},"tool":...,"a":...,
"metrics":...,"grid":...,"thresholds":{"passed":false,...},"outputs":[...]}`. A value of `null` in a check is an
infinite PSNR.

### montage

```text
canvasnet montage <inputs...> -o <output> [options]
```

`montage` composes one cell for each input into a contact sheet, in the order given, row by row. A document input
contributes its `--page` (default 1) or, with `--all-pages`, **every page** in ascending order; a model contributes one
view. The tool does not expand wildcards, so name the files (or let the shell expand them).

```bash
canvasnet montage a.png b.svg c.jpg -o sheet.png --labels
canvasnet montage deck.pptx --all-pages --columns 4 --cell 320x180 --labels -o deck-overview.png
```

| Option | Meaning |
| --- | --- |
| `-o`, `--output <file>` | The output (required); its extension selects the format (`--quality` applies to a JPEG) |
| `--columns <N>` | The number of columns, 1 to 64 (default: the square root of the number of cells, rounded up) |
| `--cell <WxH>` | The image area of each cell (default `256x256`; each side at least 16) |
| `--fit <contain\|stretch>` | How an image is fitted into its cell (default `contain`: scaled to fill the limiting side, keeping the aspect ratio, centered; `stretch`: resampled to the exact cell) |
| `--spacing <N>` | Pixels between cells (default 8) |
| `--padding <N>` | Pixels around the sheet (default 8) |
| `--background <color>` | The opaque color of the sheet (default white) |
| `--labels` | Draw the name of each input under its cell |
| `--page <N>`, `--all-pages` | The page of each document, or every page (together they are a `usage-error`) |
| `--dpi`, the model options, `--no-auto-orient`, `--strict`, `--filter`, `--quality`, `--no-clobber` | As for `render`; options that do not concern an input are skipped for it |

A vector input (SVG, document page, model) is drawn directly at the cell size, so it is sharp; a raster is fitted with
`--fit`. A cell is always scaled to fill the limiting side, because a contact sheet is a preview (there is no
`--allow-upscale`).

**Layout.** With `count` cells and `columns` columns, `rows = ceil(count / columns)`, the label strip is
`labelHeight = 22` pixels with `--labels` and 0 without, and

```text
width  = 2*padding + columns*cellW + (columns - 1)*spacing
height = 2*padding + rows*(cellH + labelHeight) + (rows - 1)*spacing
```

Cell `i` is in column `i % columns` and row `i / columns`, and its image area starts at
`x = padding + column*(cellW + spacing)`, `y = padding + row*(cellH + labelHeight + spacing)`. A sheet larger than 8192
pixels on a side is refused with `size-limit` (hint: a smaller `--cell` or more `--columns`) before any input is loaded
(with `--all-pages`, after the page counts were read from the file headers).

**Labels.** The label is the file name of the input (`report.pdf`), plus `p<N>` for a page of a document
(`report.pdf p2`), centered in the 22-pixel strip under the cell, 14 pixels high, in the bundled font (never an
installed font, so a sheet is the same on every machine). The text is black on a light background and white on a dark
one, and a name wider than the cell is cut with `...`.

The text result is `wrote sheet.png (1048x552 png, 52000 bytes): 6 images in 3 columns`. With `--json`:

```json
{"schemaVersion":1,"ok":true,"command":"montage","tool":{"name":"canvasnet","version":"<version>"},
 "sheet":{"width":1048,"height":552,"columns":3,"rows":2,"cellWidth":256,"cellHeight":256,"labelHeight":22,"spacing":8,"padding":8,"fit":"contain","labels":true,"background":"#FFFFFF"},
 "cells":[{"index":0,"column":0,"row":0,"x":8,"y":8,"input":"a.png","format":"png","loadedWidth":900,"loadedHeight":600,"label":"a.png"},
          {"index":1,"column":1,"row":0,"x":272,"y":8,"input":"deck.pptx","format":"pptx","page":2,"pageCount":6,"loadedWidth":960,"loadedHeight":540,"label":"deck.pptx p2"}],
 "outputs":[{"path":"sheet.png","format":"png","width":1048,"height":552,"bytes":52000}],
 "diagnostics":[{"input":"deck.pptx","page":2,"source":"pptx","severity":"unsupported","code":"...","message":"..."}],"echo":[]}
```

`x` and `y` are the top-left corner of the image area of the cell (the label strip is below it); `page` and
`pageCount` are present only for a document, and `label` only with `--labels`.

<!-- markdownlint-enable MD013 -->

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

`compare --json` adds `a`, `b`, `size`, `options`, `metrics`, `grid`, `worstCells`, `thresholds`, and `outputs`, and
`montage --json` adds `sheet`, `cells`, and `outputs`; both documents are described in the sections of the commands
(_compare_ and _montage_).

### Failure Documents

```json
{"schemaVersion":1,"ok":false,"command":"render","error":{"code":"page-out-of-range","message":"Page 9 does not exist: 'report.pdf' has 2 pages.","hint":"Pages are numbered from 1."}}
```

`error.code` is one of the codes in the next section, `error.message` describes the problem, and `error.hint`
(omitted when there is none) says what to do about it. The document has no exit-code field, because the exit code of
every failure is 1. A failure document is written only when `--json` is on the command line and `--silent` is not.

One failure carries more than the error. When `compare` is given `--threshold` and a limit is not met, the document is
the failure envelope followed by the fields of the comparison, so that a single parse gives the failure and the
numbers:

```json
{"schemaVersion":1,"ok":false,"command":"compare","error":{"code":"threshold-failed","message":"rmse 23.91 is above the limit 10","hint":"The result and any pictures were written. ..."},"tool":{"name":"canvasnet","version":"<version>"},"a":{"path":"ref.png",...},"b":{...},"metrics":{"identical":false,"rmse":23.9,...},"grid":{...},"worstCells":[...],"thresholds":{"passed":false,"checks":[{"metric":"rmse","comparison":"<=","limit":10,"value":23.9,"passed":false}]},"outputs":[...],"diagnostics":[],"echo":[]}
```

A program that reads `ok`, `error.code`, and `metrics` handles both outcomes of a gate in one parse.

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
| `size-mismatch` | `compare` was given two inputs of different pixel sizes and no `--resize-to` | Add `--resize-to a`, `b`, or `WxH` |
| `threshold-failed` | `compare` measured a value on the wrong side of a `--threshold` limit (the result and the pictures were still written) | Look at the pictures and the grid, fix the image or relax the limit |

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
| Compare: tolerance, grid, gain, overlay weight | 8, `4x4`, 4, 0.5 | `--tolerance`, `--grid`, `--gain`, `--overlay-weight` |
| Compare: inputs of different sizes | Refused (`size-mismatch`) | `--resize-to a\|b\|WxH`, with `--fit contain\|stretch` |
| Compare and montage: background | Opaque white | `--background <opaque color>` |
| Montage: cell, spacing, padding, fit, labels | `256x256`, 8, 8, `contain`, none | `--cell`, `--spacing`, `--padding`, `--fit`, `--labels` |
| Montage: columns | The square root of the number of cells, rounded up | `--columns` |
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
- Use `compare` to measure a redraw against its reference instead of judging by eye alone: `--threshold` turns it
  into a gate (`ok`, `error.code` `threshold-failed`), and the metrics are in the document whether it passed or not.
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

5. Compare the two pictures, and let the tool say where they differ. `--resize-to a` draws the SVG directly at the size
   of the reference (sharp, not enlarged afterwards), `--side-by-side` writes the reference, the redraw, and the
   differences in one picture to look at, and `worstCells` in the JSON names the grid cells (0-based `column` and
   `row`) with the largest error:

   ```bash
   canvasnet compare ref-page2.png redraw.svg --resize-to a --grid 4x4 --side-by-side cmp.png --json
   ```

   To look closely at a detail, cut the same region out of both, enlarged by the same factor:

   ```bash
   canvasnet render ref-page2.png --crop 200x120+100+80 --resize 1000x600 -o ref-detail.png
   canvasnet render redraw.svg --size 1250x1750 --crop 200x120+100+80 --resize 1000x600 -o redraw-detail.png
   ```

6. Fix what differs in the SVG and repeat from step 4 (or use the loop of _Recipe: Iterate a Redraw Until It
   Matches_). A text-heavy SVG depends on the fonts of the machine, so compare layout rather than exact glyph shapes.

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

### Recipe: Check an OpenSCAD Model Against a Sketch

`compare` accepts a model directly, so a view of the model can be measured against the sketch without a separate
render step. A sketch drawn from the front is compared with the front view; `--resize-to a` draws the model at the size
of the (prepared) sketch, and `--overlay` blends the two so that the agent can see where the outline drifts:

```bash
canvasnet render napkin.jpg --crop 5%,10%,90%,80% --fit 1600x1600 -o napkin-view.png
canvasnet compare napkin-view.png bracket.scad --b-view front --resize-to a --background white \
  --overlay overlay-front.png --side-by-side cmp-front.png --threshold ssim=0.6 --json
```

A sketch and a model rarely agree pixel for pixel, so use a loose threshold as a trend line (it should rise as the
model improves) and look at `overlay-front.png` for the real judgment. Try a dimension without editing the model by
adding `--b-define width=55`, and compare another view of the same model by changing `--b-view`. If `ok` is false and
`error.code` is `threshold-failed`, the numbers and the pictures were still written: read `worstCells` and the
overlay, adjust the model, and run the same command again.

### Recipe: Iterate a Redraw Until It Matches

An agent redrawing an image as an SVG can use `compare` as its loop condition. Each round is one command whose JSON
says whether to stop and where to work next:

```bash
canvasnet compare ref.png redraw.svg --resize-to a --grid 6x4 --threshold ssim=0.92 --threshold rmse=20 \
  --diff diff.png --side-by-side cmp.png --json
```

1. Parse the one line on standard output. If `ok` is true, every threshold is met: stop.
2. If `ok` is false and `error.code` is `threshold-failed`, the document still has `metrics`, `grid`, and `worstCells`
   (and the pictures were written). Look at `cmp.png`, concentrate on the cells in `worstCells` (the cell at column
   `c` and row `r` covers the pixels `x`, `y`, `width`, `height` given in `grid.cells`; zoom into it with
   `render ref.png --crop x,y,w,h`), edit the SVG, and run the command again. A falling `rmse` and a rising `ssim` mean
   the redraw is converging.
3. If `error.code` is `size-mismatch` you forgot `--resize-to`; for any other code (`corrupt-input` for an SVG that does
   not parse, `usage-error`) fix the command or the file, as the `message` and `hint` say.
4. Give the loop a limit (for example ten rounds), and keep the best attempt by writing each round's pictures to new
   names, because the pictures of a round replace the earlier ones of the same name.

### Recipe: Review Every Page at a Glance

To look at a whole document in one picture, compose a labeled contact sheet of its pages, then zoom into the page that
needs attention:

```bash
canvasnet info deck.pptx --json
canvasnet montage deck.pptx --all-pages --columns 4 --cell 320x180 --labels -o overview.png --json
canvasnet render deck.pptx --page 7 --dpi 150 -o slide7.png
```

The `cells` of the JSON result map each cell to its `input`, `page`, and position (`x`, `y`), and `diagnostics` lists
what was left out of a page (with its `page`). Several files can share one sheet (`montage a.pdf b.pdf --all-pages`):
the labels name each file and page. To review what changed between two versions of a document, compare the pages one
by one (`compare old.pdf new.pdf --a-page 3 --b-page 3 --diff changes-p3.png`) and use `montage` to lay the difference
pictures side by side.

## Limitations and Determinism

- **Determinism.** For the same input, command line, tool version, and machine, the output file is identical byte for
  byte, as is a JSON document. Different machines can differ where text is drawn from the host's fonts (text in SVG,
  PDF, PowerPoint, and Visio files that does not embed its font), so compare such pictures by eye rather than by
  checksum. Pictures made of shapes, rasters, and OpenSCAD models do not depend on fonts.
- **Determinism of `compare` and `montage`.** The measures of `compare` are computed from exact integer sums with a
  single division, square root, or logarithm at the end, so two runs on the same pixels give the same numbers, and
  its pictures and the contact sheets of `montage` are the same bytes on the same machine. The labels of `montage` use
  the bundled font, so they do not depend on installed fonts. What can differ between machines is the input pixels
  themselves where they come from resampling or from decoding (a JPEG, a raster fitted with `--fit`), so do not
  compare metrics of such inputs across machines with exact thresholds.
- **Memory.** `compare` holds both images and one picture at a time (an 8192 by 8192 image is about 256 MB per copy),
  and `montage` holds the sheet and one input at a time.
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
✓ CanvasNetTool_Compare - Passed
✓ CanvasNetTool_Montage - Passed

Total Tests: 13
Passed: 13
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
| `CanvasNetTool_Compare` | `compare` measures identical and different images exactly and exits 1 on a miss |
| `CanvasNetTool_Montage` | `montage` composes a labeled sheet of the expected size and layout |

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

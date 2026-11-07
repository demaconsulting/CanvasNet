# CanvasNet Viewer Package

<!-- cspell:ignore vsdx pptx scad href hrefs ssim rmse -->

## API Reference

### CanvasNetViewer

`CanvasNetViewer` is distributed via the separate `DemaConsulting.CanvasNet.Viewer` NuGet package (namespace
`DemaConsulting.CanvasNet.Viewer`), which references the core `DemaConsulting.CanvasNet` package and the Svg, Pdf, Pptx,
Vsdx, Scad, and VegaLite packages - see the Installation section of the project README.

The viewer is the one place that knows every file format of CanvasNet. It does three things without you wiring each
format package yourself:

- it creates the **image resolver** that lets an SVG document embed 3D models, charts, other SVG files, document pages,
  and pictures with `<image href="...">` (`CreateImageResolver`),
- it **renders any file** of those formats as a picture in one call (`Render`), and
- it does **everything the `canvasnet` command-line tool does** with images, from code (`ImageRenderer`,
  `ImageInspector`,
  `ImageComparer`, `MontageBuilder`, and `SourceLoader` of the `DemaConsulting.CanvasNet.Viewer.Imaging` namespace):
  probe a file, render it through an ordered pipeline of transformations, inspect colors and pixels, compare two images,
  and compose a contact sheet - see *Doing what the canvasnet commands do, from code* below.

Every example in this guide is a complete program (top-level statements, then any types): copy it into a console
project that references the `DemaConsulting.CanvasNet.Viewer` package and has implicit usings enabled (the .NET
default), and it compiles. The examples are compiled against the current API by the test suite, but never run, so the
files they name (`deck.pptx`, `part.scad`, ...) are placeholders for your own. They list their `using` directives, so
you
can see where each type lives:

- `DemaConsulting.CanvasNet.Viewer` holds `CanvasNetViewer`, `ViewerRenderOptions`, `ViewerImage`, and the exception
  `ViewerException` with its `ErrorCodes`;
- `DemaConsulting.CanvasNet.Viewer.Imaging` holds the operations (`SourceLoader`, `ImageRenderer`, `ImageInspector`,
  `ImageComparer`, `MontageBuilder`), their requests and results, and the image operations (`CropOperation`, ...);
- `DemaConsulting.CanvasNet.Canvas` holds `Surface`, `Rgba32`, `FitMode`, and `ResamplingMode`;
- `DemaConsulting.CanvasNet.Codecs` reads and writes the image files (`PngCodec`), and `DemaConsulting.CanvasNet.Svg`
  holds `SvgCodec` and `SvgLoadOptions`.

An example that catches a `ViewerException` therefore needs both `using DemaConsulting.CanvasNet.Viewer;` and
`using DemaConsulting.CanvasNet.Viewer.Imaging;`.

<!-- signature -->

```csharp
public static class CanvasNetViewer
{
    public static IReadOnlyList<IImageSource> ImageSources { get; }   // raster, svg, pdf, pptx, vsdx, scad, vega-lite
    public static IReadOnlyList<string> Extensions { get; }           // every extension they claim, sorted

    public static FolderImageResolver CreateImageResolver(string folder, FolderImageResolverOptions? options = null);
    public static FolderImageResolver CreateImageResolver(string folder, IEnumerable<IImageSource> extraSources,
        FolderImageResolverOptions? options = null);

    public static ViewerImage Render(string path, ViewerRenderOptions? options = null);
    public static ViewerImage Render(Stream content, string fileName, ViewerRenderOptions? options = null);
}
```

The list of built-in sources is fixed: there is no discovery, no reflection, and no global registry that can change it.
The entries of `ImageSources` are the viewer's own adapters over the loader that the `canvasnet` tool uses
(`SourceLoader`), so a linked file and a file given to `Render` are drawn by the same code; they have the names,
extensions, and order listed above, and they are not the stand-alone `PdfImageSource`, `SvgImageSource`, ... classes of
the format packages (which stay public for use without the viewer). The file types a link can point to are the
extensions in `CanvasNetViewer.Extensions`: `.png`, `.jpg`, `.jpeg`, `.bmp`,
`.tif`, `.tiff`, `.gif`, `.jp2`, `.j2k`, `.j2c` (rasters), `.svg`, `.scad` (an OpenSCAD model), `.vl.json` and `.vl`
(a Vega-Lite chart), and `.pdf`, `.pptx`, and `.vsdx` (a page of the document: page 1, or the page the fragment of the
link names, see *Choosing a page, slide, or view with a fragment* below). The type is decided by the
file name; a chart in a plain `.json` file is not linked.

#### Embedding files in an SVG document

Create a resolver for the folder of the document and hand it to the SVG codec:

<!-- compile -->

```csharp
using DemaConsulting.CanvasNet.Svg;
using DemaConsulting.CanvasNet.Viewer;

// diagram.svg contains: <image href="part.scad" x="10" y="10" width="120" height="120"/>
//                       <image href="sales.vl.json" x="140" y="10" width="200" height="120"/>
//                       <image href="logo.png" x="10" y="140" width="64" height="64"/>
var folder = "diagrams";
var resolver = CanvasNetViewer.CreateImageResolver(folder);
var options = new SvgLoadOptions { ImageResolver = resolver };
using var picture = SvgCodec.Load(Path.Combine(folder, "diagram.svg"), 400, 220, null, options);

foreach (var note in resolver.Diagnostics)           // warnings, such as an image that was not drawn
{
    Console.WriteLine($"{note.Code}: {note.Message}");
}

foreach (var image in resolver.Embedded)             // every image that was drawn
{
    Console.WriteLine($"{image.Href} ({image.Format}) {image.Width}x{image.Height}");
}
```

Links are confined to the folder: a relative path below it is followed (a query after the file name, as in
`logo.png?v=2`,
is ignored, and a fragment, as in `report.pdf#page=3`, is read as parameters, see below); an absolute path, a path that
leaves the folder, a symbolic link, and a
network URL (`http:`, `https:`) are never read or fetched. A link that is not drawn is a warning with the code
`svg-image-unresolved` in `Diagnostics` and draws nothing; so is a picture that would take more pixels than the render
may use (`FolderImageResolverOptions.MaxDecodedPixels`, 64 million by default, counted before anything is decoded, so a
huge raster in a tiny placement is refused and not decoded), with the code `svg-image-too-large`. A file that is found
but is broken throws `ImageSourceException`, which names the link and the embedding file and keeps the original
exception as its `InnerException`. Models and charts are drawn at the size of their placement over a transparent
background (a chart keeps its own background); a raster keeps its natural size and the SVG codec resamples it. See the
core user guide, *Linked Images*, for every rule.

**A chart can link pictures too.** The `image` mark of a Vega-Lite chart (`"url": "part.scad"`, `"url": "icon.png"`,
`"url": "report.pdf#page=2"`) is drawn through the same sandboxed resolver: a chart loaded by `SourceLoader` shows the
files that lie in its own folder, and a chart that an SVG or another document embeds uses the resolver of that
document. The notes, the echo text of a model, the embed operations, and `--strict` work as for an SVG; the library's
own
warning for a picture that was not drawn (`vl-mark-image-unresolved`) is dropped because the resolver's
`svg-image-unresolved` already names the link. More pictures than the limit (`MaxImages`, 256) is `budget-exceeded`, and
a
picture that is found but is broken fails with the error of its own format. Measuring a chart (the probe) loads no
picture.

Every note carries an `Origin`, the chain of embedded files that it is about, outermost first: for a link that was not
drawn it is the file that contains the link, and for a note that an embedded model or chart reported it is the embedding
files followed by that file. A note about the top-level document has an empty `Origin`, and the `Message` of a note
about
a link still starts with the href, as written. See *Diagnostics: Code and Origin* below.

**Create a new resolver for every render.** A resolver counts the images of one render and collects its notes, and it is
not thread-safe; creating one is cheap. It never caches a picture, so a live source is asked again every time.

#### Choosing a page, slide, or view with a fragment

The fragment of a link (the text after the first `#`) is a list of `name=value` parameters separated by `&`, in the
style
of RFC 8118 (`report.pdf#page=3`). A document link draws **page 1 unless the fragment says otherwise**:

- `report.pdf#page=3` draws page 3 of a PDF document;
- `deck.pptx#slide=2` draws slide 2 of a presentation (`#page=2` means the same);
- `drawing.vsdx#page=2` draws page 2 of a Visio drawing;
- `part.scad#view=front` draws an OpenSCAD model from a named view (`iso`, `front`, `back`, `left`, `right`, `top`,
  or `bottom`);
- `part.scad#def.width=20` sets the variable `width` of a model, exactly like `-D width=20` of the tool (a number,
  `true`, `false`,
  or text; `def.label=%22A%22` is the text `A`);
- `chart.vl.json#param.threshold=5` sets the parameter `threshold` of a chart, exactly like `--param threshold=5` (a
  number,
  `true`, `false`, `null`, text, or JSON, so `param.brush={"x":[2,6]}` sets the initial state of an interval selection);
- a raster and an SVG file take no parameter.

The `def.` and `param.` prefixes keep the names a link author chooses from colliding with `view=` and `page=`; the
prefix is not
case sensitive and the name after it is. A value that is not usable (a vector that is cut short, a name that is not an identifier,
malformed JSON)
is `image-fragment-invalid`, `def.` on a file that is not a model and `param.` on one that is not a chart is
`image-fragment-unknown-parameter`, and a `param.` name that the chart does not declare is the warning
`vl-param-unknown`. The built-in
sources declare the prefixes through `IFragmentImageSource.FragmentParameterPrefixes`.

The grammar is: the first `#` starts the fragment; `&` separates parameters (an empty one, as in `a&&b`, is skipped);
the
first `=` of a parameter splits the name from the value (no `=` means an empty value); `%XX` escapes are decoded after
that split, so `a=%26b` has the value `&b`; `+` is not a space. Names are not case sensitive. A page is a whole number
from 1 (digits only, leading zeros allowed). A fragment is at most 2048 characters with at most 32 parameters
(`ImageFragment.MaxLength` and `MaxParameters`). A `?query` before the fragment is ignored for a file link. In an SVG
file an `&` inside an attribute is written `&amp;` (`href="report.pdf#page=2&amp;page=3"`), as XML requires.

What the resolver reports, as notes in `Diagnostics`:

- `image-fragment-unknown-parameter` (`Warning`): the file does not use the parameter (a typo such as `pag=2`); the
  image is drawn without it.
- `image-fragment-duplicate-parameter` (`Warning`): a parameter has two different values; the last one wins and the
  image is drawn.
- `vl-param-unknown` (`Warning`): a `param.` parameter of a chart link names a parameter the chart does not declare; the
  chart is
  drawn without it (`Strict` does not fail on it).
- `image-fragment-invalid` (`Unsupported`): a value that is not allowed (`page=0`, `page=abc`, `view=` with no value, a
  `def.` vector, malformed `param.` JSON) or
  a fragment over the limits; nothing is drawn.
- `image-page-out-of-range` (`Unsupported`): the page or slide does not exist; the message tells how many there are;
  nothing is drawn.

Behavior change: a fragment on a file that takes no parameter used to be ignored silently; `logo.png#anything` and
`other.svg#top` now draw the picture and add `image-fragment-unknown-parameter`. A model link `part.scad#view` (no
value) is now `image-fragment-invalid`, and a missing `include` or `use` file of a model embedded in an SVG file is an
error as it is for `canvasnet render` (an `ImageSourceException` naming the link, with the `scad-error`
`ViewerException` as its `InnerException`), instead of a warning. An embedded file never inherits the options
of the document that embeds it: the page is the one in its own link.

A link to a page that does not exist leaves the rest of the document drawn:

<!-- compile -->

```csharp
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Svg;
using DemaConsulting.CanvasNet.Viewer;

// pages.svg contains: <image href="report.pdf#page=2" x="0" y="0" width="200" height="280"/>
//                     <image href="deck.pptx#slide=9" x="220" y="0" width="200" height="150"/>
var folder = "diagrams";
var resolver = CanvasNetViewer.CreateImageResolver(folder);
var options = new SvgLoadOptions { ImageResolver = resolver };
using var picture = SvgCodec.Load(Path.Combine(folder, "pages.svg"), 440, 280, null, options);

foreach (var note in resolver.Diagnostics)
{
    if (note.Code == FolderImageResolver.PageOutOfRangeCode)      // "image-page-out-of-range"
    {
        Console.WriteLine($"{string.Join(" > ", note.Origin)}: {note.Message}");
    }
}
```

With `--strict` on the command line an image that was not drawn (`image-fragment-invalid` or `image-page-out-of-range`,
like `svg-image-unresolved` and `svg-image-too-large`) is an `unsupported-feature` error; the two warnings above stay
warnings. The library never throws for such a note: read `Diagnostics`. Your own source can opt in to parameters by
implementing `IFragmentImageSource` (see the core user guide, *Linked Images*).

#### Rendering any file

`Render` draws one file as a picture, with the same sources, the same folder sandbox, and the same limits as embedding:

<!-- compile -->

```csharp
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Viewer;

using var image = CanvasNetViewer.Render("models/part.scad", new ViewerRenderOptions { Width = 512 });

Console.WriteLine($"{image.Format}: {image.Surface.Width}x{image.Surface.Height}");   // scad: 512x512
PngCodec.Save(image.Surface, "part.png");
```

`ViewerRenderOptions` has `Width` and `Height` (none: natural size; one: keep the aspect ratio; both: a box - a vector
file is drawn at that size and a raster is fitted into it; when given, each must be from 1 to 8192, otherwise `Render`
throws `ArgumentOutOfRangeException` before it reads anything), `Page` (the 1-based page or slide of a PDF, PPTX, or
VSDX
file, 1 by default; a page the file does not have is a `ViewerException` with the code `page-out-of-range`, and a value
below 1 throws `ArgumentOutOfRangeException` before the file is read), `Dpi` (the resolution a document page is sized
at,
96 by default, above 0 and at most 2400; a VSDX file takes a whole number), `Background` (the color transparent areas
are
flattened over, and the color of the margin when `Width` and `Height` fit the picture into a box, whose anchors follow
the picture; null keeps the transparency of a document, a model, and an SVG file, and the own background of a chart),
`Scad` (the `ScadViewOptions` of a model: the view, angles, projection, `Defines`, time, and triangle limit), `VegaLite`
(the
`ChartOptions` of a Vega-Lite chart: the values of its parameters, the option behind `--param`; see below), `Folder`
(the sandbox for the files the rendered file itself
includes or embeds; by default the folder of the file, or none for a stream), `ThrowOnUnsupportedFeature` (an
unsupported
feature fails instead of being drawn without it and reported; for an SVG file it also fails on a link that is not
drawn, as `--strict` does: a missing file, a picture too large, an unusable fragment, or a page that does not exist is a
`ViewerException` with the code `unsupported-feature`, while an unknown fragment parameter stays a warning),
`MaxDecodedPixels` (the most pixels the render may decode
in total; null keeps the resolver default of 64,000,000, which refuses a single 8192 x 8192 picture, so set it to render
one larger image; when a raster is resized to the requested size, the final picture is a second buffer that counts
against the same budget, so a 10 x 10 picture resized to 1 x 1 needs 101 pixels, and one that does not fit throws
`ImageSourceBudgetException`), `ExtraSources` (see *`ExtraSources` or `CreateImageResolver`?* below), and
`CancellationToken`. The `ViewerImage` owns the `Surface` (disposing it disposes the surface) and carries `Format`,
`Page` (the page that was drawn) and `PageCount` (how many the file has, so you can walk a document), `Diagnostics`,
`Echo` (the text a model printed with `echo`), `Anchors` (the named points a model declared, with their pixels; empty
for
every other file), and `Embedded`. `Render` throws `FileNotFoundException`
for a missing file, `NotSupportedException` when no source claims the extension (the message lists them),
`SandboxException` when the sandbox refuses the file, and `ImageSourceException` for a file that cannot be drawn
(a mistake of the caller or a limit - `page-out-of-range`, a size above 8192 pixels, a model whose `include` is
missing - is a `ViewerException` instead). Render
a file that is not on disk with `Render(stream, fileName)`; the extension of the file name chooses the source. With both
`Width` and `Height`, `Render` fits a raster inside the box over a transparent margin, whereas `SourceOptions.Width` and
`Height` of the `Imaging` operations stretch a raster (see *Sizes* below). A document or model that the rendered file
embeds chooses its own page or view, and sets its own variables or parameters, with the fragment of its link; it never
inherits
`Page`, `Dpi`, `Scad`, or `VegaLite`.

To render page 2 of a PDF document at 192 dpi, over white, in memory:

<!-- compile -->

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Viewer;

using var page = CanvasNetViewer.Render(
    "reports/q3.pdf",
    new ViewerRenderOptions { Page = 2, Dpi = 192, Background = new Rgba32(255, 255, 255, 255) });

Console.WriteLine($"page {page.Page} of {page.PageCount}: {page.Surface.Width}x{page.Surface.Height}");
foreach (var note in page.Diagnostics)
{
    Console.WriteLine($"{note.Severity} {note.Code}: {note.Message}");
}
```

A model with anchors, a named view, and its notes (`Detail` is the finer identifier of the library and `Location` the
position in the model file):

<!-- compile -->

```csharp
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasNet.Viewer.Imaging;

using var image = CanvasNetViewer.Render(
    "models/bracket.scad",
    new ViewerRenderOptions { Width = 600, Scad = new ScadViewOptions { View = "front", ViewGiven = true } });

foreach (var anchor in image.Anchors)
{
    Console.WriteLine($"{anchor.Name}: pixel {anchor.PixelX}, {anchor.PixelY}");
}

foreach (var line in image.Echo)
{
    Console.WriteLine($"echo: {line}");
}

foreach (var note in image.Diagnostics)
{
    Console.WriteLine($"{note.Code} {note.Detail} at {note.Location?.File}:{note.Location?.Line}: {note.Message}");
}
```

The parameters of a Vega-Lite chart are set with `ViewerRenderOptions.VegaLite`, a `ChartOptions` whose `Parameters`
take what
`VegaLiteRenderOptions.Parameters` takes (a number, text, a boolean, `null`, a list, or a dictionary, and for a
selection the shape of its
initial state). They are checked before the file is read (`ArgumentOutOfRangeException` for what the library rejects),
the
chart is laid out from them (so its size follows a parameter that changes the layout), and a name the chart does not
declare is a
`ViewerException` with the code `vega-lite-error` that lists the declared ones (`WarnOnUnknownParameters = true` makes
it the
warning `vl-param-unknown` instead, as for a link):

<!-- compile -->

```csharp
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasNet.Viewer.Imaging;

using var chart = CanvasNetViewer.Render(
    "charts/sales.vl.json",
    new ViewerRenderOptions
    {
        VegaLite = new ChartOptions
        {
            Parameters = new Dictionary<string, object?>
            {
                ["threshold"] = 5.0,
                ["brush"] = new Dictionary<string, object?> { ["x"] = new object[] { 2, 6 } }
            }
        }
    });

Console.WriteLine($"{chart.Format}: {chart.Surface.Width}x{chart.Surface.Height}");
```

`Render` returns pixels and writes no file. To save them with a format or a quality, use the writer of the `Imaging`
namespace (the third argument is the file you rendered, which the output may not be):

<!-- compile -->

```csharp
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasNet.Viewer.Imaging;

using var image = CanvasNetViewer.Render("models/part.scad", new ViewerRenderOptions { Width = 512 });
var written = ImageWriter.Write(
    image.Surface, "part.jpg", "models/part.scad", WriteOptions.Default with { Quality = 85 });
Console.WriteLine($"{written.Path}: {written.Width}x{written.Height}, {written.Bytes} bytes");
```

#### Doing what the canvasnet commands do, from code

Everything the `canvasnet` command-line tool does with images is a call of the `Imaging` namespace of the Viewer package
(`DemaConsulting.CanvasNet.Viewer.Imaging`); the tool only parses a command line into the same requests and prints the
results. Each operation takes plain request values and returns plain result values, and every expected failure is a
`ViewerException` (see *Handling errors* below). The paths of the operations are file paths, as on the command line.

Three conventions hold for every request type, so that a request with nothing but its required members does what the
command does without options:

- **The constructor takes only what is required; everything optional is an `init` property whose default is the default
  of the command line.** `new RenderRequest("out.png")`, `new MontageRequest("sheet.png")`, `new SheetOptions()`,
  `new WriteOptions()` (also `WriteOptions.Default`), `new SourceOptions()`, and `new CompareRequest(a, b)` are complete
  requests, and `with` works on them. Set a property only to change it.
- **Pages are numbered from 1**, as `--page` is: `SourceOptions.Page`, `MontageRequest.Page`, `SourcePage.Number`, and
  the
  `{page}` placeholder of an output name all start at 1 (the format packages themselves, such as `PdfDocument.Render`
  and `PptxDocument.Render`, count from 0; do not mix the two). The `Index` and `InputIndex` of a `MontageCell` are
  positions in a list and start at 0.
- **Numbers are in the units of the command line**: sizes in pixels, angles in degrees, and for a comparison the units
  of the table in *`compare`* below.
- **Some requests have a factory.** `InspectRequest.ForColors(count)` and `InspectRequest.ForPixel(point, radius)` build
  the two kinds (a pixel inspection needs a point, which a constructor cannot require). The result is a record, so add
  more with a `with` expression, for example `InspectRequest.ForColors(5) with { Operations = [new TrimOperation(8)] }`.
  An object initializer cannot follow a method call, so `InspectRequest.ForColors(5) { Operations = ... }` does not
  compile; after `new InspectRequest(InspectKind.Colors)` it does.

**Which call do I need: a file or pixels?** `ImageRenderer.Execute` **writes files** and returns where it wrote them
(`RenderedPage.Result` is a `WriteResult` with `Path`, `Format`, `Width`, `Height`, `Bytes`, and `Flattened`; it has no
`Surface`). To get **pixels in memory**, use `CanvasNetViewer.Render` (a `ViewerImage`) for one call without options, or
`SourceLoader.Load` (a `LoadedSource`) with the page, resolution, EXIF, and model options of the tool. Either surface
can
be written with the options of a render by `ImageWriter.Write(surface, path, inputPath, options)`.

- `ImageRenderer.Execute(info, request)` gives files on disk and a `RenderResult` (paths and sizes, no pixels)
- `CanvasNetViewer.Render(path, options)` gives a `ViewerImage`, which you dispose
- `SourceLoader.Load(info, options)` gives a `LoadedSource`, which you dispose

**`formats` - what can be read and written.** The input kinds by extension, and the output formats by extension:

<!-- compile -->

```csharp
using DemaConsulting.CanvasNet.Viewer.Imaging;

foreach (var (extension, kind) in SourceLoader.ExtensionKinds)      // .png, .svg, .pdf, .scad, ... -> SourceKind
{
    Console.WriteLine($"{extension} -> {kind}");
}

foreach (var (extension, format) in ImageWriter.ExtensionFormats)   // .png, .jpg, .bmp, .tif -> OutputFormat
{
    Console.WriteLine($"{extension} -> {format}");
}
```

**`info` - the facts of a file, without rendering it.** `SourceLoader.Probe` reads the header only: the detected kind,
the size, and for a document every page (a model's modules and functions, a chart's facts, are in the same record):

<!-- compile -->

```csharp
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasNet.Viewer.Imaging;

try
{
    var info = SourceLoader.Probe("deck.pptx");
    Console.WriteLine($"{info.Format}: {info.PageCount} page(s), page 1 is {info.Width}x{info.Height} pixels");
    foreach (var page in info.Pages)
    {
        Console.WriteLine($"  page {page.Number}: {page.Width}x{page.Height}");      // 1, 2, 3, ...
    }
}
catch (ViewerException ex)
{
    Console.Error.WriteLine($"{ex.Code}: {ex.Message}");
}
```

**`render`, `convert`, and the verbs `crop`, `resize`, `rotate`, `flip`, `pad` - load, transform, write.** An
`ImageRenderer` call loads the page (or every page) of a probed source, runs an ordered pipeline of operations over it,
and writes one file for each page. The operations are records, applied in the order of the list, and the output format
is
chosen by the extension of the output, or by `WriteOptions.Format`; `WriteOptions.Quality` is the JPEG quality. All the
files are written together or none:

<!-- compile -->

```csharp
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasNet.Viewer.Imaging;

var info = SourceLoader.Probe("part.scad");
var request = new RenderRequest("out/part.jpg")
{
    // View is iso (the default), front, back, left, right, top, or bottom; naming one is enough
    Source = new SourceOptions { Width = 800, Scad = new ScadViewOptions { View = "front" } },
    Write = WriteOptions.Default with { Quality = 85 },     // JPEG quality 1 to 100, default 90 (PNG, BMP, TIFF: none)
    Operations =
    [
        // Dimension.Percent(10) is 10 percent of the image size at this step; Dimension.Pixels(10) is 10 pixels
        new CropOperation(new GeometrySpec(
            Dimension.Percent(10), Dimension.Percent(10), Dimension.Percent(80), Dimension.Percent(80))),
        new RotateOperation(90),
        new GrayscaleOperation(),
        new GridOperation(50, null)
    ]
};

try
{
    var result = ImageRenderer.Execute(info, request);
    foreach (var page in result.Pages)
    {
        Console.WriteLine($"wrote {page.Result.Path} ({page.Result.Width}x{page.Result.Height})");
        foreach (var step in page.Operations)                 // what was done, in order, with its parameters
        {
            Console.WriteLine($"  {step.Op}");
        }
    }
}
catch (ViewerException ex)
{
    Console.Error.WriteLine($"{ex.Code}: {ex.Message} {ex.Hint}");
}
```

**Writing: `WriteOptions`.** How a file is written is `RenderRequest.Write`: `Quality` (JPEG only, 1 to 100, default
90), `Format` (`OutputFormat.Png`, `Jpeg`, `Bmp`, or `Tiff`; null takes it from the extension of the output, and wins
when set), `NoClobber` (refuse to overwrite), and `FlattenBackground` (the color a JPEG's transparency is flattened
over, white by default). Set them with `with` on the default, `Write = WriteOptions.Default with { Quality = 85 }`, or
with an initializer, `new WriteOptions { Format = OutputFormat.Jpeg, Quality = 85 }`. `MontageRequest.Write` and
`CompareRequest.Write` are the same type (for a comparison they apply to the difference, side-by-side, and overlay
pictures). You never need to render a PNG and re-encode it with a codec to get a JPEG of a given quality.

The command verbs are these operations: `crop` is `CropOperation(GeometrySpec)`, `resize` is `ResizeOperation(width,
height)`, `fit` is `FitOperation(width, height)` (add `allowUpscale: true` to enlarge), `rotate` is
`RotateOperation(degrees)` (any angle), `flip` is `FlipOperation(FlipAxis.Horizontal | Vertical)`, and `pad` is
`PadOperation(new PadSpec(left, top, right, bottom), color)`; the others are `GrayscaleOperation`, `InvertOperation`,
`ThresholdOperation`, `LevelsOperation`, `BrightnessOperation`, `ContrastOperation`, `AutoLevelsOperation` (the default
clips 1 percent at each end of the histogram, as the command does), `DeskewOperation`, `TrimOperation`, and
`GridOperation`. `SourceOptions` holds what the command-line options of a source hold (`Page`, `Dpi`, `Scale`, `Width`,
`Height`, `Background`, `Strict`, `AutoOrient`, `Filter`, and `Scad` for a model's view and variables). The view of a
model is `ScadViewOptions.View`, one of `iso` (the default), `front`, `back`, `left`, `right`, `top`, or `bottom` (any
case; `SourceLoader.ScadViews` lists them with their angles; another name is a `usage-error`), and `Azimuth` and
`Elevation` replace its angles. Naming a view other than `iso`, or setting an angle, is all it takes: you do **not**
need
`ViewGiven`, which only matters to ask for `iso` on a model that is entirely 2D (see the property's documentation).
With `AllPages` every page of a document is written, to a folder (an output
that ends with a directory separator) or to a name that contains `{page}`, which is replaced by the 1-based page
number. To transform a surface you already have, call
`ImagePipeline.Apply(surface, operations, defaultPadColor, out var applied)`, and to write one,
`ImageWriter.Write(surface, path, inputPathForGuard, options)`; `SourceLoader.Load(path, options)` gives the pixels of
one page as a `LoadedSource` that owns a `Surface`.

**Sizes: `SourceOptions.Width` and `Height`.** Each is 1 to 8192 (a larger value is `size-limit`). With **one** side the
other keeps the aspect ratio of the source (rounded to the nearest pixel; a model is 800 x 800 naturally, so one side
gives a square). With **both**, the loaded picture is always exactly `Width` x `Height`, and the aspect ratio is handled
per kind:

- a **raster** (PNG, JPEG, BMP, TIFF, GIF, JPEG 2000) is resampled to exactly that size, so it is **stretched** when the
  aspect ratio differs (add a `FitOperation` after loading to keep the ratio);
- an **SVG** is drawn at that size and fitted by its own `preserveAspectRatio` (by default the aspect ratio is kept and
  the drawing is centered);
- a **PDF, PPTX, or VSDX** page is fitted inside the box and centered, never stretched, with `Background` in the margin;
- an **OpenSCAD** model is framed to the box (not stretched), and a **Vega-Lite** chart is scaled uniformly and centered
  over its background.

`Scale` takes precedence: when it is set, `Width` and `Height` are ignored (the command line rejects the combination).
`CanvasNetViewer.Render` is different for a raster: with both sizes it fits the picture inside the box over a
transparent margin (see `ViewerRenderOptions`).

**Cropping.** Every value of a `GeometrySpec` is a non-negative `Dimension` (`Dimension.Pixels(n)` or
`Dimension.Percent(n)`): the origin must be 0 or more, the width and the height must be more than 0, and a percentage
must be at most 100, otherwise the call fails with a `usage-error` before anything is loaded. There is no negative or
"inset" form, so to cut 20 pixels off every side, give the region that is left. A region that extends beyond the right
or the bottom edge is cut at the edge; one that starts at or beyond an edge is a `usage-error`. A percentage is of the
size of the image at that step of the pipeline (after the operations before it):

<!-- compile -->

```csharp
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasNet.Viewer.Imaging;

var info = SourceLoader.Probe("scan.png");
const int inset = 20;

// 20 pixels off every side of the picture: the region starts at (20, 20) and is 40 pixels smaller in each direction
var pixels = new CropOperation(new GeometrySpec(
    Dimension.Pixels(inset), Dimension.Pixels(inset),
    Dimension.Pixels(info.Width - 2 * inset), Dimension.Pixels(info.Height - 2 * inset)));

// when the size is not known (or later steps change it), a percentage works at any size: 5 percent off every side
var percent = new CropOperation(new GeometrySpec(
    Dimension.Percent(5), Dimension.Percent(5), Dimension.Percent(90), Dimension.Percent(90)));

try
{
    ImageRenderer.Execute(info, new RenderRequest("cropped.png") { Operations = [pixels] });
    ImageRenderer.Execute(info, new RenderRequest("cropped-percent.png") { Operations = [percent] });
}
catch (ViewerException ex) when (ex.Code == ErrorCodes.UsageError)
{
    Console.Error.WriteLine($"{ex.Message} {ex.Hint}");    // for example a picture of 40 pixels or less
}
```

The width and the height of `info` are those of page 1 at its natural size; when you set `SourceOptions.Width`,
`Dpi`, or `Scale`, or put a resize in front of the crop, work in percent or take the size from the earlier step.

**Every page of a document.** `AllPages` writes one file for each page; `{page}` in the output name is the page number,
counted from 1:

<!-- compile -->

```csharp
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasNet.Viewer.Imaging;

var info = SourceLoader.Probe("deck.pptx");

try
{
    SourceLoader.CheckPage(info, 99);        // optional: fails now, with page-out-of-range, before any work is done
}
catch (ViewerException ex) when (ex.Code == ErrorCodes.PageOutOfRange)
{
    Console.Error.WriteLine($"The deck has {info.PageCount} slides.");
}

var all = ImageRenderer.Execute(info, new RenderRequest("slides/slide-{page}.png") { AllPages = true });
Console.WriteLine(string.Join(", ", all.Pages.Select(page => $"{page.Page} -> {page.Result.Path}")));   // 1 -> ...

var third = ImageRenderer.Execute(
    info, new RenderRequest("slide3.png") { Source = new SourceOptions { Page = 3, Dpi = 150 } });
Console.WriteLine(third.Pages[0].Result.Path);
```

**`inspect colors` and `inspect pixel` - numbers instead of a picture.** The operations of the request run first, so you
can measure the picture after a trim or a crop:

<!-- compile -->

```csharp
using DemaConsulting.CanvasNet.Viewer.Imaging;

var info = SourceLoader.Probe("photo.jpg");

// The request is a record: the factory gives the kind and `with` sets the rest. This is the same request as
// new InspectRequest(InspectKind.Colors) { Count = 5, Operations = [new TrimOperation(8)] }
var colors = ImageInspector.Execute(
    info, InspectRequest.ForColors(5) with { Operations = [new TrimOperation(8)] });
foreach (var color in colors.Colors!.Colors)              // the dominant colors, largest first
{
    Console.WriteLine($"{color.Color} {color.Fraction:P1}");
}

// A pixel needs a point, so use the factory; the point is in the coordinates after the operations
var pixel = ImageInspector.Execute(
    info, InspectRequest.ForPixel(new PixelPoint(10, 20), radius: 1) with { Operations = [new TrimOperation(8)] });
Console.WriteLine($"{pixel.Pixel!.Pixel} luma {pixel.Pixel.Luma}");
```

**`compare` - how different are two images?** Two inputs are brought to one size (`Resize`), measured, checked against
thresholds, and, if asked, written as a difference picture, a side-by-side picture, or an overlay. A threshold that is
not met is **data** in the result (`Passed` is false), not an exception; you decide what a miss means. The numbers have
fixed scales, and a threshold limit must be given in the same unit as its metric:

| Metric (`ThresholdMetric`) | Unit and range                                                   | A threshold is |
| -------------------------- | ---------------------------------------------------------------- | -------------- |
| `Mae`, `Rmse`, `MaxDiff`   | the 0 to 255 channel scale (not 0 to 1); 0 is identical          | a maximum      |
| `CellRmse`                 | the 0 to 255 scale of the worst cell of the grid                 | a maximum      |
| `DiffRatio`                | a fraction from 0 to 1 (not a percent) of the pixels that differ | a maximum      |
| `Psnr`                     | decibels; higher is better, identical images are infinite        | a minimum      |
| `Ssim`                     | from -1 to 1; 1 is identical                                     | a minimum      |

An RMSE limit of 10 is therefore about 4 percent of full scale. `Tolerance` (0 to 255) only decides which pixels count
as different for `DiffRatio` and the differing-pixel count (a pixel differs when any of its R, G, or B channels differs
by more than `Tolerance`); it does not change the other metrics.

`Resize` chooses the common size: `ResizeTarget.ToInputA` loads the second input at the size of the first,
`ResizeTarget.ToInputB` the reverse, `ResizeTarget.To(width, height)` loads both at an explicit size (1 to 8192), and
null compares the inputs at their own sizes (which must then be equal, otherwise the call fails with `size-mismatch`).
A vector input is drawn at the target size and a raster is fitted into it with `Fit` and `Filter`:

<!-- compile -->

```csharp
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasNet.Viewer.Imaging;

var request = new CompareRequest(
    new CompareInput("a", SourceLoader.Probe("reference.png")),
    new CompareInput("b", SourceLoader.Probe("redraw.svg")))
{
    Resize = ResizeTarget.ToInputA,                  // load b at the size of a
    Tolerance = 8,                                   // 0-255: a pixel differs when a channel differs by more than 8
    Thresholds =
    [
        new ComparisonThreshold(ThresholdMetric.Rmse, 10),        // 0-255 scale: at most about 4% of full scale
        new ComparisonThreshold(ThresholdMetric.DiffRatio, 0.01), // a fraction: at most 1% of the pixels differ
        new ComparisonThreshold(ThresholdMetric.Ssim, 0.95)       // a minimum
    ],
    DiffPath = "diff.png"
};

try
{
    var comparison = ImageComparer.Execute(request);
    Console.WriteLine($"RMSE {comparison.Metrics.Rmse:F2} of 255, SSIM {comparison.Metrics.Ssim:F4}");
    foreach (var cell in comparison.Comparison.WorstCells(3))      // the grid cells that differ most
    {
        Console.WriteLine($"cell {cell.Column},{cell.Row}: RMSE {cell.Rmse:F2}");
    }

    foreach (var check in comparison.Checks)
    {
        var verdict = check.Passed ? "passed" : "failed";
        Console.WriteLine($"{ImageComparison.MetricName(check.Metric)}: {verdict} ({check.Value}, limit {check.Limit})");
    }

    Console.WriteLine(comparison.Checks.All(check => check.Passed) ? "images match" : "images differ");
}
catch (ViewerException ex) when (ex.Code == ErrorCodes.SizeMismatch)
{
    Console.Error.WriteLine($"{ex.Message} {ex.Hint}");
}
```

The result is flat: `comparison.Metrics` holds the numbers (`Mae`, `Rmse`, `Psnr`, `Ssim`, `DiffRatio`, `MaxDiff`,
`Identical`), `comparison.Comparison` is the full measurement (`Cells`, `WorstCells`, `MaxCellRmse`, the tolerance), and
`comparison.A`, `comparison.B`, `comparison.Checks`, and `comparison.Pictures` describe the two sides, the thresholds,
and the pictures written.

**`montage` - a contact sheet.** The sources are opened one at a time, so the memory is the sheet plus one source. The
label under a cell is the file name of the input, followed by `#` and the fragment of the input when it has one, and by
`p{page}` for a page of a document that the fragment does not name (`Labels = true` draws
it, and `MontageCell.Label` is the text). `Page` is 1-based; null shows page 1:

<!-- compile -->

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasNet.Viewer.Imaging;

var infos = Directory.GetFiles("assets").Select(SourceLoader.Probe).ToList();   // images, and documents (first page)
var request = new MontageRequest("sheet.png")
{
    Sheet = new SheetOptions
    {
        Columns = 4,                       // 0 chooses a roughly square grid
        CellWidth = 200,
        CellHeight = 150,
        Background = new Rgba32(255, 255, 255, 255),
        Labels = true
    }
};

try
{
    var montage = MontageBuilder.Execute(infos, request);
    Console.WriteLine($"{montage.Layout.Width}x{montage.Layout.Height}, {montage.Cells.Count} cells");
    foreach (var cell in montage.Cells)
    {
        Console.WriteLine($"{cell.Label} at {cell.X},{cell.Y}");
    }
}
catch (ViewerException ex)
{
    Console.Error.WriteLine($"{ex.Code}: {ex.Message}");
}
```

**Inputs with a fragment - `part.scad#def.width=20`, `report.pdf#page=3`.** The command line accepts the syntax of a
link of an SVG image after the file name, and so does the library: `InputReference.Parse(text)` splits the text (a `#`
starts a fragment only when no file has the name as written; the longest existing file before a `#` is the path), and
`SourceLoader.Probe(InputReference)` reads the fragment with the built-in image source of the kind of the file, so the
parameters and the values are those of a link (`page` and `slide`, `view`, `def.<name>` with a number, `true`, `false`,
text, or a vector as a JSON array, and `param.<name>`). The facts it returns carry the result in `SourceInfo.Selection`
(an `InputSelection`) and `SourceInfo.Specifier` (the path, `#`, and the fragment); `ImageRenderer`, `ImageInspector`,
`ImageComparer`, `MontageBuilder`, and `SourceLoader.Load` apply it over their options, the fragment winning (its page
replaces the page, its view the view, and its variables and parameters are merged by name). A value that cannot be used
is a `ViewerException` with `ErrorCodes.ImageFragmentInvalid`, a page the file does not have is
`ErrorCodes.PageOutOfRange`, and a parameter that is unknown or repeated is a warning among the diagnostics of the first
page that is loaded (`image-fragment-unknown-parameter`, `image-fragment-duplicate-parameter`, `vl-param-unknown`; never
an error). To draw several versions of a model at one scale, fix the camera with `$vpd` (and `$vpt` to centre it), as a
variable of the command or of each fragment:

<!-- compile -->

```csharp
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasNet.Viewer.Imaging;

string[] inputs =
[
    "assembly.scad#def.explode=0&def.$vpd=360&def.$vpt=[0,0,50]",
    "assembly.scad#def.explode=0.5&def.$vpd=360&def.$vpt=[0,0,50]",
    "assembly.scad#def.explode=1&def.$vpd=360&def.$vpt=[0,0,50]"
];

try
{
    var infos = inputs.Select(text => SourceLoader.Probe(InputReference.Parse(text))).ToList();
    var request = new MontageRequest("explode.png") { Sheet = new SheetOptions { Labels = true } };
    var montage = MontageBuilder.Execute(infos, request);
    foreach (var cell in montage.Cells)
    {
        Console.WriteLine(cell.Label);      // assembly.scad#def.explode=0&def.$vpd=360&def.$vpt=[0,0,50], ...
    }
}
catch (ViewerException ex) when (ex.Code == ErrorCodes.ImageFragmentInvalid)
{
    Console.Error.WriteLine($"{ex.Message} {ex.Hint}");
}
```

**Where do notes, echo, and anchors come from?** A model reports three kinds of data besides its picture: warnings
(diagnostics), the text it printed with `echo`, and its anchors (named points with the pixel they land on). Not every
result carries all three:

| Result                                            | Diagnostics | Echo | Anchors | Diagnostic type         |
| ------------------------------------------------- | ----------- | ---- | ------- | ----------------------- |
| `RenderedPage` (of `ImageRenderer`)               | yes         | yes  | yes     | `ImageDiagnostic`       |
| `LoadedSource` (of `SourceLoader.Load`)           | yes         | yes  | yes     | `ImageDiagnostic`       |
| `InspectResult`, `CompareSide`, `MontageCell`     | yes         | yes  | no      | `ImageDiagnostic`       |
| `ViewerImage` (of `CanvasNetViewer.Render`)       | yes         | yes  | yes     | `ImageSourceDiagnostic` |
| `FolderImageResolver` (an SVG render)             | yes         | yes  | no      | `ImageSourceDiagnostic` |

`ImageRenderer`, `SourceLoader.Load`, and `CanvasNetViewer.Render` give anchors; the other operations cannot report
them.
The two diagnostic
types carry the same facts: `ImageDiagnostic` (the Imaging namespace) has a string `Severity` and a page, and
`ImageSourceDiagnostic` (the core package) has an `ImageDiagnosticSeverity` and a `Page`; both have `Source`, `Code`,
`Message`, `Detail`, `Location`, and `Origin`:

<!-- compile -->

```csharp
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasNet.Viewer.Imaging;

var info = SourceLoader.Probe("bracket.scad");
var page = ImageRenderer.Execute(info, new RenderRequest("bracket.png")).Pages[0];

foreach (var anchor in page.Anchors)                       // anchor("name", ...) calls of the model
{
    Console.WriteLine($"{anchor.Name}: ({anchor.X}, {anchor.Y}, {anchor.Z}) lands on pixel {anchor.PixelX}, {anchor.PixelY}");
}

foreach (var line in page.Echo)                            // echo(...) output, one line each
{
    Console.WriteLine($"echo: {line}");
}

foreach (var note in page.Diagnostics)                     // warnings; an anchor that cannot be read is scad-anchor-invalid
{
    Console.WriteLine($"{note.Severity} {note.Code}: {note.Message}");
}

// the same data of CanvasNetViewer.Render, including the anchors
using var image = CanvasNetViewer.Render("bracket.scad");
foreach (var note in image.Diagnostics)
{
    Console.WriteLine($"{note.Severity} {note.Code} [{string.Join(" > ", note.Origin)}]: {note.Message}");
}
```

**Diagnostics: `Code`, `Detail`, `Location`, and `Origin`.** Test the `Code` of a note, never its message. The code of a
note for a model warning is the generic `scad-warning` (a chart warning is `vega-lite-warning`); the finer identifier of
the library's own warning, such as `scad-anchor-invalid` or `scad-include-not-found`, or the `vl-*` identifier of a
chart
warning, is in `Detail` (it is `null` when the `Code` is already the most specific). `Location` is where the note
points:
`File`, `Line`, and `Column`, and for a chart the JSON `Pointer`; a member the source did not know is `null`, and the
whole `Location` is `null` for a note without a position (a page note has the 1-based `Page` instead). `Origin` names
where the note comes from when the file was embedded in another: for `part.scad` embedded by
`a.svg`, a warning of the model has `Origin` `["a.svg", "part.scad"]`; for a link that did not resolve inside `a.svg` it
is `["a.svg"]`. A file link is shown as its folder-relative file name and a scheme link as its href.

**Who disposes what.**

- **Dispose** a `ViewerImage` (`CanvasNetViewer.Render`) and a `LoadedSource` (`SourceLoader.Load`,
  `PageSource.ReadPage`): each owns one `Surface`, and disposing it disposes the surface. Dispose the owner, not both,
  and use `using var`.
- **Dispose** a `PageSource` (`SourceLoader.OpenPages`) and a `ContactSheet` (or take its pixels with `Detach()` and
  dispose that surface). Pages already read from a `PageSource` stay valid after it is disposed.
- **Dispose** the `Surface` of an `ImageSourceResult` (from `FolderImageResolver.Render` or your own source): the result
  is a record and is **not** `IDisposable`, so it cannot be in a `using`.
- **Nothing to dispose**: a `FolderImageResolver` (not `IDisposable`; it holds no open files, so no `using`), the
  results
  of `ImageRenderer`, `ImageInspector`, `ImageComparer`, and `MontageBuilder`, a `WriteResult`, and a `SourceInfo`
  (plain
  values, no pixels).
- `ImagePipeline.Apply` returns a **new** surface that you dispose; its input is neither changed nor disposed.

**Handling errors.** An expected failure (a missing, unsupported, or corrupt file, a page that does not exist, a size
beyond 8192 pixels, an output that cannot be written, inputs that cannot be compared) is a `ViewerException` with a
stable `Code` (one of the strings of `ErrorCodes`, the same ones the tool prints), a message, and a hint. Anything else
is a defect and is not caught. The codes are `usage-error`, `operation-failed`, `input-not-found`, `input-unreadable`,
`unsupported-format`, `corrupt-input`, `unsupported-feature`, `page-out-of-range`, `size-limit`, `same-input-output`,
`output-exists`, `output-write-failed`, `scad-error`, `vega-lite-error`, `budget-exceeded`, `size-mismatch`, and
`threshold-failed`. `ViewerException` is in `DemaConsulting.CanvasNet.Viewer` and the operations are in
`DemaConsulting.CanvasNet.Viewer.Imaging`, so a program that catches it lists both:

<!-- compile -->

```csharp
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasNet.Viewer.Imaging;

var path = args.Length > 0 ? args[0] : "photo.png";
try
{
    var info = SourceLoader.Probe(path);
    ImageRenderer.Execute(info, new RenderRequest("out/photo.jpg") { Source = new SourceOptions { Page = 2 } });
}
catch (ViewerException ex) when (ex.Code == ErrorCodes.PageOutOfRange)
{
    Console.Error.WriteLine($"{ex.Message} {ex.Hint}");
}
catch (ViewerException ex) when (ex.Code is ErrorCodes.InputNotFound or ErrorCodes.UnsupportedFormat)
{
    Console.Error.WriteLine($"Cannot read {path}: {ex.Message}");
}
catch (ViewerException ex)
{
    Console.Error.WriteLine($"{ex.Code}: {ex.Message}");
}
```

The operation records you build yourself are checked up front: a value that no command line could produce (a resize to
0x0, a levels black point above the white point, a rotation of NaN) is a `usage-error` `ViewerException` (`size-limit`
above 8192 pixels) whose message names the position and the operation, raised before anything is decoded or written. A
request with a missing member, or a null element in a list, is a defect of the caller and throws
`ArgumentNullException`. The format in `WriteOptions.Format` is used for every output, also for one without an extension
or with a custom one; without it the extension decides.

A note about the two ways to draw a file: `CanvasNetViewer.Render` (above) is the simple way to get a picture of a file
(transparent background for vector files, the fit of a raster into a box, the image-source sandbox, and its own
exceptions), and the `Imaging` operations are the way the `canvasnet` tool works (document pages and resolution, EXIF
orientation, a flattened background, the ordered pipeline, and the `ViewerException` codes). The messages and hints of
the
operations are written for the command line and name the options of the tool (for example `--all-pages`). The `Imaging`
operations write files (see *Which call do I need: a file or pixels?*).

#### Using only some formats, without the Viewer

If you need few formats, skip the Viewer package and compose a resolver from the sources you reference, one line:

<!-- compile -->

```csharp
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Scad;
using DemaConsulting.CanvasNet.Svg;

var folder = "diagrams";
var resolver = new FolderImageResolver(folder, [new RasterImageSource(), new SvgImageSource(), new ScadImageSource()]);
Console.WriteLine(resolver.Diagnostics.Count);
```

#### Adding your own image source

The Viewer is for library users: a program that uses the library can add file types, override how a built-in type is
drawn, or draw a live picture. The command-line tool has no such plug-in mechanism. A source implements `IImageSource`
(see the core user guide for the whole contract) and claims **file name extensions**, **URI schemes**, or both.

<!-- compile -->

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Svg;
using DemaConsulting.CanvasNet.Viewer;

var folder = "dashboards";
var myCamera = new StubCamera();

// Pass the sources for one render; <image href="grid.dots"/> and <image href="camera://front"/> now draw.
var resolver = CanvasNetViewer.CreateImageResolver(folder, [new DotsSource(), new CameraSource(myCamera)]);
var options = new SvgLoadOptions { ImageResolver = resolver };
using var picture = SvgCodec.Load(Path.Combine(folder, "dashboard.svg"), 800, 600, null, options);

// The same sources work for Render of a single file
using var dots = CanvasNetViewer.Render("grid.dots", new ViewerRenderOptions { ExtraSources = [new DotsSource()] });

// 1. Claimed by extension: the file is found inside the folder and the source receives its bytes.
//    The text of a ".dots" file is the number of dots to draw.
sealed class DotsSource : IImageSource
{
    public string Name => "dots";
    public IReadOnlyList<string> Extensions => [".dots"];
    public IReadOnlyList<string> Schemes => [];

    public ImageSourceResult Render(ImageSourceRequest request)
    {
        using var reader = new StreamReader(request.OpenContent());
        var count = int.Parse(reader.ReadToEnd().Trim());
        var (width, height) = request.SizeFor(200, 100);          // the size of the placement, or 200x100
        var surface = new Surface(width, height);
        // ... draw `count` dots ...
        return new ImageSourceResult(surface) { Format = "dots" };
    }
}

// 2. Claimed by URI scheme: "camera://front" is handed to the source as written; there is no file and no sandbox.
sealed class CameraSource(ICamera camera) : IImageSource
{
    public string Name => "camera";
    public IReadOnlyList<string> Extensions => [];
    public IReadOnlyList<string> Schemes => ["camera"];

    public ImageSourceResult Render(ImageSourceRequest request)
    {
        // request.Href is "camera://front". Treat it as untrusted: only the cameras you list may be opened.
        var frame = camera.GrabFrame(request.Href);               // a NEW surface on every call
        return new ImageSourceResult(frame) { Format = "camera" };
    }
}

// The device of your application; here a stub that returns a blank frame
interface ICamera
{
    Surface GrabFrame(string href);
}

sealed class StubCamera : ICamera
{
    public Surface GrabFrame(string href) => new(64, 48);
}
```

- **The later source wins.** Extra sources are placed after the built-in ones. When two sources claim the same extension
  or scheme, the later one takes precedence, so a source that claims `.png` replaces the built-in raster source, and a
  second source that claims `camera` replaces the first. Claims are validated up front (an extension is a dot and a
  name, a scheme has two or more characters, and `file` and `data` can never be claimed).
- **Nothing is cached.** The source is called again for every link, and the surface it returns is owned and disposed
  by the caller, so return a new picture on every call. Create a new resolver for every render; a live source gives a
  fresh frame each time.
- **`http` and `https` stay unresolved.** No built-in source claims them, so such a link is only the
  `svg-image-unresolved` warning. A source that claims them takes full responsibility for what it fetches.
- **Security.** A file-based source receives bytes the resolver has already confined to the folder. A **scheme source
  owns its own access control**: it receives the text of the link as written and decides what that may reach. Validate
  it (a list of allowed cameras, hosts, or paths), never build a file path or a URL from it unchecked, and bound the
  work
  it does; the resolver counts the images and the depth of scheme links like any other, but it cannot confine what the
  source reaches.
- **Charge the pixel budget before you allocate.** A render may allocate only so many pixels in all
  (`MaxDecodedPixels`), from one live budget shared by every picture, nested ones included. Call
  `request.EnsureWithinBudget(width, height)` once for each surface you are about to allocate, before allocating it,
  with the size you will allocate: it takes the pixels from the budget (and records them in `request.ChargedPixels`), or
  throws `ImageSourceBudgetException` and charges nothing, which the resolver turns into an `svg-image-too-large`
  warning (an exception for a direct `Render`). `request.RemainingPixels` always reads what is free right now. The
  resolver reserves nothing for you, charges what you return beyond what you charged, and gives back what you charged
  when you throw. The full contract is in the XML documentation of `ImageSourceRequest`.
- **Throw for a file that cannot be drawn.** Throw for a corrupt file or an unsupported feature when
  `request.ThrowOnUnsupportedFeature` is set; the resolver wraps it in an `ImageSourceException` that names the link and
  the embedding file. Return notes in `ImageSourceResult.Diagnostics` and printed text in `Echo`.

#### `ExtraSources` or `CreateImageResolver`?

Both add your sources to the built-in ones (the later source wins in both), and the choice is only who draws:

- **`ViewerRenderOptions.ExtraSources`** is for `CanvasNetViewer.Render`. `Render` builds its own resolver from the
  built-in sources plus yours, so you never create or see a resolver; use it when you want a picture of one file.
- **`CanvasNetViewer.CreateImageResolver(folder, extraSources)`** is for a render that you drive yourself, such as
  `SvgCodec.Load` with `SvgLoadOptions.ImageResolver`; it gives you the resolver, with its `Diagnostics` and `Embedded`
  lists, for that one render.

<!-- compile -->

```csharp
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Svg;
using DemaConsulting.CanvasNet.Viewer;

IImageSource[] mine = [new StubSource()];

// You draw the SVG yourself: create the resolver, pass it to the codec, and read its notes afterwards
var resolver = CanvasNetViewer.CreateImageResolver("diagrams", mine);
using var svg = SvgCodec.Load("diagrams/overview.svg", 400, 300, null, new SvgLoadOptions { ImageResolver = resolver });
Console.WriteLine($"{resolver.Embedded.Count} image(s) drawn, {resolver.Diagnostics.Count} note(s)");

// The viewer draws a file for you: it makes its own resolver from the built-in sources and ExtraSources
using var image = CanvasNetViewer.Render("diagrams/overview.svg", new ViewerRenderOptions { ExtraSources = mine });
PngCodec.Save(image.Surface, "overview.png");

sealed class StubSource : IImageSource
{
    public string Name => "stub";
    public IReadOnlyList<string> Extensions => [".stub"];
    public IReadOnlyList<string> Schemes => [];

    public ImageSourceResult Render(ImageSourceRequest request) =>
        new(new DemaConsulting.CanvasNet.Canvas.Surface(8, 8)) { Format = "stub" };
}
```

## Examples

### Example 1: A Dashboard With a Model, a Chart, and a Picture

<!-- compile -->

```csharp
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Svg;
using DemaConsulting.CanvasNet.Viewer;

var resolver = CanvasNetViewer.CreateImageResolver("dashboard");
var options = new SvgLoadOptions { ImageResolver = resolver };
using var picture = SvgCodec.Load("dashboard/overview.svg", 800, 480, null, options);
PngCodec.Save(picture, "overview.png");

if (resolver.Diagnostics.Count > 0)
{
    Console.Error.WriteLine("Some images were not drawn.");
}
```

### Example 2: Thumbnails of Every File in a Folder

<!-- compile -->

```csharp
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Viewer;

foreach (var path in Directory.EnumerateFiles("assets")
             .Where(p => CanvasNetViewer.Extensions.Any(e => p.EndsWith(e, StringComparison.OrdinalIgnoreCase))))
{
    using var thumbnail = CanvasNetViewer.Render(path, new ViewerRenderOptions { Width = 128, Height = 128 });
    PngCodec.Save(thumbnail.Surface, Path.ChangeExtension(path, ".thumb.png"));
}
```

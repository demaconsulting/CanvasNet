# CanvasNet Viewer Package

<!-- cspell:ignore vsdx pptx scad href hrefs ssim -->

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
- it does **everything the `canvasnet` command-line tool does** with images, from code (`ImageRenderer`, `ImageInspector`,
  `ImageComparer`, `MontageBuilder`, and `SourceLoader` of the `DemaConsulting.CanvasNet.Viewer.Imaging` namespace):
  probe a file, render it through an ordered pipeline of transformations, inspect colors and pixels, compare two images,
  and compose a contact sheet - see *Doing what the canvasnet commands do, from code* below.

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
The file types a link can point to are the extensions in `CanvasNetViewer.Extensions`: `.png`, `.jpg`, `.jpeg`, `.bmp`,
`.tif`, `.tiff`, `.gif`, `.jp2`, `.j2k`, `.j2c` (rasters), `.svg`, `.scad` (an OpenSCAD model), `.vl.json` and `.vl`
(a Vega-Lite chart), and `.pdf`, `.pptx`, and `.vsdx` (the first page of the document). The type is decided by the
file name; a chart in a plain `.json` file is not linked.

#### Embedding files in an SVG document

Create a resolver for the folder of the document and hand it to the SVG codec:

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

Links are confined to the folder: a relative path below it is followed (a query or fragment after the file name, as in
`logo.png?v=2` or `part.scad#view`, is ignored); an absolute path, a path that leaves the
folder, a
symbolic link, and a network URL (`http:`, `https:`) are never read or fetched. A link that is not drawn is a warning with
the code `svg-image-unresolved` in `Diagnostics` and draws nothing; so is a picture that would take more pixels than the
render may use (`FolderImageResolverOptions.MaxDecodedPixels`, 64 million by default, counted before anything is
decoded, so a huge raster in a tiny placement is refused and not decoded), with the code `svg-image-too-large`. A file
that is found but is broken throws
`ImageSourceException`, which names the link and the embedding file and keeps the original exception as its
`InnerException`. Models and charts are drawn at the size of their placement over a transparent background (a chart keeps
its own background); a raster keeps its natural size and the SVG codec resamples it. See the core user guide, *Linked
Images*, for every rule.

**Create a new resolver for every render.** A resolver counts the images of one render and collects its notes, and it is
not thread-safe; creating one is cheap. It never caches a picture, so a live source is asked again every time.

#### Rendering any file

`Render` draws one file as a picture, with the same sources, the same folder sandbox, and the same limits as embedding:

```csharp
using var image = CanvasNetViewer.Render("models/part.scad", new ViewerRenderOptions { Width = 512 });

Console.WriteLine($"{image.Format}: {image.Surface.Width}x{image.Surface.Height}");   // scad: 512x512
PngCodec.Save(image.Surface, "part.png");
```

`ViewerRenderOptions` has `Width` and `Height` (none: natural size; one: keep the aspect ratio; both: a box - a vector
file is drawn at that size and a raster is fitted into it; when given, each must be from 1 to 8192, otherwise `Render`
throws `ArgumentOutOfRangeException` before it reads anything), `Folder` (the sandbox for the files the rendered file itself
includes or embeds; by default the folder of the file, or none for a stream), `ThrowOnUnsupportedFeature` (an unsupported
feature fails instead of being drawn without it and reported), `MaxDecodedPixels` (the most pixels the render may decode
in total; null keeps the resolver default of 64,000,000, which refuses a single 8192 x 8192 picture, so set it to render
one larger image; when a raster is resized to the requested size, the final picture is a second buffer that counts
against the same budget, so a 10 x 10 picture resized to 1 x 1 needs 101 pixels, and one that does not fit throws
`ImageSourceBudgetException`), `ExtraSources` (see below), and `CancellationToken`. The
`ViewerImage` owns the `Surface` (disposing it disposes the surface) and carries `Format`, `Diagnostics`, `Echo` (the
text a model printed with `echo`), and `Embedded`. `Render` throws `FileNotFoundException` for a missing file,
`NotSupportedException` when no source claims the extension (the message lists them), `SandboxException` when the sandbox
refuses the file, and `ImageSourceException` for a file that cannot be drawn. Render a file that is not on disk with
`Render(stream, fileName)`; the extension of the file name chooses the source.

#### Doing what the canvasnet commands do, from code

Everything the `canvasnet` command-line tool does with images is a call of the `Imaging` namespace of the Viewer package
(`DemaConsulting.CanvasNet.Viewer.Imaging`); the tool only parses a command line into the same requests and prints the
results. Each operation takes plain request values and returns plain result values, and every expected failure is a
`ViewerException` (see *Handling errors* below). The paths of the operations are file paths, as on the command line.

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasNet.Viewer.Imaging;
```

**`formats` - what can be read and written.** The input kinds by extension, and the output formats by extension:

```csharp
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

```csharp
var info = SourceLoader.Probe("deck.pptx");
Console.WriteLine($"{info.Format}: {info.PageCount} page(s), page 1 is {info.Width}x{info.Height} pixels");
foreach (var page in info.Pages)
{
    Console.WriteLine($"  page {page.Number}: {page.Width}x{page.Height}");
}
```

**`render`, `convert`, and the verbs `crop`, `resize`, `rotate`, `flip`, `pad` - load, transform, write.** An
`ImageRenderer` call loads the page (or every page) of a probed source, runs an ordered pipeline of operations over it,
and writes one file for each page. The operations are records, applied in the order of the list, and the output format is
chosen by the extension of the output (or by `WriteOptions`). All the files are written together or none:

```csharp
var info = SourceLoader.Probe("part.scad");
var result = ImageRenderer.Execute(
    info,
    new RenderRequest(
        Output: "out/part.png",
        AllPages: false,
        Source: new SourceOptions { Width = 800, Scad = new ScadViewOptions { View = "front", ViewGiven = true } },
        Write: WriteOptions.Default,
        Operations:
        [
            new CropOperation(new GeometrySpec(
                new Dimension(10, false), new Dimension(10, false), new Dimension(80, true), new Dimension(80, true))),
            new RotateOperation(90),
            new GrayscaleOperation(),
            new GridOperation(50, null)
        ]));

foreach (var page in result.Pages)
{
    Console.WriteLine($"wrote {page.Result.Path} ({page.Result.Width}x{page.Result.Height})");
    foreach (var step in page.Operations)                 // what was done, in order, with its parameters
    {
        Console.WriteLine($"  {step.Op}");
    }
}
```

The pipeline operations are `CropOperation`, `ResizeOperation`, `FitOperation`, `RotateOperation` (any angle),
`FlipOperation`, `PadOperation`, `GrayscaleOperation`, `InvertOperation`, `ThresholdOperation`, `LevelsOperation`,
`BrightnessOperation`, `ContrastOperation`, `AutoLevelsOperation`, `DeskewOperation`, `TrimOperation`, and
`GridOperation`; `SourceOptions` holds what the command-line options of a source hold (`Page`, `Dpi`, `Scale`, `Width`,
`Height`, `Background`, `Strict`, `AutoOrient`, `Filter`, and `Scad` for a model's view and variables). With `AllPages`
every page of a document is written, to a folder (an output that ends with a directory separator) or to a name that
contains `{page}`. To transform a surface you already have, call `ImagePipeline.Apply(surface, operations,
defaultPadColor, out var applied)`, and to write one, `ImageWriter.Write(surface, path, inputPathForGuard, options)`;
`SourceLoader.Load(path, options)` gives the pixels of one page as a `LoadedSource` that owns a `Surface`.

**`inspect colors` and `inspect pixel` - numbers instead of a picture.**

```csharp
var info = SourceLoader.Probe("photo.jpg");

var colors = ImageInspector.Execute(
    info,
    new InspectRequest(new SourceOptions(), [new TrimOperation(8)], InspectKind.Colors, Count: 5, Radius: 0, Point: null));
foreach (var color in colors.Colors!.Colors)              // the dominant colors, largest first
{
    Console.WriteLine($"{color.Color} {color.Fraction:P1}");
}

var pixel = ImageInspector.Execute(
    info,
    new InspectRequest(new SourceOptions(), [], InspectKind.Pixel, Count: 0, Radius: 1, Point: new PixelPoint(10, 20)));
Console.WriteLine($"{pixel.Pixel!.Pixel} luma {pixel.Pixel.Luma}");
```

**`compare` - how different are two images?** Two inputs are brought to one size (`Resize`), measured, checked against
thresholds, and, if asked, written as a difference picture, a side-by-side picture, or an overlay. A threshold that is
not met is **data** in the result (`Passed` is false), not an exception; you decide what a miss means:

```csharp
var request = new CompareRequest(
    A: new CompareInput("a", SourceLoader.Probe("reference.png"), 1, new SourceOptions()),
    B: new CompareInput("b", SourceLoader.Probe("redraw.svg"), 1, new SourceOptions()),
    Resize: new ResizeTarget("a", 0, 0),                  // load b at the size of a; null compares at the own sizes
    Fit: FitMode.Contain,
    Filter: ResamplingMode.Auto,
    Background: new Rgba32(255, 255, 255, 255),
    Tolerance: ImageComparison.DefaultTolerance,
    GridColumns: ImageComparison.DefaultGridColumns,
    GridRows: ImageComparison.DefaultGridRows,
    Thresholds: [new ComparisonThreshold(ThresholdMetric.Rmse, 10), new ComparisonThreshold(ThresholdMetric.Ssim, 0.95)],
    DiffPath: "diff.png",
    SideBySidePath: null,
    OverlayPath: null,
    DiffStyle: DiffStyle.Heat,
    Gain: ImageComparison.DefaultGain,
    OverlayWeight: ImageComparison.DefaultOverlayWeight,
    Write: WriteOptions.Default);

var comparison = ImageComparer.Execute(request);
Console.WriteLine($"RMSE {comparison.Metrics.Metrics.Rmse:F2}, SSIM {comparison.Metrics.Metrics.Ssim:F4}");
foreach (var check in comparison.Checks)
{
    Console.WriteLine($"{ImageComparison.MetricName(check.Metric)}: {(check.Passed ? "passed" : "failed")} ({check.Value})");
}
```

**`montage` - a contact sheet.** The sources are opened one at a time, so the memory is the sheet plus one source:

```csharp
var infos = Directory.GetFiles("assets").Select(SourceLoader.Probe).ToList();   // images, and documents (first page)
var sheet = new SheetOptions(
    Columns: 0, CellWidth: 200, CellHeight: 150, Spacing: 8, Padding: 8,
    Background: new Rgba32(255, 255, 255, 255), Labels: true, Fit: FitMode.Contain, Filter: ResamplingMode.Auto);

var montage = MontageBuilder.Execute(
    infos, new MontageRequest("sheet.png", Page: null, AllPages: false, Dpi: null, Scad: null, Strict: false,
        AutoOrient: true, Sheet: sheet, Write: WriteOptions.Default));
Console.WriteLine($"{montage.Layout.Width}x{montage.Layout.Height}, {montage.Cells.Count} cells");
```

**Handling errors.** An expected failure (a missing, unsupported, or corrupt file, a page that does not exist, a size
beyond 8192 pixels, an output that cannot be written, inputs that cannot be compared) is a `ViewerException` with a
stable `Code` (one of the strings of `ErrorCodes`, the same ones the tool prints), a message, and a hint. Anything else
is a defect and is not caught:

```csharp
try
{
    ImageRenderer.Execute(SourceLoader.Probe(path), request);
}
catch (ViewerException ex) when (ex.Code == ErrorCodes.PageOutOfRange)
{
    Console.Error.WriteLine($"{ex.Message} {ex.Hint}");
}
```

A note about the two ways to draw a file: `CanvasNetViewer.Render` (above) is the simple way to get a picture of a file
(transparent background for vector files, the fit of a raster into a box, the image-source sandbox, and its own
exceptions), and the `Imaging` operations are the way the `canvasnet` tool works (document pages and resolution, EXIF
orientation, a flattened background, the ordered pipeline, and the `ViewerException` codes). The messages and hints of the
operations are written for the command line and name the options of the tool (for example `--all-pages`).

#### Using only some formats, without the Viewer

If you need few formats, skip the Viewer package and compose a resolver from the sources you reference, one line:

```csharp
var resolver = new FolderImageResolver(folder, [new RasterImageSource(), new SvgImageSource(), new ScadImageSource()]);
```

#### Adding your own image source

The Viewer is for library users: a program that uses the library can add file types, override how a built-in type is
drawn, or draw a live picture. The command-line tool has no such plug-in mechanism. A source implements `IImageSource`
(see the core user guide for the whole contract) and claims **file name extensions**, **URI schemes**, or both.

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Viewer;

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

// Pass the sources for one render; <image href="grid.dots"/> and <image href="camera://front"/> now draw.
var resolver = CanvasNetViewer.CreateImageResolver(folder, [new DotsSource(), new CameraSource(myCamera)]);
var options = new SvgLoadOptions { ImageResolver = resolver };
using var picture = SvgCodec.Load(Path.Combine(folder, "dashboard.svg"), 800, 600, null, options);

// The same sources work for Render of a single file
using var dots = CanvasNetViewer.Render("grid.dots", new ViewerRenderOptions { ExtraSources = [new DotsSource()] });
```

- **The later source wins.** Extra sources are placed after the built-in ones. When two sources claim the same extension
  or scheme, the later one takes precedence, so a source that claims `.png` replaces the built-in raster source, and a
  second source that claims `camera` replaces the first. Claims are validated up front (an extension is a dot and a
  name, a scheme has two or more characters, and `file` and `data` can never be claimed).
- **Nothing is cached.** The source is called again for every link, and the surface it returns is owned and disposed
  by the
  caller, so return a new picture on every call. Create a new resolver for every render; a live source gives a fresh
  frame each time.
- **`http` and `https` stay unresolved.** No built-in source claims them, so such a link is only the
  `svg-image-unresolved` warning. A source that claims them takes full responsibility for what it fetches.
- **Security.** A file-based source receives bytes the resolver has already confined to the folder. A **scheme source
  owns its own access control**: it receives the text of the link as written and decides what that may reach. Validate
  it (a list of allowed cameras, hosts, or paths), never build a file path or a URL from it unchecked, and bound the work
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

## Examples

### Example 1: A Dashboard With a Model, a Chart, and a Picture

```csharp
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

```csharp
foreach (var path in Directory.EnumerateFiles("assets")
             .Where(p => CanvasNetViewer.Extensions.Any(e => p.EndsWith(e, StringComparison.OrdinalIgnoreCase))))
{
    using var thumbnail = CanvasNetViewer.Render(path, new ViewerRenderOptions { Width = 128, Height = 128 });
    PngCodec.Save(thumbnail.Surface, Path.ChangeExtension(path, ".thumb.png"));
}
```

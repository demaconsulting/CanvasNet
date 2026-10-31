# CanvasNet PPTX Package

## API Reference

### PptxDocument

`PptxDocument` is distributed via the separate `DemaConsulting.CanvasNet.Pptx` NuGet package
(namespace `DemaConsulting.CanvasNet.Pptx`), which references the core `DemaConsulting.CanvasNet`
package - see the Installation section of the project README.

The `PptxDocument` sealed class opens a PowerPoint (`.pptx`) presentation - an OPC (Open Packaging
Conventions) ZIP package - and rasterizes individual slides onto a `Surface`. Opening buffers the
whole package in memory (up to 256 MiB), so the caller's stream need not stay open. A slide is
rendered by walking its shape tree in document order, including nested groups, and painting:
auto shapes (preset and custom geometry) with solid, linear-gradient and covered pattern fills and
outlines; connectors (straight, elbow and curved, with arrowheads); raster pictures (BMP, PNG, TIFF,
JPEG and GIF, with `srcRect` cropping) and SVG-only picture blips; tables; embedded charts; and text
with placeholder, layout, master and theme inheritance, word wrap, alignment, vertical anchoring,
autofit, bullets and numbering. The slide's own background fill (falling back to its layout's, then
its master's) is painted before the shapes. Footer, date and slide-number placeholders inherit from
the master like any other placeholder, and a master's non-placeholder date or slide-number text box
honors its own list style, so its size is not replaced by the 18 pt default. `PptxDocument` is not
thread-safe: open one instance per thread, or synchronize access externally.

```csharp
using DemaConsulting.CanvasNet.Pptx;

using var deck = PptxDocument.Open("slides.pptx");
Console.WriteLine($"{deck.SlideCount} slides, {deck.SlideSize.WidthEmu}x{deck.SlideSize.HeightEmu} EMU");

// Render the first slide at 96 DPI (the surface size follows the slide's aspect ratio).
using var slide = deck.Render(0, 96f);
var (width, height) = deck.GetSlideSizeInPixels(96f);
Console.WriteLine($"{width}x{height}"); // Output: the pixel size of the surface returned above
```

#### PptxDocument Members

- `PptxDocument.Open(Stream stream)` / `PptxDocument.Open(string path)`: Opens and parses a
  presentation. Throws `ArgumentNullException` for a null argument, `ArgumentException` for an empty
  path, and `InvalidDataException` when the package is not a readable ZIP archive, is missing a
  required part, is malformed, or is not a navigable presentation (a slide size and at least one slide
  are required).
- `PptxDocument.SlideCount`: The number of slides declared by the presentation.
- `PptxDocument.SlideSize`: The slide size as a `PptxSlideSize` (`WidthEmu` and `HeightEmu`, in EMU,
  English Metric Units; 914,400 EMU per inch).
- `PptxDocument.GetSlideSizeInPixels(float dpi)`: The slide size in pixels at `dpi`, each dimension
  rounded to the nearest pixel.
- `PptxDocument.Render(int slideIndex, int width, int height, PptxRenderOptions? options = null)`:
  Renders the slide into a new `width` x `height` `Surface`.
- `PptxDocument.Render(int slideIndex, float dpi, PptxRenderOptions? options = null)`: Renders the
  slide at `dpi`, preserving the slide's aspect ratio.
- `PptxDocument.Dispose()`: Releases the document; idempotent. Every other member then throws
  `ObjectDisposedException`.

`Render` throws `ArgumentOutOfRangeException` for an invalid slide index, `dpi` or surface size,
and `InvalidDataException` for malformed slide content. A shape, picture or graphic frame that
declares no position anywhere in its ancestry is skipped silently.

#### PptxRenderOptions

`PptxRenderOptions` is a sealed class with `init`-only properties (`PptxRenderOptions.Default` holds
the defaults):

- `BackgroundColor`: The color the surface is cleared to first (opaque white by default). A slide,
  layout or master `<p:bg>` fill, when declared, is painted over it.
- `StrictUnsupportedPictures`: See the EMF/WMF section below.
- `OnUnsupportedPictureSkipped`: See the EMF/WMF section below.

A well-formed but unsupported construct (for example a radial or path gradient, an unsupported
preset geometry or pattern-fill preset, a linked non-embedded image, or an unsupported raster image
format) makes `Render` throw `PptxUnsupportedFeatureException`, an `IOException` whose `Feature`
property carries a stable token such as `pptx-image-link`, `pptx-image-format`, `pptx-pattern-fill` or
`pptx-gradient-path`. A chart that cannot be rendered is reported as `pptx-chart-` followed by the
chart feature token.

### PPTX EMF/WMF Pictures

`PptxDocument.Render` cannot draw EMF/WMF vector metafile pictures (a known gap). By default, a
slide's `<p:pic>` whose media is an EMF or WMF is skipped - nothing is drawn for that picture and
the rest of the slide renders normally. To learn which pictures were skipped, set
`PptxRenderOptions.OnUnsupportedPictureSkipped` (it reports only the slide's own or group-nested
pictures; EMF/WMF pictures on a layout or master are skipped silently in both modes); to make
`Render` fail instead, set `PptxRenderOptions.StrictUnsupportedPictures = true`, which makes it throw
`PptxUnsupportedFeatureException` (`Feature` is `"pptx-image-metafile"`).

```csharp
using DemaConsulting.CanvasNet.Pptx;

using var document = PptxDocument.Open("slides.pptx");
var options = new PptxRenderOptions
{
    OnUnsupportedPictureSkipped = ex => Console.WriteLine($"Skipped picture: {ex.Message}"),
};
using var surface = document.Render(0, 96f, options);
```

## Examples

### Example 18: Rendering a PowerPoint (.pptx) Slide

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Pptx;

using var deck = PptxDocument.Open("slides.pptx");

// Render the first slide at 96 DPI, reporting any EMF/WMF pictures that had to be skipped.
var options = new PptxRenderOptions
{
    OnUnsupportedPictureSkipped = ex => Console.WriteLine($"Skipped picture: {ex.Message}"),
};
using var slide = deck.Render(0, 96f, options);
PngCodec.Save(slide, "slide1.png");
```

### Example 41: Drawing a Slide in a Pandoc Document

The Pandoc filter `docs/filters/canvasnet.lua` draws one slide of a `.pptx` file; `page` counts from 1 and `dpi`
sets the resolution. The slide below is the original `docs/images/showcase/slide.pptx` of this repository.

```markdown
![The showcase slide](slide.pptx){page=1 dpi=80 width=60%}
```

![The showcase slide](slide.pptx){page=1 dpi=80 width=60%}

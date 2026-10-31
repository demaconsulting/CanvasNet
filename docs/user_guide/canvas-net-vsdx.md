# CanvasNet VSDX Package

## API Reference

### VsdxDocument

`VsdxDocument` is distributed via the separate `DemaConsulting.CanvasNet.Vsdx` NuGet package
(namespace `DemaConsulting.CanvasNet.Vsdx`), which references the core `DemaConsulting.CanvasNet`
package - see the Installation section of the project README.

The `VsdxDocument` sealed class opens a Microsoft Visio (`.vsdx`) diagram document - an OPC (Open
Packaging Conventions) ZIP package - resolving `[Content_Types].xml` and its relationship graph to
locate `visio/document.xml` and `visio/pages/pages.xml`, and reports each page's declared name and
size and the document's page count. `Render` resolves the requested page's full shape tree (each
shape's effective cells, geometry, and style, merged with its referenced Master/MasterShape and
StyleSheet chain exactly as a native Visio client would) and walks it in document order (the first
declared shape paints first/bottom), painting each shape's resolved fill, stroke, text, and - for
a 1-D connector - its line and arrowheads onto the returned `Surface`. Master/MasterShape cell and
geometry-row inheritance, StyleSheet-chain-resolved line/fill/text style (including the built-in
color palette, resolved fill/line transparency, and bare `THEMEVAL()` themed color/font cells, which
resolve to the built-in "No Style" StyleSheet's values and the theme's minor Latin typeface), connector/
glue-point routing, arrowhead rendering, and arbitrarily nested shape groups are all resolved
automatically - no separate API is needed to opt into any of them. A `NonPrinting` shape skips its
own fill/stroke/text/arrowhead paint (but still recurses into its own children); a `HideText` shape
skips only its own text paint; and a resolved sub-pixel `LineWeight` is floored to a minimum
visible stroke width rather than vanishing entirely, mirroring Visio's own "always draw a
perceptible line" rendering convention. Constructs the renderer recognizes but cannot (fully)
render are never skipped silently: set `VsdxRenderOptions.UnsupportedFeatureHandler` to receive a
`VsdxUnsupportedFeature` (stable `Feature` token plus message) once per distinct feature per
`Render` call, or set `ThrowOnUnsupportedFeature` to get a `VsdxUnsupportedFeatureException`
instead (rendering is tolerant by default). **Documented scope boundaries**: embedded images/foreign
shapes, theme Quick Style variation matrices (a themed color that cannot be resolved is drawn neutral
gray and reported as `vsdx-themed-color`), non-solid fill-pattern combinations beyond solid
(including any gradient approximation), the full arrowhead style-index table, and a handful of
rarely-encountered geometry row types (`InfiniteLine`) are tolerated (the row/feature is skipped
and reported, never thrown) but not resolved into their own path segments or paint.
`PolylineTo`, `NURBSTo` and `SplineStart`/`SplineKnot` rows are drawn as flattened line segments
(bounded by hard limits on control points, degree and segments; a curve that cannot be evaluated
exactly is approximated by a polyline and reported as `vsdx-geometry-spline-approximated`). A
malformed geometry row is skipped, never thrown, and reported with the token
`vsdx-geometry-row-<Type>` (for example `vsdx-geometry-row-NURBSTo`): a `PolylineTo`, `NURBSTo` or
`SplineStart`/`SplineKnot` row with a missing, malformed, non-finite or over-limit cell or formula;
an orphan `SplineStart`/`SplineKnot`; a row that continues a subpath before any `MoveTo`; or an
unrecognized row type such as `InfiniteLine`. See `canvas-net-vsdx.md`'s Design Constraints
section for the complete deferred-feature boundary.

```csharp
using var doc = VsdxDocument.Open("diagram.vsdx");
var info = doc.GetPageSize(0);
using var surface = doc.Render(0, 150); // Render the first page at 150 DPI.

// Receive a diagnostic for every construct that was recognized but could not be fully rendered.
var options = new VsdxRenderOptions
{
    UnsupportedFeatureHandler = f => Console.WriteLine($"{f.Feature}: {f.Message}"),
};
using var reported = doc.Render(0, 150, options);
```

#### VsdxDocument Methods

##### VsdxDocument.Open(Stream stream)

```csharp
public static VsdxDocument Open(Stream stream)
```

Reads the entirety of an open, readable stream into an in-memory buffer and parses it into a new
`VsdxDocument`. Does not take ownership of, and does not dispose, the caller's `stream`.

**Exceptions:**

- `ArgumentNullException`: Thrown when `stream` is null.
- `InvalidDataException`: Thrown when the stream does not contain a valid, supported `.vsdx`
  package (not a readable ZIP archive, missing a required part, or a malformed part), or when
  `stream` supplies more than the package layer's maximum supported package size.

##### VsdxDocument.Open(string path)

```csharp
public static VsdxDocument Open(string path)
```

Opens its own internal `FileStream` for the file at `path`, reads it fully, closes that stream,
then parses the buffered content identically to the `Stream` overload above.

**Exceptions:**

- `ArgumentNullException`: Thrown when `path` is null.
- `ArgumentException`: Thrown when `path` is empty or consists only of white space.
- `InvalidDataException`: Thrown for the same conditions as `Open(Stream)`.

##### VsdxDocument.PageCount

```csharp
public int PageCount { get; }
```

The total number of pages declared by `visio/pages/pages.xml`'s page index.

**Exceptions:**

- `ObjectDisposedException`: Thrown when accessed after `Dispose()` has been called.

##### VsdxDocument.GetPageSize(int pageIndex)

```csharp
public VsdxPageInfo GetPageSize(int pageIndex)
```

Returns the requested page's declared name and size (converted from inches to EMU, English Metric
Units) as a `VsdxPageInfo`.

**Exceptions:**

- `ObjectDisposedException`: Thrown when this document has been disposed.
- `ArgumentOutOfRangeException`: Thrown when `pageIndex` is outside `[0, PageCount)`.

##### VsdxDocument.Render(int pageIndex, int width, int height, VsdxRenderOptions? options = null)

```csharp
public Surface Render(int pageIndex, int width, int height, VsdxRenderOptions? options = null)
```

Returns a new `Surface` of the caller-specified `width` x `height` for the given page, cleared to
`options.BackgroundColor` (opaque white by default, when `options` is `null`) and then painted
with the page's resolved shape tree (see *VsdxDocument* above). The `width`/`height` used is
exactly as given - it is not clamped or derived from the page's own declared size.

**Exceptions:**

- `ObjectDisposedException`: Thrown when this document has been disposed.
- `ArgumentOutOfRangeException`: Thrown when `pageIndex` is outside `[0, PageCount)`, or when
  `width`/`height` is not a valid `Surface` size.
- `InvalidDataException`: Thrown when the page's shape tree cannot be resolved (a malformed
  content part, or a group-nesting depth/resolved-shape-count budget exceeded).

##### VsdxDocument.Render(int pageIndex, int dpi, VsdxRenderOptions? options = null)

```csharp
public Surface Render(int pageIndex, int dpi, VsdxRenderOptions? options = null)
```

Convenience overload of `Render(int, int, int, VsdxRenderOptions?)` for the common "render at a
given resolution" case: reads `GetPageSize(pageIndex)`'s declared width/height (in inches), scales
both by `dpi`, rounds to the nearest pixel, and renders at that size - preserving the page's own
aspect ratio, unlike the four-argument overload. Deliberately accepts `dpi` as an `int`, not a
`float` - a documented deviation from `PptxDocument`'s own `float dpi` overload.

**Exceptions:**

- `ObjectDisposedException`: Thrown when this document has been disposed.
- `ArgumentOutOfRangeException`: Thrown when `dpi` is not greater than zero, when `pageIndex` is
  outside `[0, PageCount)`, or when the computed pixel width/height falls outside `Surface`'s own
  valid dimension range.
- `InvalidDataException`: Thrown for the same conditions as the pixel-dimension overload above.

##### VsdxDocument.Dispose()

```csharp
public void Dispose()
```

Releases the document's underlying ZIP archive and in-memory buffer. Idempotent - safe to call
more than once. Every other public member throws `ObjectDisposedException` once called.

## Examples

### Example 17: Opening and Rendering a Visio (.vsdx) Diagram

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Vsdx;

using var doc = VsdxDocument.Open("diagram.vsdx");
Console.WriteLine(doc.PageCount); // Output: the number of pages in the document

// Each page reports its own declared name and size (converted to EMU, English Metric Units).
var pageInfo = doc.GetPageSize(0);
Console.WriteLine(pageInfo.Name);

// Render the first page at 150 DPI - Master/StyleSheet inheritance, connectors/arrowheads, and
// nested groups are all resolved automatically.
using var surface = doc.Render(0, 150);
Console.WriteLine($"{surface.Width}x{surface.Height}");
```

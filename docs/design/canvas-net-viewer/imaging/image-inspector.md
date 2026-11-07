### ImageInspector

<!-- cspell:ignore canvasnet luma -->

![Imaging Structure](ImagingView.svg)

#### Purpose

`ImageInspector` is the inspect operation of the library: it reports what an image contains - its dominant colors and
luma statistics, or the pixel at a point with its surroundings - after the image pipeline, without writing a file. Its
single responsibility is the order of load, transform, and analyze; the pixels are analyzed by the core `Surface`. It is
the library form of the `inspect` command of the `canvasnet` tool. The unit is the static class `ImageInspector`
(`ImageInspector.cs`) with the request and result records, `InspectKind`, and `PixelPoint`.

#### Data Model

**InspectKind** (enum): `Colors` or `Pixel`. **PixelPoint** (record struct): `X` and `Y`, from the top left.

**InspectRequest** (record): the required `Kind` (positional) and the `init` properties `Source` (the `SourceOptions`;
default), `Operations` (applied before the analysis; none), `Count` (the number of dominant colors; the constant
`ImageInspector.DefaultColors`, 8), `Radius` (the window around a pixel; 0), and `Point` (required for a pixel). The
factories `InspectRequest.ForColors(count)` and `ForPixel(point, radius)` build the two kinds, so that a pixel request
cannot be built without its point by mistake.

**InspectResult** (record): `Info`, `Page`, `Operations`, `Diagnostics`, `Echo`, `Width` and `Height` (of the analyzed
pixels), `Background` (the opaque color a translucent background was reduced to; white when none), `Colors`
(`SurfaceStatistics?`) and `Pixel` (`PixelSample?`), of which the one the kind asks for is set.

#### Key Methods

**Execute(info, request)**: refuses a request with a missing member (`Source`, `Operations`, or a null operation) as
`ArgumentNullException`, an operation with an invalid value as a `usage-error` or `size-limit`
(`ImagePipeline.Validate`, before anything is decoded), and a pixel request without a point as a defect of the caller,
checks the page, loads the
page with `SourceLoader.Load`, applies the operations with `ImagePipeline.Apply` (the default color of a border as in
`ImageRenderer`), and then analyzes the result: `Surface.Analyze` over opaque white for the colors (the loader and the
pipeline have already composited the picture over the background, so what is still translucent is analyzed over white
and no pixel is composited twice), or `Surface.Sample` at the point. A point outside the image after the operations is a
`usage-error` that names the image size. The reported background is the background that was given, opaque, or a
translucent one flattened over white; white when none was given.

**Input fragment.** The page of a `SourceInfo.Selection` replaces `request.Source.Page` (checked before the load), and
`SourceLoader.Load` applies the rest of the selection (variables, view, chart parameters) to the options.

#### Error Handling

`ViewerException` with `page-out-of-range`, `usage-error` (the point outside the image, an operation value out of range,
an operation that does not fit), `size-limit` (an operation size above the maximum),
and the codes of `SourceLoader` and `ImagePipeline`. A null argument, a request with a missing member, or a pixel
request without a point is a defect
(`ArgumentNullException`, `ArgumentException`). Any other exception propagates. The intermediate surface of the
pipeline is disposed on every path.

#### Dependencies

- **SourceLoader** - `CheckPage` and `Load`.
- **ImagePipeline** - `Apply`.
- **ViewerException** and **ErrorCodes** - the expected-error contract.
- **CanvasNet core library** - `Surface.Analyze`, `Surface.Sample`, `SurfaceStatistics`, `PixelSample`, and `Rgba32`.

#### Callers

- **InspectCommand** (of the tool) - for `inspect colors` and `inspect pixel`.
- **Library users** - read colors and pixels of any supported file.

### RasterImageSource

![ImageSources Structure](ImageSourcesView.svg)

The `RasterImageSource` unit is the source for the raster pictures the core library decodes: PNG, JPEG, BMP, TIFF, GIF,
and
JPEG 2000. It is one `public` sealed class, `RasterImageSource`, in `RasterImageSource.cs` in the namespace
`DemaConsulting.CanvasNet.ImageSources`.

#### Purpose

Rasters are the most common linked pictures, and the core package already has every decoder, so the source belongs in
the
core package: it needs no other package and makes the resolver useful on its own. It also shows the smallest possible
implementation of the contract.

The unit's behavior is specified by the `CanvasNet-ImageSources-RasterImageSource-*` requirements (`Formats`,
`Orientation`, `Budget`, and `Failures`) in `docs/reqstream/canvas-net/image-sources/raster-image-source.yaml`.

#### Data Model

The class has no state. `Name` is `raster`; `Extensions` are `.png .jpg .jpeg .bmp .tif .tiff .gif .jp2 .j2k .j2c`;
`Schemes` is empty. The class is stateless and thread-safe, so one instance serves any number of resolvers.

#### Key Methods

**`Render(request)`**: check the token and that the request has content; detect the format from the first bytes with
`ImageFormats.Detect` (the name of the file is not used, so a mislabeled file loads); **read the declared size from the
header and check it against the budget of the request with `EnsureWithinBudget` before decoding anything**; decode with
`ImageDecoder.Load(stream, format)`; for a JPEG read the Exif orientation from the same bytes and, when it is greater
than 1,
replace the surface with `Surface.ApplyExifOrientation` (disposing the first); return the surface at its natural size
with
`Format` set to the lower-case name of the detected format. The size of the request is deliberately ignored: a raster
has no
sharper rendering at another size, and the renderer that draws it resamples once.

**`ReadDeclaredSize`** (private): the header probe of each codec, `PngCodec.GetInfo`, `JpegCodec.GetInfo`,
`BmpCodec.GetInfo`, `TiffCodec.GetInfo`, and `Jpeg2000Codec.GetInfo`, read only the header and return the declared
size even above `Surface.MaxDimension` (so a file that declares more than the codecs decode is refused by the budget,
not allocated). `GifCodec.GetInfo` is not used because it also decodes the first frame (to report whether it can be
decoded), which would allocate the picture the check is meant to guard; for GIF the width and height of the Logical
Screen Descriptor (bytes 6 to 9, little-endian), which is the size `GifCodec.Load` allocates, are read directly. No
public API was added to the codecs for this.

#### Error Handling

Content that is not a recognized raster, or that the codec rejects, is `InvalidDataException` (the exception of the
codec,
unchanged, which `FolderImageResolver` wraps in an `ImageSourceException`). A picture whose declared size exceeds the
remaining pixels is `ImageSourceBudgetException`, thrown before any decoding (the resolver turns it into the warning
`svg-image-too-large`). A request without content is
`InvalidOperationException`, a null request `ArgumentNullException`, and a canceled token `OperationCanceledException`.

#### Dependencies

`ImageFormats`, `ImageDecoder`, `ImageFormat`, the `GetInfo` probes of the codecs, and `JpegCodec.ReadExifOrientation`
of the `Codecs` subsystem, and `Surface` (`ApplyExifOrientation`) of the `Canvas` subsystem; the contract types of the
`ImageSourceContract` unit.

#### Callers

`FolderImageResolver` (through `IImageSource`), the `CanvasNetViewer` system (in its list of sources), and applications.

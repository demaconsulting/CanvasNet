<!-- cspell:ignore embedder -->

### ImageSourceContract

![ImageSources Structure](ImageSourcesView.svg)

The `ImageSourceContract` unit is the contract of the `ImageSources` subsystem: the public types through which a format
or a live source plugs into a resolver, in the namespace `DemaConsulting.CanvasNet.ImageSources`. It comprises these
source
files, all `public`:

- `IImageSource.cs` - `IImageSource` (interface): a source and what it claims
- `ImageSourceRequest.cs` - `ImageSourceRequest` (sealed class): what a source is asked to draw
- `ImageSourceResult.cs` - `ImageSourceResult` (sealed record): what it drew
- `ImageSourceDiagnostic.cs` and `ImageDiagnosticSeverity.cs` - `ImageSourceDiagnostic` (sealed record) and the severity
  enum
- `IImageResolver.cs` - `IImageResolver` (interface): what a renderer calls
- `EmbeddedImage.cs` - `EmbeddedImage` (sealed record): one loaded image
- `ImageSourceException.cs` - `ImageSourceException` (sealed class): the failure of a source
- `ImageSourceBudgetException.cs` - `ImageSourceBudgetException` (sealed class): the refusal of a source for a picture
  that does not fit in the pixel budget
- `ImageSourceSet.cs` - `ImageSourceSet` (sealed class): the ordered, validated list of sources
- `NamespaceDoc.cs` - documentation of the namespace

#### Purpose

The unit makes the plug-in point complete and public, so an application can implement a source for its own format or
its own live source without any internal type: every member of the contract is documented with its semantics (size,
diagnostics, cancellation, budgets, ownership of the surface, and how nested links are resolved). `ImageSourceSet`
exists
so that the claims of every source are validated once, where the list is composed, and so that the precedence rule (the
later source wins) is implemented in one place for every consumer.

The unit's behavior is specified by the `CanvasNet-ImageSources-ImageSourceContract-*` requirements (`SourceClaims`,
`Request`, `SizeRule`, `Result`, and `FailureReporting`) in
`docs/reqstream/canvas-net/image-sources/image-source-contract.yaml`.

#### Data Model

**`IImageSource`**: `Name` (stable lower-case format name), `Extensions` (lower case, with the dot; compound allowed;
the
longest match wins), `Schemes` (lower case, without the colon; never `file` or `data`), and
`ImageSourceResult Render(ImageSourceRequest)`. A source that claims nothing of one kind returns an empty list.

**`ImageSourceRequest`** (immutable): `Href` (the raw link; always set), `Content` (the bytes of a file), `FileName`
(the
path of the file relative to the folder, with forward slashes), `Directory` (the full path of the folder of the file),
`PixelWidth` and `PixelHeight` (requested size in device pixels; `null` means the natural size), `Resolver` (an
`IImageResolver` scoped to the request), `ThrowOnUnsupportedFeature`, `CancellationToken`, `ReservedPixels` (the pixels
the resolver has already charged for the request: the requested size, zero for a natural-size render; default 0), and
`RemainingPixels` (the budget still free after that charge, a snapshot; `long.MaxValue` for a request built by hand).
`HasContent` is true exactly when `FileName` is not null: a file-based request has content, a scheme-claimed request
has only `Href`.

**`ImageSourceResult`** (immutable): `Surface` (required; owned by the receiver), `Diagnostics`, `Echo`, and `Format`.
**`ImageSourceDiagnostic`**: `Source`, `Severity` (`Info`, `Warning`, `Unsupported`), `Code`, and `Message`.
**`EmbeddedImage`**: `Href`, `Format`, `Width`, `Height`, and `Depth` (0 for an image of the top-level document).
**`ImageSourceException`**: `Href`, `Embedder` (null for the top-level document), and the original failure as the inner
exception. **`ImageSourceBudgetException`**: `RequestedPixels` and `RemainingPixels`; it is how a source refuses, and
it is not a failure of the file.

**`ImageSourceSet`**: the sources in order, a dictionary from extension to the winning source, a dictionary from scheme
to
the winning source, the extensions longest first, and the sorted lists `Extensions` and `Schemes`, which are
`ReadOnlyCollection<string>` instances (not arrays or lists, so a cast cannot change them). It is immutable.

#### Key Methods

**`ImageSourceSet(IEnumerable<IImageSource>)`**: copies the sequence; for each source checks the name and that the lists
are not null; normalizes each extension (a dot, a name, no `/`, `\`, `:`, or white space; lower case) and each scheme
(`GetScheme(scheme + ":")` must give the same scheme, so claims and links follow one definition; not `file` or `data`);
overwrites earlier claims with later ones. Postcondition: every claim is valid and the later source wins.

**`ImageSourceSet.Add(IImageSource)`**: returns `new ImageSourceSet([.. sources, source])`; the original is unchanged.

**`ImageSourceSet.FindByExtension(string)`**: takes the part after the last `/` or `\`, and returns the source of the
first extension (longest first) that the name ends with, ignoring case, or `null`.

**`ImageSourceSet.FindByScheme(string)`**: the source of the lower-cased scheme, or `null`. **`GetScheme(string)`**
(internal): the letters, digits, `+`, `-`, and `.` before the first colon when they start with a letter; `null` when
there is
none or when the scheme is a single letter (a drive).

**`ImageSourceRequest.OpenContent()`**: a read-only `MemoryStream` over `Content` (no copy when the memory is
array-backed); `InvalidOperationException` when `HasContent` is false.

**`ImageSourceRequest.SizeFor(naturalWidth, naturalHeight, fitInside)`**: the natural size when nothing is requested;
one
requested dimension and the other `round(natural * requested / natural other)` (at least 1); both requested give the
box, or with `fitInside` `Surface.FitInside` of the natural size into the box; a requested dimension below 1 counts as
1;
the result is clamped to 1 through `Surface.MaxDimension`; a natural size below 1 is `ArgumentOutOfRangeException`.

**`ImageSourceRequest.EnsureWithinBudget(width, height)`**: computes `width * height` as a `long` (a negative side
counts as 0), subtracts `ReservedPixels` (already charged; the extra is never negative) and throws
`ImageSourceBudgetException(requested, ReservedPixels + RemainingPixels)` (saturating) when the extra is greater than
`RemainingPixels`. A source calls it with the size it is about to allocate, before allocating; a source outside this
repository that prefers not to call the method can throw the exception itself.

#### Error Handling

An invalid claim is an `ArgumentException` that names the source; a null set or source is `ArgumentNullException`;
`OpenContent` on a scheme-claimed request is `InvalidOperationException`; `SizeFor` rejects a natural size below one
pixel; `EnsureWithinBudget` throws `ImageSourceBudgetException` for a size over the budget;
`ImageSourceResult` rejects a null surface. The unit throws nothing else and performs no I/O.

#### Dependencies

Only `Surface` of the `Canvas` subsystem (for `Surface.FitInside` and `Surface.MaxDimension` and as the picture type)
and the
.NET base class library. The documentation refers to `FolderImageResolver` by name only; the unit does not use it.

#### Callers

`FolderImageResolver` (builds requests, results, notes, embedded images, and exceptions; uses `ImageSourceSet`), every
source (`RasterImageSource` and the sources of the format packages), the `CanvasNetSvg` system (`IImageResolver`), the
`CanvasNetViewer` system, and applications that implement a source.

<!-- cspell:ignore embedder -->

### ImageSourceContract

![ImageSources Structure](ImageSourcesView.svg)

The `ImageSourceContract` unit is the contract of the `ImageSources` subsystem: the public types through which a format
or a live source plugs into a resolver, in the namespace `DemaConsulting.CanvasNet.ImageSources`. It comprises these
source
files, all `public`:

- `IImageSource.cs` - `IImageSource` (interface): a source and what it claims
- `ImageSourceRequest.cs` - `ImageSourceRequest` (sealed class): what a source is asked to draw, and the home of the
  budget contract
- `ImageBudget.cs` - `ImageBudget` (sealed class): the live pixel counter that every request of a render shares
- `ImageSourceResult.cs` - `ImageSourceResult` (sealed record): what it drew
- `ImageSourceDiagnostic.cs` and `ImageDiagnosticSeverity.cs` - `ImageSourceDiagnostic` (sealed record) and the severity
  enum
- `ImageDiagnosticLocation.cs` - `ImageDiagnosticLocation` (sealed record): where in a file a note points
- `ImageFragment.cs` - `ImageFragment` (sealed class) and `ImageFragmentParameter` (record struct): the parsed fragment
  of a link
- `IFragmentImageSource.cs` - `IFragmentImageSource` (interface): the fragment parameters and prefixes a source uses
- `ImageParameterValues.cs` - `ImageParameterValues` (static class): the value grammars of model variables and chart
  parameters, and the reading of prefixed fragment parameters
- `IImageSourceOptions.cs` - `IImageSourceOptions` (marker interface): caller-defined options of a direct render
- `ImageSourceRefusedException.cs` - `ImageSourceRefusedException` (sealed class): a source declining a picture
- `IImageResolver.cs` - `IImageResolver` (interface): what a renderer calls
- `IImageNaturalSizeResolver.cs` - `IImageNaturalSizeResolver` (interface): an `IImageResolver` that also says whether
  the picture is a raster at its own pixel size (`ImageSourceResult.IsNaturalSize`)
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
`Request`, `SizeRule`, `Result`, `FailureReporting`, `ImageFragment`, `FragmentPrefix`, `ParameterValues`, `FragmentReview`,
`Refusal`,
and
`DiagnosticDetail`) in
`docs/reqstream/canvas-net/image-sources/image-source-contract.yaml`.

#### Data Model

**`IImageSource`**: `Name` (stable lower-case format name), `Extensions` (lower case, with the dot; compound allowed;
the
longest match wins), `Schemes` (lower case, without the colon; never `file` or `data`), and
`ImageSourceResult Render(ImageSourceRequest)`. A source that claims nothing of one kind returns an empty list.

**`ImageSourceRequest`** (immutable apart from one counter): `Href` (the raw link; always set), `Content` (the bytes of
a file), `FileName` (the path of the file relative to the folder, with forward slashes), `Directory` (the full path of
the folder of the file), `PixelWidth` and `PixelHeight` (requested size in device pixels; `null` means the natural
size), `Resolver` (an `IImageResolver` scoped to the request), `ThrowOnUnsupportedFeature`, `CancellationToken`,
`Budget` (the shared live `ImageBudget`, or `null` for no limit, which is what a request built by hand has unless a test
of a custom source sets `new ImageBudget(maxPixels)`), `RemainingPixels` (read live from `Budget`; `long.MaxValue`
without one) and `ChargedPixels` (the pixels this request has charged through `EnsureWithinBudget`; default 0; the only
state that changes after construction). `Fragment` (an `init` property, the `ImageFragment` of the link; `Empty` for a
link without one) and `Options` (an `init` property, the `IImageSourceOptions` of a direct render; `null` for a link,
because an embedded file never inherits the options of its embedder). `HasContent` is true exactly when `FileName` is
not null: a file-based request
has content, a scheme-claimed request has only `Href`. The budget contract is documented once, in the XML remarks of
this class; the other documents link to it.

**`ImageFragment`** (immutable): the parsed text after the first `#` of a link, with the grammar
`fragment = [ param *( "&" param ) ]`, `param = name [ "=" value ]`. `Raw` is the text as written, `Parameters` the
decoded `(Name, Value)` pairs in order (a read-only list), and `Problem` is non-null when a limit was exceeded (more
than `MaxLength` = 2048 characters or `MaxParameters` = 32 parameters), in which case there are no parameters. The text
is split at `&` and at the first `=` of each segment before it is percent-decoded (`Uri.UnescapeDataString`; a `+` is
a `+`), so an encoded `%26` or `%3D` stays inside its name or value. `GetValue(names)` is the last value of any of the
names (ASCII case-insensitive), and `GetPositiveInt32(names)` reads it as a whole number from 1 (ASCII digits only,
leading zeros allowed). `Quote` (internal) shows untrusted text in a message with control characters replaced and a
length limit. `GetPrefixed(prefix)` returns the parameters whose name starts with the prefix (ASCII case-insensitive)
and has at
least one more character, with the prefix cut off the name (the rest as written, so `def.Width` is the name `Width`), in
the order the names first appear and with the last value of a name; `HasPrefix` (internal) is the test the resolver
shares.
`ReviewParameters(subject, href, source)` is the one check of a fragment against what a source declares: it groups the
parameters by name (ASCII case-insensitive; what follows a declared prefix as written), and gives an
`ImageFragmentNote` with the code `image-fragment-unknown-parameter` for a group that neither a declared name nor a
declared prefix understands, and `image-fragment-duplicate-parameter` for an understood group whose values differ (last
wins), in the order of first appearance. `FolderImageResolver.ReportFragmentParameters` turns the notes of the links of
a document into diagnostics, and the `SourceLoader` of the Viewer turns the notes of the input paths of the command line
into the diagnostics of a result, so both say the same thing with the same codes.
**`IFragmentImageSource`**: `FragmentParameters`, the lower-case names a source uses, and `FragmentParameterPrefixes`
(a default interface member, empty), the lower-case prefixes of the parameters whose names the link author chooses
(`def.`
for the variables of a model, `param.` for the parameters of a chart); a parameter with a declared prefix is understood
whatever follows it, so a source needs no list of names and its own names can never collide with them.
**`ImageParameterValues`** (static): `TryParseVariable` reads the grammar of `-D` (`true` and `false`; a finite number
as a
`double`; a value that starts with `[` is a vector, read as strict JSON by the code of the parameter grammar below and
refused when it is not valid JSON or has an object anywhere inside, so the elements are `double`, `bool`, `string`,
`null` and nested `List<object?>`; anything else is text with one pair of surrounding single or double
quotes removed), `TryParseParameter` the grammar of `--param` (`true`, `false`, `null`, a finite number; text that
starts
with `{`, `[` or `"` is strict JSON read by `JsonDocument` with `MaxDepth` = 32 and converted to `List<object?>`,
`Dictionary<string, object?>`, `string`, `double` and `bool`, at most `MaxValues` = 65,536 values and a finite number
only; anything else is text with one pair of single quotes removed), neither throws (a bad value gives a one-line
reason,
without the position of a JSON failure), and `ReadVariables` and `ReadParameters` read every `GetPrefixed` parameter of
a
fragment with one of them, throwing `ImageSourceRefusedException` (`image-fragment-invalid`, naming the parameter with
its
prefix and quoting the value) for the first bad value. A vector is what the viewport variables `$vpt` and `$vpr` need,
so `def.$vpt=[0,0,20]` and `-D '$vpt=[0,0,20]'` fix a camera. **`IImageSourceOptions`**:
a marker with no member. **`ImageSourceRefusedException`**: a `Code` and a message; how a source says "not this
picture".

**`ImageBudget`**: a private count of the pixels still free, created from `MaxDecodedPixels` (a negative maximum is
`ArgumentOutOfRangeException`), the public get-only `Remaining`, and the internal `Charge(pixels)` (throws
`ImageSourceBudgetException(pixels, Remaining)` and charges nothing when it does not fit) and `Refund(pixels)`. Only
`EnsureWithinBudget` and the resolver can charge it, so it cannot be overdrawn by accident. Not thread-safe.

**`ImageSourceResult`** (immutable): `Surface` (required; owned by the receiver), `Diagnostics`, `Echo`, and `Format`.
`Diagnostics` and `Echo` are copied into private `ReadOnlyCollection<T>` snapshots when they are assigned (null becomes
empty), so changing the original list afterwards does not change the result and a cast cannot modify it; `with` keeps
working.
**`ImageSourceDiagnostic`**: `Source`, `Severity` (`Info`, `Warning`, `Unsupported`), `Code`, `Message`, and the
optional
`Origin` (an `init` property, a list of strings, empty by default: the embedded files, outermost first, that the note is
about, set by the resolver; a root-relative file name for a file link and the href as written for a scheme link), and
the optional `Detail` (the finer identifier when `Code` is generic), `Location` (an `ImageDiagnosticLocation`: `File`,
`Line`, `Column`, `Pointer`, each optional) and `Page` (1-based). The
record overrides `Equals` and `GetHashCode` so that two diagnostics are equal when their fields and their origin
entries are equal, which the compiler-generated equality would not do for a list.
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
counts as 0), charges it to `Budget` at that moment (a lease; the sizes of several calls add up, and nothing is charged
when it throws) and adds it to `ChargedPixels` (saturating). It throws `ImageSourceBudgetException(requested,
RemainingPixels)` when the pixels exceed the live `RemainingPixels`. A source calls it once for every surface it is
about to allocate, with the size it will allocate, before allocating; a source that draws nested pictures charges its
own surface first so they see what is left. A source outside this repository that prefers not to call the method can
throw the exception itself. The internal `RefundCharges()` gives back what the request charged (the resolver calls it
when a source fails or is refused).

#### Error Handling

An invalid claim is an `ArgumentException` that names the source; a null set or source is `ArgumentNullException`;
`OpenContent` on a scheme-claimed request is `InvalidOperationException`; `SizeFor` rejects a natural size below one
pixel; `EnsureWithinBudget` throws `ImageSourceBudgetException` for a size over the live budget; `ImageBudget` rejects a
negative maximum;
`ImageSourceResult` rejects a null surface; `ImageFragment.Parse` never throws (a limit sets `Problem`), `GetValue`
rejects null names, and `GetPositiveInt32` throws `ImageSourceRefusedException` (code `image-fragment-invalid`) for a
value that is not a whole number from 1, and `GetPrefixed` rejects a null or empty prefix; `ImageParameterValues` never
throws for a
bad value, and its two readers refuse the link with `image-fragment-invalid`; `ImageSourceRefusedException` rejects an
empty code and a null message. The
unit throws nothing else and performs no I/O.

#### Dependencies

Only `Surface` of the `Canvas` subsystem (for `Surface.FitInside` and `Surface.MaxDimension` and as the picture type)
and the
.NET base class library. The documentation refers to `FolderImageResolver` by name only; the unit does not use it.

#### Callers

`FolderImageResolver` (builds requests, results, notes, embedded images, and exceptions; uses `ImageSourceSet`), every
source (`RasterImageSource` and the sources of the format packages), the `CanvasNetSvg` system (`IImageResolver`), the
`CanvasNetViewer` system, and applications that implement a source.

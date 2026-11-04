<!-- cspell:ignore embedder -->

### FolderImageResolver

![ImageSources Structure](ImageSourcesView.svg)

The `FolderImageResolver` unit loads the pictures a document links to from one folder, safely, through pluggable
sources.
It comprises these source files in the namespace `DemaConsulting.CanvasNet.ImageSources`, all `public`:

- `FolderImageResolver.cs` - `FolderImageResolver` (sealed partial class): the public entry points (`Resolve`,
  `Render(path)`, `Render(Stream)`), the dispatch, the limits, the call of a source, and the private scope class
- `FolderImageResolver.Href.cs` - the parsing of links: the cutting off of a query and a fragment, `file:` URIs,
  percent-encoding, and the description of a refusal of the sandbox
- `FolderImageResolverOptions.cs` - `FolderImageResolverOptions` (sealed record): the limits and switches

#### Purpose

The unit is the single, format-independent implementation of "draw what this link names" that the SVG codec, the
command-line
tool, and the viewer share. It exists so that the safety rules for untrusted links (confinement to a folder, refusal of
links and of the network, bounded depth, count and pixels, cycle detection) are written, tested, and reviewed once, in
the core package, instead of in each host. Everything format-specific is in the sources it calls.

The unit's behavior is specified by the `CanvasNet-ImageSources-FolderImageResolver-*` requirements (`Confinement`,
`FileUri`, `UriReference`, `Dispatch`, `Precedence`, `Schemes`, `NestedScope`, `Limits`, `PixelBudget`, `Cancellation`,
`Diagnostics`, `Failures`, `NoCaching`, `DirectRender`, and `Configuration`) in
`docs/reqstream/canvas-net/image-sources/folder-image-resolver.yaml`.

#### Data Model

**State of one resolver** (not thread-safe; one resolver serves one top-level render): the `ImageSourceSet` of the
sources;
the `FolderImageResolverOptions`; the full path of the folder and the `SandboxedFileResolver` over it (both `null` when
the
resolver has no folder; the sandbox allows `MaxImages + 1` distinct files, one more than the images because a direct
render
reads the top-level file too); the lists of notes, printed text, and embedded images; the count of images that reached a
source; the pixels charged so far against the pixel budget; and the root scope.

**`FolderImageResolverOptions`** (immutable): `MaxDepth` (4), `MaxImages` (64), `MaxDecodedPixels` (64,000,000: the
pixels, width times height summed over every picture, that the sources of one resolver may allocate in total),
`ThrowOnUnsupportedFeature` (false), and `CancellationToken` (none). `MaxDepth`, `MaxImages`, and `MaxDecodedPixels`
must not be negative.

**Scope** (private): the file whose folder relative links resolve against (`Current`; null for the root folder), the
chain of
identities (full paths for files, `scheme:` plus the link for scheme-claimed pictures) for the cycle check, the `Depth`
(levels of embedded files above the links of the scope: 0 at the top level, also for a direct render, which puts its
file on
the chain but not in the depth), and the name of the embedding file or link for failures. A scope is the
`IImageResolver`
that a source receives in its request; it shares the owner's counts and lists.

#### Key Methods

**`Resolve(href, pixelWidth, pixelHeight)`** (the tolerant embed path): (1) check the token; (2) find the scheme with
`ImageSourceSet.GetScheme`: a scheme other than `file` goes to the scheme path, anything else to the file path.

*Scheme path*: find the source that claims the scheme (none: unresolved, "network and other URLs are never loaded");
clamp the placement size and check it against the pixel budget (below); apply the limits to the key `scheme:` + link;
build a request with only the link **as written** (nothing is cut off a scheme link: the source owns its meaning), the
clamped size, the remaining budget, the child scope (no file, depth + 1), the switch, and the token; call the source.

*File path*: with no folder the link is unresolved. Convert the link to a path (`ToRelativePath`): first cut the query
(from the first `?`) and the fragment (from the first `#`) off the link, as RFC 3986 defines a reference, **before**
anything is decoded; a link that is empty after that is unresolved ("the reference is empty"). Then a `file:` URI
inside the folder becomes a path relative to the folder (a `file:` URI with a host or outside is refused), and for any
other link percent-encoding is decoded, so an encoded `%23` or `%3F` is a literal `#` or `?` of the file name and the
text that was cut off is never used again; absolute, drive, and UNC paths are left in the text so that the sandbox
refuses them. Pick the source by the longest extension of the path (none: unresolved, listing the claimed extensions)
**before any file is read**. Resolve the path with the sandbox, relative to the folder of the current file for an
ordinary link (a refusal becomes a reason; no file is "the file was not found"); check that the name the sandbox found
leads to the same source; clamp the placement size and check it against the pixel budget; apply the limits; build a
request with the bytes, the relative name (forward slashes), the folder, the clamped size, the remaining budget, the
child scope, the switch, and the token, and the link **as written** in `Href`; call the source.

**Limits** (`CheckLimits`): a key already on the chain is a cycle (checked first, with the sandbox's key comparer); a
scope
whose depth has reached `MaxDepth` refuses; a count that has reached `MaxImages` refuses; otherwise the count is
incremented.
Each refusal is an unresolved warning.

**Pixel budget** (`TryClampRequest`, `Draw`, `PrepareDirect`, `RenderDirect`): a picture can be far larger than the
rectangle it is placed in (a raster comes back at its own size), so the pixels the sources allocate are limited for the
whole render, charged **before** the allocation:

1. The placement size is clamped to 1 through `Surface.MaxDimension` per side. If its area (the *reservation*) is more
   than what is left of `MaxDecodedPixels`, the link is refused without calling a source (it does not count as a loaded
   image).
2. `Draw` adds the reservation to the pixels used, and the request carries it as `ReservedPixels` and what is left
   *after* that charge as `RemainingPixels` (the room for nested pictures, which are charged against the live budget,
   and for any surface larger than the requested size; the value is a snapshot). A source checks the size it will
   allocate (the raster source from the header, a vector source the size it draws at) with
   `ImageSourceRequest.EnsureWithinBudget`, which passes when the pixels beyond `ReservedPixels` do not exceed
   `RemainingPixels` and otherwise throws `ImageSourceBudgetException` before anything is allocated. The resolver
   catches it, gives the reservation back, and reports the warning `svg-image-too-large`.
3. After the call the resolver charges the larger of the reservation and the size of the surface that came back, against
   the live budget. If the excess does not fit, the surface is disposed and the link is refused with the same warning
   (this protects against a source that does not check, and against a nested picture that used the room the snapshot
   promised, after the fact; checking first is what protects the memory).

Nested links go through the scope and so draw on the same budget. A direct render (`Render`) reserves the requested
size, if any, in the same way but throws `ImageSourceBudgetException` instead of warning. The public
`RemainingPixels` property of the resolver is `MaxDecodedPixels` minus the pixels charged so far; a caller that allocates
a buffer of its own after a render (the viewer's resized final picture) compares its size with it.

**Calling a source** (`Invoke`/`Draw`): check the token; call `Render`; on any exception other than
`OperationCanceledException`, `ImageSourceException`, and `ImageSourceBudgetException` throw `ImageSourceException`
with the link, the embedder, and the original exception inside; apply the pixel budget as above; add each note with the
message `"{href}: {message}"`; add the printed text; add an `EmbeddedImage` with the format the source reported (or its
name), the size of the returned surface, and the depth; return the surface, which the caller now owns. Nothing is
cached.

**`Render(path, w, h)`**: strict version for a top-level file: no folder is `InvalidOperationException`; the extension
picks
the source or `NotSupportedException` (listing the extensions); the sandbox resolves the path (`SandboxException` for a
refusal, `FileNotFoundException` for none); the request has the file on the chain at depth 0; the result is returned
with the
format filled in. The path is used as given (no query, fragment, or decoding). **`Render(stream, name, w, h)`**: the
same without a file: the stream is read up to 16 MiB (`InvalidDataException` beyond), `Directory` is the folder of the
resolver, and the scope has no file.

#### Error Handling

An unresolved link is never an exception: it is a warning with the code `svg-image-unresolved` and the message
`image '{href}' was not drawn: {reason}`. A picture that does not fit in the pixel budget is the warning
`svg-image-too-large` with the message `image '{href}' was not drawn: it needs {n} pixels but only {m} of the budget of
{budget} pixels remain`. A found file that its source cannot draw is `ImageSourceException`.
Cancellation
(from the resolver's own checks, the sandbox, or a source) passes through as `OperationCanceledException`. A direct
render
throws the exceptions listed above, and `ImageSourceBudgetException` for an over-budget picture. Invalid construction
arguments (a null sequence, an empty folder, a negative limit, an
invalid claim) are `ArgumentNullException`, `ArgumentException`, or `ArgumentOutOfRangeException`.

#### Dependencies

`SandboxedFileResolver` and its types (`Files` subsystem), `ImageSourceSet` and the contract types
(`ImageSourceContract`
unit), and `Surface` of the `Canvas` subsystem. The sources it calls are injected as `IImageSource` instances.

#### Callers

The SVG codec (through `IImageResolver`, with `SvgLoadOptions.ImageResolver`), the `CanvasNetViewer` system (creates a
resolver per render and calls `Render`), the command-line tool, and applications.

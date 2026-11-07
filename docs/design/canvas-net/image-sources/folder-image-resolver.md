<!-- cspell:ignore embedder -->

### FolderImageResolver

![ImageSources Structure](ImageSourcesView.svg)

The `FolderImageResolver` unit loads the pictures a document links to from one folder, safely, through pluggable
sources.
It comprises these source files in the namespace `DemaConsulting.CanvasNet.ImageSources`, all `public`:

- `FolderImageResolver.cs` - `FolderImageResolver` (sealed partial class): the public entry points (`Resolve`,
  `Render(path)`, `Render(Stream)`), the dispatch, the limits, the call of a source, and the private scope class
- `FolderImageResolver.Href.cs` - the parsing of links: the cutting off of a query and a fragment (`SplitHref`),
  `file:` URIs, percent-encoding, and the description of a refusal of the sandbox
- `FolderImageResolverOptions.cs` - `FolderImageResolverOptions` (sealed record): the limits and switches

#### Purpose

The unit is the single, format-independent implementation of "draw what this link names" that the SVG codec, the
command-line
tool, and the viewer share. It exists so that the safety rules for untrusted links (confinement to a folder, refusal of
links and of the network, bounded depth, count and pixels, cycle detection) are written, tested, and reviewed once, in
the core package, instead of in each host. Everything format-specific is in the sources it calls.

The unit's behavior is specified by the `CanvasNet-ImageSources-FolderImageResolver-*` requirements (`Confinement`,
`FileUri`, `UriReference`, `Dispatch`, `Precedence`, `Schemes`, `NestedScope`, `Limits`, `PixelBudget`, `Cancellation`,
`Diagnostics`, `Failures`, `NoCaching`, `DirectRender`, `Configuration`, `Fragment`, `FragmentDiagnostics`, and
`PageRefusal`) in
`docs/reqstream/canvas-net/image-sources/folder-image-resolver.yaml`.

#### Data Model

**State of one resolver** (not thread-safe; one resolver serves one top-level render): the `ImageSourceSet` of the
sources; the `FolderImageResolverOptions`; the full path of the folder (`null` when the resolver has no folder) and the
`SandboxedFileResolver` over it, created at first use (never when there is no folder); the lists of notes, printed text,
and embedded images; the count of images that reached a source; the live `ImageBudget` (created from
`MaxDecodedPixels` and handed to every request); and the root scope. The sandbox may read `MaxImages` distinct files,
plus one more when the render is by path, because `Render(path)` also reads the top-level file and a render of a stream
does not; the limit is fixed when the sandbox is created, so a render by path is the first call of a resolver.

**`FolderImageResolverOptions`** (immutable): `MaxDepth` (4), `MaxImages` (64), `MaxDecodedPixels` (64,000,000: the
pixels, width times height summed over every picture, that the sources of one resolver may allocate in total),
`ThrowOnUnsupportedFeature` (false), and `CancellationToken` (none). `MaxDepth`, `MaxImages`, and `MaxDecodedPixels`
must not be negative.

**Scope** (private): the file whose folder relative links resolve against (`Current`; null for the root folder), the
chain of
identities (full paths for files, `scheme:` plus the link for scheme-claimed pictures) for the cycle check, the `Depth`
(levels of embedded files above the links of the scope: 0 at the top level, also for a direct render, which puts its
file on
the chain but not in the depth), the name of the embedding file or link for failures, and the `Origin` (the names of the
embedded files and scheme links above the scope, outermost first: empty for the root scope and for a direct render, and
for a child scope the origin of its parent followed by the file name relative to the folder or by the link as written
for a scheme link). A scope is the
`IImageResolver`
that a source receives in its request; it shares the owner's counts and lists.

#### Key Methods

**`Resolve(href, pixelWidth, pixelHeight)`** (the tolerant embed path): (1) check the token; (2) find the scheme with
`ImageSourceSet.GetScheme`: a scheme other than `file` goes to the scheme path, anything else to the file path.

*Scheme path*: find the source that claims the scheme (none: unresolved, "network and other URLs are never loaded");
clamp the placement size and check it against the live budget (below); apply the limits to the key `scheme:` + link;
parse the fragment (below); build a request with only the link **as written** (nothing is cut off a scheme link: the
source owns its meaning), the parsed `Fragment`, the clamped size, the shared budget, the child scope (no file, depth +
1), the switch, and the token; call the source.

*File path*: with no folder the link is unresolved. Convert the link to a path (`ToRelativePath`, which uses
`SplitHref`): first cut the query (from the first `?` before a `#`) and the fragment (from the first `#`) off the link,
as RFC 3986 defines a reference, **before**
anything is decoded; a `?` after the first `#` belongs to the fragment; a link that is empty after that is unresolved
("the reference is empty"). Then a `file:` URI
inside the folder becomes a path relative to the folder (a `file:` URI with a host or outside is refused), and for any
other link percent-encoding is decoded, so an encoded `%23` or `%3F` is a literal `#` or `?` of the file name and the
text that was cut off is never used for the path (the query is never used; the fragment goes to the source, below);
absolute, drive, and UNC paths are left in the text so that the sandbox
refuses them. Pick the source by the longest extension of the path (none: unresolved, listing the claimed extensions)
and apply the depth and image-count limits (`CheckDepthAndCount`), both **before any file is read**, so an image that is
over a limit is never opened. Parse the fragment (below). Resolve the path with the sandbox, relative to the folder of
the current file for an
ordinary link (a refusal becomes a reason; no file is "the file was not found"); check that the name the sandbox found
leads to the same source; clamp the placement size and check it against the live budget; apply the cycle check, which
needs the resolved key, and only then count the image; build a request with the bytes, the relative name (forward
slashes), the folder, the parsed `Fragment`, the clamped size, the shared budget, the child scope, the switch, and the
token, and the link
**as written** in `Href`; call the source.

**Fragment** (`ResolveFile`, `ResolveScheme`, `Draw`): the text after the first `#` of the link is parsed with
`ImageFragment.Parse`. A fragment with a `Problem` (over 2048 characters or 32 parameters) is refused right after the
source is chosen and before anything is counted or read: the note has the code `image-fragment-invalid` and the
severity unsupported, the source is not called and the file is not counted as an image. Otherwise `Draw`, before it
calls the source, reports for each parameter name that the source does not declare in `IFragmentImageSource`
(case-insensitively) one `image-fragment-unknown-parameter` warning - a source without the interface declares nothing,
so every parameter of a file link is unknown, while a scheme-claimed source without the interface owns its links and
gets
no warning - and for each name given twice with different values one `image-fragment-duplicate-parameter` warning (the
last value wins in the source; the same value twice is silent, and parameters a source treats as aliases are compared by
`GetValue` and not reported as duplicates). The message names the href and the parameter through
`ImageFragment.Quote`, which replaces control characters and cuts the text, so a hostile link cannot flood a report.
The picture is still drawn after a warning. A name that starts with a prefix the source declares in
`IFragmentImageSource.FragmentParameterPrefixes` (ASCII case-insensitive, at least one more character) is understood, so
it gets no unknown-parameter warning; a bare prefix is still unknown, and so is a prefixed name for a source that
declares
no prefix (none, a null list, a null or empty entry). Two names are the same parameter when they are equal ignoring
case,
except that the part after a declared prefix is compared as written (`GroupKey`), so `def.a` and `def.A` are two
variables
and `def.b` and `DEF.b` are one.

**Refusal** (`Draw`, `Invoke`): an `ImageSourceRefusedException` of a source (a page that does not exist, a value it
cannot use) passes `Invoke` unwrapped; `Draw` adds an unsupported note with the code of the exception and the message
`"{href}: {message}"`, gives back what the request charged, and returns nothing, so the render carries on. A direct
render lets the exception propagate.

**Limits**: `CheckDepthAndCount` refuses a scope whose depth has reached `MaxDepth` and a count that has reached
`MaxImages` (no file is needed, so it runs before the sandbox opens one); `CheckCycle` refuses a key already on the
chain (with the sandbox's key comparer; it needs the resolved key, so for a file it runs after the sandbox, and for a
scheme-claimed link, where nothing is read, it runs first so a self-embedding link is named as a cycle). The image is
counted (`_loaded`) only after every check has passed. Each refusal is an unresolved warning.

**Pixel budget** (`TryClampRequest`, `PrepareDirect`, `Draw`, `RenderDirect`, `Run`): the contract is documented once,
in the XML remarks of `ImageSourceRequest`; this is the resolver's side of it. A picture can be far larger than the
rectangle it is placed in (a raster comes back at its own size), so the pixels the sources allocate are limited for the
whole render by one live `ImageBudget`, and nothing is reserved up front:

1. The placement size is clamped to 1 through `Surface.MaxDimension` per side. If its area is more than the live
   remaining budget, the link is refused without calling a source (it does not count as a loaded image); nothing is
   charged by this check. A direct render with a requested size does the same check but throws
   `ImageSourceBudgetException`; a natural-size render has nothing to check.
2. The request carries the shared budget. A source charges each surface it is about to allocate with
   `ImageSourceRequest.EnsureWithinBudget`, which takes the pixels from the live budget (or throws
   `ImageSourceBudgetException` before anything is allocated, charging nothing) and adds them to
   `ImageSourceRequest.ChargedPixels`. The SVG source charges its own canvas before it resolves nested pictures, so they
   see only what is left.
3. `Run` settles the call. When the source throws, returns nothing, or is refused, it gives back
   `ChargedPixels` (nested pictures already drawn stay charged) and `Draw` turns an `ImageSourceBudgetException` into
   the warning `svg-image-too-large`. When the source returns, the resolver charges the excess of the returned surface
   over `ChargedPixels`; if that does not fit, the surface is disposed, the charge is given back, and the link is
   refused with the same warning (this protects against a source that does not check; checking first is what protects
   the memory). A surface that was accepted stays charged for the rest of the render.

The public `RemainingPixels` property of the resolver is the live remaining budget; a caller that allocates a buffer of
its own after a render (the viewer's resized final picture) compares its size with it.

**Calling a source** (`Invoke`/`Run`/`Draw`): check the token; call `Render`; on any exception other than
`OperationCanceledException`, `ImageSourceException`, and `ImageSourceBudgetException` throw `ImageSourceException`
with the link, the embedder, and the original exception inside; settle the pixel budget as above; add each note with
the message `"{href}: {message}"` (the origin of the note becomes the origin of the child scope followed by the origin
the
source set on the note); add the printed text; add an `EmbeddedImage` with the format the source reported (or
its name), the size of the returned surface, and the depth; return the surface, which the caller now owns. Nothing is
cached.

**`Render(path, w, h)`**: strict version for a top-level file: no folder is `InvalidOperationException`; the extension
picks
the source or `NotSupportedException` (listing the extensions); the sandbox resolves the path (`SandboxException` for a
refusal, `FileNotFoundException` for none); the request has the file on the chain at depth 0; the result is returned
with the
format filled in. The path is used as given (no query, fragment, or decoding). The overloads with a final
`IImageSourceOptions` parameter do the same and put the options in `ImageSourceRequest.Options` (a link never gets
options, so an embedded file does not inherit those of its embedder); the overloads without it pass `null`.
**`Render(stream, name, w, h)`**: the
same without a file: the stream is read up to 16 MiB (`InvalidDataException` beyond), `Directory` is the folder of the
resolver, and the scope has no file.

#### Error Handling

An unresolved link is never an exception: it is a warning with the code `svg-image-unresolved` and the message
`image '{href}' was not drawn: {reason}`. A fragment over its limits, or a page a source refuses, is an unsupported note
with the code `image-fragment-invalid` or the code of the `ImageSourceRefusedException` (`image-page-out-of-range`),
and an unknown or duplicate parameter is a warning; none of them is an exception, and none stops the render.
A picture that does not fit in the pixel budget is the warning
`svg-image-too-large` with the message `image '{href}' was not drawn: it needs {n} pixels but only {m} of the budget of
{budget} pixels remain`. Both carry the origin of the scope that contains the link (the file that holds it). A found
file
that its source cannot draw is `ImageSourceException`.
Cancellation
(from the resolver's own checks, the sandbox, or a source) passes through as `OperationCanceledException`. A direct
render
throws the exceptions listed above, and `ImageSourceBudgetException` for an over-budget picture. Invalid construction
arguments (a null sequence, an empty or white-space folder, a negative limit, an
invalid claim) are `ArgumentNullException`, `ArgumentException`, or `ArgumentOutOfRangeException`.

#### Dependencies

`SandboxedFileResolver` and its types (`Files` subsystem), `ImageSourceSet` and the contract types
(`ImageSourceContract`
unit), and `Surface` of the `Canvas` subsystem. The sources it calls are injected as `IImageSource` instances.

#### Callers

The SVG codec (through `IImageResolver`, with `SvgLoadOptions.ImageResolver`), the `CanvasNetViewer` system (creates a
resolver per render and calls `Render`), the command-line tool, and applications.

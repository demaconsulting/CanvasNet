// cspell:ignore href
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Svg;

/// <summary>
///     Optional, additive load settings for <see cref="SvgCodec"/>'s <c>Load</c>/
///     <c>LoadWithFontFaces</c> overloads that accept an options argument.
/// </summary>
/// <remarks>
///     Every property's default value reproduces the behavior of the overloads that take no
///     options argument, so passing <c>new SvgLoadOptions()</c> is always equivalent to omitting
///     the argument.
/// </remarks>
public sealed record SvgLoadOptions
{
    /// <summary>
    ///     Gets a value indicating whether <c>text</c> rendering may use fonts installed on the
    ///     host operating system (found via
    ///     <see cref="DemaConsulting.CanvasNet.Fonts.SystemFontCatalog"/>) when a
    ///     <c>font-family</c> name is not satisfied by a caller-registered font.
    /// </summary>
    /// <remarks>
    ///     Defaults to <see langword="true"/>. Installed fonts differ from host to host, so the same
    ///     document can rasterize differently on different machines. Set this to
    ///     <see langword="false"/> for reproducible output: every <c>text</c> element that no
    ///     caller-registered font satisfies is then drawn with the bundled Liberation Sans/Serif/
    ///     Mono fonts (embedded in <c>DemaConsulting.CanvasNet</c>), and the host's installed fonts
    ///     are never consulted.
    /// </remarks>
    public bool UseSystemFonts { get; init; } = true;

    /// <summary>
    ///     Gets an optional resolver that loads the picture an <c>image</c> element's <c>href</c>
    ///     refers to, or <see langword="null"/> (the default) to keep the codec's data-URI-only
    ///     behavior.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     By default the codec draws an <c>image</c> only when its <c>href</c> is a base64
    ///     <c>data:</c> URI, and it never reads a file or the network for any other <c>href</c>. Set
    ///     this resolver to opt in to loading other references. The codec itself still performs no
    ///     I/O: the only I/O is whatever the resolver you supply does, and what it is allowed to
    ///     reach (a folder, a sandbox, a network allow-list) is entirely your decision. Do not
    ///     resolve an <c>href</c> from an untrusted document against the file system without
    ///     confining it first; <see cref="FolderImageResolver"/> does that confinement (and also
    ///     loads models, charts, nested SVG files, documents and rasters through pluggable
    ///     <see cref="IImageSource"/> implementations).
    ///     </para>
    ///     <para>
    ///     The resolver receives the raw <c>href</c> and the device-pixel size of the placement
    ///     rectangle through <see cref="IImageResolver.Resolve(string, int, int)"/>. It returns a
    ///     <see cref="Surface"/> or <see langword="null"/>. A <see langword="null"/> result is a
    ///     tolerant no-op: the element draws nothing. A returned surface is fitted into the
    ///     placement rectangle per the element's <c>preserveAspectRatio</c> and drawn through the
    ///     same <c>image-rendering</c>, <c>opacity</c>, <c>clip-path</c>, <c>mask</c> and
    ///     <c>filter</c> pipeline as a decoded <c>data:</c> raster. The codec takes ownership of
    ///     the returned surface and disposes it; the resolver must not keep or reuse it, and must
    ///     return a fresh surface on every call (the codec does not cache results, so a resolver
    ///     backed by a live source, such as a video frame, is asked again for every
    ///     <c>image</c> element and every render).
    ///     </para>
    ///     <para>
    ///     The resolver is never called for an empty <c>href</c>, a <c>data:</c> URI (valid or
    ///     not), or a same-document <c>#fragment</c> reference. The codec charges the placement's
    ///     pixel area plus a fixed per-call overhead against its shared work budget before each
    ///     call and skips the call when the budget is exhausted, which bounds the number of calls a
    ///     hostile document (for example a <c>use</c> fan-out) can cause. A surface larger than the
    ///     requested size is charged for the difference and dropped when that exceeds the budget.
    ///     </para>
    ///     <para>
    ///     An exception thrown by the resolver propagates to the caller of <c>Load</c> unchanged:
    ///     the resolver is trusted host code, so its failures are not hidden. The one exception is
    ///     <see cref="FormatException"/>, which is wrapped in an <see cref="InvalidOperationException"/>
    ///     so that a host error is not reported as invalid SVG numeric data. The resolver is not
    ///     used by <c>feImage</c>, whose external references stay unsupported.
    ///     </para>
    /// </remarks>
    public IImageResolver? ImageResolver { get; init; }
}

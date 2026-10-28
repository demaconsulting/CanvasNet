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
}

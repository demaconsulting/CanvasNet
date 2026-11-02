namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     Controls where a <see cref="FontResolver"/> looks for a named font family.
/// </summary>
/// <remarks>
///     <para>
///         The defaults make resolution independent of the host: only the bundled Liberation fonts (and the
///         additional directories, when given) are candidates. The instance is a record with <see langword="init"/>-only
///         properties and is not modified by the resolver (which copies what it needs), so it may be shared between
///         threads and resolvers.
///     </para>
///     <para>
///         <b>Thread safety</b>: immutable once initialized, provided the list given as
///         <see cref="AdditionalDirectories"/> is not modified afterwards (the resolver copies it when it is created).
///     </para>
/// </remarks>
public sealed record FontResolverOptions
{
    /// <summary>
    ///     Gets the directories scanned (recursively) for fonts before the installed fonts, in search order; empty by
    ///     default. A missing or unreadable directory and a file that is not a usable font are skipped silently; see
    ///     <see cref="FontDirectoryScanner"/> for the traversal order and link policy.
    /// </summary>
    public IReadOnlyList<string> AdditionalDirectories { get; init; } = [];

    /// <summary>
    ///     Gets a value indicating whether the fonts installed on the host (<see cref="SystemFontCatalog.Fonts"/>)
    ///     are consulted for a family that the additional directories do not have; <see langword="false"/> by
    ///     default, so resolution does not depend on the machine.
    /// </summary>
    public bool UseSystemFonts { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the well-known family names that
    ///     <see cref="GenericFontFamilies.TryClassify"/> recognizes as aliases (<c>Arial</c>, <c>Helvetica</c>,
    ///     <c>Times New Roman</c>, <c>Courier New</c>, ...) resolve to the bundled Liberation font of their design
    ///     <i>before</i> any directory or installed font is consulted, so that those names give the same result on
    ///     every machine; <see langword="false"/> by default (the names are then looked up like any other family).
    ///     The CSS generic keywords always resolve to the bundled fonts.
    /// </summary>
    public bool MapCommonAliasesToBundled { get; init; }

    /// <summary>
    ///     Gets the bounds of the scan of <see cref="AdditionalDirectories"/>; the defaults of
    ///     <see cref="FontScanLimits"/> by default.
    /// </summary>
    public FontScanLimits ScanLimits { get; init; } = new();
}

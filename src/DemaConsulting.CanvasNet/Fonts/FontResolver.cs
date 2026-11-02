using System.Collections.Concurrent;

namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     Where a font returned by <see cref="FontResolver.Resolve"/> came from.
/// </summary>
public enum FontSource
{
    /// <summary>One of the Liberation Sans, Serif or Mono fonts bundled with the library: the same on every machine.</summary>
    Bundled,

    /// <summary>A font file found in one of <see cref="FontResolverOptions.AdditionalDirectories"/>.</summary>
    AdditionalDirectory,

    /// <summary>A font installed on the host, consulted because <see cref="FontResolverOptions.UseSystemFonts"/> is set.</summary>
    System,
}

/// <summary>
///     The font face a request resolved to.
/// </summary>
/// <param name="Font">The loaded font; shared (the same instance is returned for the same face) and safe to use from several threads.</param>
/// <param name="Family">
///     The family name of the face: for a bundled face <c>Liberation Sans</c>, <c>Liberation Serif</c> or
///     <c>Liberation Mono</c>, otherwise the family name the font file reports.
/// </param>
/// <param name="Source">Where the face came from.</param>
/// <param name="Bold">Whether the face is bold (for a bundled face, whether bold was requested, which is what the bundled face is).</param>
/// <param name="Italic">Whether the face is italic (for a bundled face, whether italic was requested, which is what the bundled face is).</param>
public sealed record ResolvedFont(TrueTypeFont Font, string Family, FontSource Source, bool Bold, bool Italic);

/// <summary>
///     Resolves a CSS <c>font-family</c> list, a weight and a style to a font, deterministically and without ever
///     failing: the bundled Liberation Sans is the last resort.
/// </summary>
/// <remarks>
///     <para>
///         <b>Order.</b> The list is read from left to right and the first family that resolves wins. For each
///         family: (1) a generic keyword (<c>sans-serif</c>, <c>serif</c>, <c>monospace</c>, ... - and, when
///         <see cref="FontResolverOptions.MapCommonAliasesToBundled"/> is set, a common alias such as <c>Arial</c> or
///         <c>Times New Roman</c>; see <see cref="GenericFontFamilies"/>) resolves to the bundled Liberation font of that
///         design; (2) otherwise the family is looked up by exact name (ignoring case) in the
///         <see cref="FontResolverOptions.AdditionalDirectories"/>; (3) then, only when
///         <see cref="FontResolverOptions.UseSystemFonts"/> is set, among the installed fonts; (4) then, if it is one
///         of the three bundled family names (<c>Liberation Sans</c>, <c>Liberation Serif</c>, <c>Liberation Mono</c>),
///         the bundled font of that name. A family that is found nowhere is skipped, and an empty list, or a list that
///         is exhausted, resolves to the bundled Liberation Sans. The generic substitution of
///         <see cref="SystemFontCatalog.FindBestMatch"/> is deliberately not used, because it would make the result
///         depend on what the host has installed.
///     </para>
///     <para>
///         <b>Style.</b> Within a family the face whose bold and italic flags differ from the request least wins, and
///         the first one found wins a tie (see <see cref="SystemFontCatalog.FindInFamily"/>); a family that lacks the
///         requested style is used as it is. A bundled face has exactly the requested style.
///     </para>
///     <para>
///         <b>Failure reporting.</b> A family that is found but whose font file cannot be loaded (it vanished or is
///         damaged) is skipped like a family that is not found - the faces of the later steps for that family are not
///         tried - and the reason is reported through the <c>failure</c> output of <see cref="Resolve"/> when no later
///         family in the list was met either.
///     </para>
///     <para>
///         <b>Directories.</b> The additional directories are scanned once, on the first lookup that needs them, with
///         <see cref="FontDirectoryScanner.Scan"/> (deterministic, bounded by
///         <see cref="FontResolverOptions.ScanLimits"/>, header-only, never following a link and skipping linked
///         files); a font is loaded only when it is selected, through <see cref="SystemFontCatalog.Load"/>, whose
///         process-wide cache keeps a loaded face (or a failed load) for the life of the process.
///     </para>
///     <para>
///         <b>Thread safety</b>: every member may be called concurrently. The one-time directory scan runs under a
///         lock (concurrent first callers wait for one scan), the results of earlier requests are kept in a
///         <see cref="ConcurrentDictionary{TKey, TValue}"/> - one entry per distinct request, so use one resolver per
///         render or job rather than one for the life of a process that resolves untrusted family lists - and the
///         loaded fonts are shared immutable objects.
///     </para>
/// </remarks>
public sealed class FontResolver
{
    /// <summary>The reason reported when a family is found nowhere.</summary>
    private const string NotFoundReason = "no such font family was found";

    /// <summary>The name of the bundled sans-serif family.</summary>
    private const string SansFamily = "Liberation Sans";

    /// <summary>The name of the bundled serif family.</summary>
    private const string SerifFamily = "Liberation Serif";

    /// <summary>The name of the bundled monospace family.</summary>
    private const string MonoFamily = "Liberation Mono";

    /// <summary>The additional directories, in search order (a private copy).</summary>
    private readonly string[] _directories;

    /// <summary>Whether the installed fonts are consulted.</summary>
    private readonly bool _useSystemFonts;

    /// <summary>Whether common aliases resolve to the bundled fonts before any lookup.</summary>
    private readonly bool _mapAliases;

    /// <summary>The bounds of the scan of the additional directories.</summary>
    private readonly FontScanLimits _limits;

    /// <summary>The token that cancels the scan of the additional directories.</summary>
    private readonly CancellationToken _cancellation;

    /// <summary>Supplies the installed faces; read only when <see cref="_useSystemFonts"/> is set and a lookup needs it.</summary>
    private readonly Func<IReadOnlyList<SystemFontInfo>> _systemFaces;

    /// <summary>The resolutions already made, by family list, bold and italic.</summary>
    private readonly ConcurrentDictionary<(string Family, bool Bold, bool Italic), Resolution> _resolved = new();

    /// <summary>Guards the one-time scan of the additional directories.</summary>
    private readonly object _scanLock = new();

    /// <summary>The faces of the additional directories; <see langword="null"/> until the first lookup that needs them.</summary>
    private IReadOnlyList<SystemFontInfo>? _directoryFaces;

    /// <summary>
    ///     Initializes a resolver.
    /// </summary>
    /// <param name="options">The options, or <see langword="null"/> for the defaults (bundled fonts only).</param>
    /// <param name="cancellationToken">
    ///     A token that cancels the scan of the additional directories: while the scan runs (on the first lookup that
    ///     needs it) a canceled token makes <see cref="Resolve"/> throw <see cref="OperationCanceledException"/>, and
    ///     the scan is not kept, so a later call scans again.
    /// </param>
    public FontResolver(FontResolverOptions? options = null, CancellationToken cancellationToken = default)
        : this(options, cancellationToken, static () => SystemFontCatalog.Fonts)
    {
    }

    /// <summary>
    ///     Initializes a resolver over an explicit list of installed faces (a test seam, so the installed-font step
    ///     can be exercised without depending on the host).
    /// </summary>
    /// <param name="options">The options, or <see langword="null"/> for the defaults.</param>
    /// <param name="cancellationToken">The token that cancels the scan of the additional directories.</param>
    /// <param name="systemFaces">Supplies the installed faces.</param>
    internal FontResolver(
        FontResolverOptions? options,
        CancellationToken cancellationToken,
        Func<IReadOnlyList<SystemFontInfo>> systemFaces)
    {
        options ??= new FontResolverOptions();
        ArgumentNullException.ThrowIfNull(options.AdditionalDirectories);
        ArgumentNullException.ThrowIfNull(options.ScanLimits);
        _directories = [.. options.AdditionalDirectories];
        _useSystemFonts = options.UseSystemFonts;
        _mapAliases = options.MapCommonAliasesToBundled;
        _limits = options.ScanLimits;
        _cancellation = cancellationToken;
        _systemFaces = systemFaces;
    }

    /// <summary>
    ///     Resolves a family list, a weight and a style to a font.
    /// </summary>
    /// <param name="cssFamilyList">
    ///     The CSS family list (<c>"Helvetica Neue", Arial, sans-serif</c>); <see langword="null"/>, empty or white
    ///     space means the default, the bundled Liberation Sans.
    /// </param>
    /// <param name="bold">Whether a bold face is requested.</param>
    /// <param name="italic">Whether an italic (or oblique) face is requested.</param>
    /// <param name="failure">
    ///     Receives <see langword="null"/> when the request was met (or named no family); otherwise a short reason
    ///     the bundled Liberation Sans was used instead - <c>no such font family was found</c>, or <c>the font file
    ///     '&lt;path&gt;' could not be loaded</c>.
    /// </param>
    /// <returns>The face; never <see langword="null"/>, because the bundled Liberation Sans is the last resort.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the token of the resolver is canceled while the additional directories are scanned.</exception>
    public ResolvedFont Resolve(string? cssFamilyList, bool bold, bool italic, out string? failure)
    {
        var resolution = _resolved.GetOrAdd(
            (cssFamilyList ?? string.Empty, bold, italic),
            key => ResolveUncached(key.Family, key.Bold, key.Italic));
        failure = resolution.Failure;
        return resolution.Font;
    }

    /// <summary>
    ///     Gets a bundled Liberation face.
    /// </summary>
    /// <param name="serif">Whether the serif family is requested.</param>
    /// <param name="fixedPitch">Whether the monospace family is requested.</param>
    /// <param name="bold">Whether bold is requested.</param>
    /// <param name="italic">Whether italic is requested.</param>
    /// <returns>The face.</returns>
    private static ResolvedFont Bundled(bool serif, bool fixedPitch, bool bold, bool italic)
    {
        var font = SystemFontCatalog.LoadBundledFallback(serif, fixedPitch, bold, italic);
        var family = SansFamily;
        if (fixedPitch)
        {
            family = MonoFamily;
        }
        else if (serif)
        {
            family = SerifFamily;
        }

        return new ResolvedFont(font, family, FontSource.Bundled, bold, italic);
    }

    /// <summary>
    ///     Determines whether a family name is one of the three bundled family names.
    /// </summary>
    /// <param name="family">The family name.</param>
    /// <param name="serif">Receives whether the name is the serif family.</param>
    /// <param name="fixedPitch">Receives whether the name is the monospace family.</param>
    /// <returns><see langword="true"/> for <c>Liberation Sans</c>, <c>Liberation Serif</c> or <c>Liberation Mono</c> (ignoring case).</returns>
    private static bool TryBundledFamily(string family, out bool serif, out bool fixedPitch)
    {
        serif = string.Equals(family, SerifFamily, StringComparison.OrdinalIgnoreCase);
        fixedPitch = string.Equals(family, MonoFamily, StringComparison.OrdinalIgnoreCase);
        return serif || fixedPitch || string.Equals(family, SansFamily, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Resolves a family list that is not in the cache.
    /// </summary>
    /// <param name="cssFamilyList">The family list.</param>
    /// <param name="bold">Whether bold is requested.</param>
    /// <param name="italic">Whether italic is requested.</param>
    /// <returns>The resolution.</returns>
    private Resolution ResolveUncached(string cssFamilyList, bool bold, bool italic)
    {
        string? firstFailure = null;
        foreach (var family in GenericFontFamilies.SplitFamilyList(cssFamilyList))
        {
            if (GenericFontFamilies.TryClassify(family, _mapAliases, out var serif, out var fixedPitch))
            {
                return new Resolution(Bundled(serif, fixedPitch, bold, italic), null);
            }

            var resolved = TryResolveNamed(family, bold, italic, out var reason);
            if (resolved is not null)
            {
                return new Resolution(resolved, null);
            }

            firstFailure ??= reason;
        }

        // Nothing named was met: the default, with the reason when a family was asked for
        return new Resolution(Bundled(serif: false, fixedPitch: false, bold, italic), firstFailure);
    }

    /// <summary>
    ///     Looks a family up by name: the additional directories, then the installed fonts, then the bundled names.
    /// </summary>
    /// <param name="family">The family name.</param>
    /// <param name="bold">Whether bold is requested.</param>
    /// <param name="italic">Whether italic is requested.</param>
    /// <param name="reason">Receives why the family could not be met when the result is <see langword="null"/>.</param>
    /// <returns>The face, or <see langword="null"/> when the family is not found or its file cannot be loaded.</returns>
    private ResolvedFont? TryResolveNamed(string family, bool bold, bool italic, out string reason)
    {
        // A face that is found decides the family: when its file cannot be loaded the later sources are not tried
        if (_directories.Length > 0
            && SystemFontCatalog.FindInFamily(DirectoryFaces(), family, bold, italic) is { } fromDirectory)
        {
            return Load(fromDirectory, FontSource.AdditionalDirectory, out reason);
        }

        if (_useSystemFonts
            && SystemFontCatalog.FindInFamily(_systemFaces(), family, bold, italic) is { } installed)
        {
            return Load(installed, FontSource.System, out reason);
        }

        if (TryBundledFamily(family, out var serif, out var fixedPitch))
        {
            reason = string.Empty;
            return Bundled(serif, fixedPitch, bold, italic);
        }

        reason = NotFoundReason;
        return null;
    }

    /// <summary>
    ///     Loads a found face.
    /// </summary>
    /// <param name="info">The face.</param>
    /// <param name="source">Where the face was found.</param>
    /// <param name="reason">Receives why the face could not be loaded when the result is <see langword="null"/>.</param>
    /// <returns>The face, or <see langword="null"/> when its file cannot be loaded.</returns>
    private static ResolvedFont? Load(SystemFontInfo info, FontSource source, out string reason)
    {
        reason = string.Empty;
        var font = SystemFontCatalog.Load(info);
        if (font is not null)
        {
            return new ResolvedFont(font, info.FamilyName, source, info.Bold, info.Italic);
        }

        reason = $"the font file '{info.FilePath}' could not be loaded";
        return null;
    }

    /// <summary>
    ///     Gets the faces of the additional directories, scanning them on the first call.
    /// </summary>
    /// <returns>The faces, in scan order.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the token of the resolver is canceled during the scan.</exception>
    private IReadOnlyList<SystemFontInfo> DirectoryFaces()
    {
        var faces = Volatile.Read(ref _directoryFaces);
        if (faces is not null)
        {
            return faces;
        }

        lock (_scanLock)
        {
            // A canceled scan throws before the field is assigned, so the next call scans again
            _directoryFaces ??= FontDirectoryScanner.Scan(_directories, _limits, trustLinkedFiles: false, _cancellation);
            return _directoryFaces;
        }
    }

    /// <summary>
    ///     A cached resolution: the face and the reason it is the fallback, if it is.
    /// </summary>
    /// <param name="Font">The face.</param>
    /// <param name="Failure">The reason, or <see langword="null"/> when the request was met.</param>
    private sealed record Resolution(ResolvedFont Font, string? Failure);
}

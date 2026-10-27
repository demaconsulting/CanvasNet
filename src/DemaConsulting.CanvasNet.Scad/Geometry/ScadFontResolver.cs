// cspell:ignore Liberation
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <summary>
///     Where a resolved font came from.
/// </summary>
internal enum ScadFontSource
{
    /// <summary>One of the Liberation fonts bundled with the core package.</summary>
    Bundled,

    /// <summary>A font file found in one of <see cref="ScadFontResolutionOptions.AdditionalDirectories"/>.</summary>
    AdditionalDirectory,

    /// <summary>A font installed on the host.</summary>
    System,
}

/// <summary>
///     The outcome of resolving a <c>font</c> argument.
/// </summary>
/// <param name="Font">The font to lay the text out with; never <see langword="null"/>.</param>
/// <param name="Source">Where the font came from.</param>
/// <param name="Warning">What the model author should be told (the font was not found or could not be loaded), or <see langword="null"/> when the request was met.</param>
internal sealed record ScadResolvedFont(TrueTypeFont Font, ScadFontSource Source, string? Warning);

/// <summary>
///     A parsed <c>font</c> argument: a family name and the requested bold and italic flags.
/// </summary>
/// <param name="Family">The family name; empty when the argument names no family (the default font is then used).</param>
/// <param name="Bold">Whether a bold face is requested.</param>
/// <param name="Italic">Whether an italic (or oblique) face is requested.</param>
internal readonly record struct ScadFontSpec(string Family, bool Bold, bool Italic);

/// <summary>
///     Resolves the <c>font</c> argument of <c>text()</c> to a font, deterministically and without ever failing.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why a resolver.</b> Font discovery scans directories and loading a font parses a file, so both are
///         done at most once per build: the resolver is owned by the build context, remembers the faces found in the
///         additional directories and every font it has loaded, and answers repeated requests from memory. It
///         reuses <see cref="SystemFontCatalog"/> for scanning and for the family and style scoring, so the
///         rules are the ones the rest of the library uses.
///     </para>
///     <para>
///         <b>Order.</b> No font argument (or one with no family) is the bundled Liberation Sans Regular (or the
///         requested style of it) whatever the host has, which keeps default text reproducible. A named family is
///         looked up in the additional directories, then (when enabled) the installed fonts, and then among the
///         bundled families (<c>Liberation Sans</c>, <c>Liberation Serif</c>, <c>Liberation Mono</c> and the generic
///         names <c>sans-serif</c>, <c>serif</c> and <c>monospace</c>). Only an exact family match counts (ignoring
///         case): the generic substitution of the catalog is deliberately not used, because it would make the result
///         depend on what the host has installed. A family that is found nowhere resolves to the bundled sans font
///         with a warning; so does a face that was found but cannot be loaded.
///     </para>
///     <para>
///         The resolver is used by one build on one thread and holds no state that outlives it.
///     </para>
/// </remarks>
internal sealed class ScadFontResolver
{
    /// <summary>The family name of the bundled sans-serif font, the default.</summary>
    private const string SansFamily = "Liberation Sans";

    /// <summary>The family name of the bundled serif font.</summary>
    private const string SerifFamily = "Liberation Serif";

    /// <summary>The family name of the bundled monospace font.</summary>
    private const string MonoFamily = "Liberation Mono";

    /// <summary>The options of the build.</summary>
    private readonly ScadFontResolutionOptions _options;

    /// <summary>The resolutions already made, by request.</summary>
    private readonly Dictionary<ScadFontSpec, ScadResolvedFont> _resolved = [];

    /// <summary>The fonts loaded from files, by path and face index.</summary>
    private readonly Dictionary<(string Path, int Face), TrueTypeFont?> _loaded = [];

    /// <summary>The faces of the additional directories; <see langword="null"/> until the first lookup.</summary>
    private IReadOnlyList<SystemFontInfo>? _additionalFaces;

    /// <summary>
    ///     Initializes a resolver.
    /// </summary>
    /// <param name="options">The font resolution options, or <see langword="null"/> for the defaults.</param>
    public ScadFontResolver(ScadFontResolutionOptions? options)
    {
        _options = options ?? new ScadFontResolutionOptions();
    }

    /// <summary>
    ///     Parses a <c>font</c> argument of the form <c>Family</c> or <c>Family:style=Bold Italic</c>.
    /// </summary>
    /// <remarks>
    ///     The text before the first colon is the family (trimmed). Each following colon-separated field of the form
    ///     <c>key=value</c> is examined; for the key <c>style</c> (any case) the words <c>Bold</c> and <c>Italic</c>
    ///     (or <c>Oblique</c>) set the flags, in any case and separated by white space or commas, and the other words
    ///     (<c>Regular</c>, <c>Normal</c>, ...) are ignored. Every other field is ignored.
    /// </remarks>
    /// <param name="font">The argument.</param>
    /// <returns>The parsed request.</returns>
    public static ScadFontSpec ParseSpec(string font)
    {
        var fields = font.Split(':');
        var bold = false;
        var italic = false;
        for (var i = 1; i < fields.Length; i++)
        {
            var separator = fields[i].IndexOf('=');
            if (separator < 0 || !fields[i][..separator].Trim().Equals("style", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var word in fields[i][(separator + 1)..].Split([' ', '\t', ','], StringSplitOptions.RemoveEmptyEntries))
            {
                bold |= word.Equals("bold", StringComparison.OrdinalIgnoreCase);
                italic |= word.Equals("italic", StringComparison.OrdinalIgnoreCase) || word.Equals("oblique", StringComparison.OrdinalIgnoreCase);
            }
        }

        return new ScadFontSpec(fields[0].Trim(), bold, italic);
    }

    /// <summary>
    ///     Resolves a request to a font.
    /// </summary>
    /// <remarks>
    ///     Never throws for a font that is missing or damaged: that resolves to the bundled sans font and a warning
    ///     for the caller to report. The result is the same every time for the same request within one build.
    /// </remarks>
    /// <param name="spec">The request; an empty family means the default font.</param>
    /// <returns>The font, where it came from, and the warning to report if any.</returns>
    public ScadResolvedFont Resolve(ScadFontSpec spec)
    {
        if (_resolved.TryGetValue(spec, out var known))
        {
            return known;
        }

        var result = ResolveUncached(spec);
        _resolved[spec] = result;
        return result;
    }

    /// <summary>
    ///     Resolves a request that is not in the cache.
    /// </summary>
    /// <param name="spec">The request.</param>
    /// <returns>The resolution.</returns>
    private ScadResolvedFont ResolveUncached(ScadFontSpec spec)
    {
        // No family: the default font, in the requested style, from the bundle only
        if (spec.Family.Length == 0)
        {
            return new ScadResolvedFont(SystemFontCatalog.LoadBundledFallback(false, false, spec.Bold, spec.Italic), ScadFontSource.Bundled, null);
        }

        // The additional directories come first, then the installed fonts; a face that cannot be loaded is reported
        string? failure = null;
        var fromDirectories = FindExact(AdditionalFaces(), spec);
        if (fromDirectories is not null)
        {
            var font = Load(fromDirectories.Value);
            if (font is not null)
            {
                return new ScadResolvedFont(font, ScadFontSource.AdditionalDirectory, null);
            }

            failure = $"the font file '{fromDirectories.Value.FilePath}' could not be loaded";
        }

        if (failure is null && _options.UseSystemFonts)
        {
            var installed = FindExact(SystemFontCatalog.Fonts, spec);
            if (installed is not null)
            {
                var font = Load(installed.Value);
                if (font is not null)
                {
                    return new ScadResolvedFont(font, ScadFontSource.System, null);
                }

                failure = $"the font file '{installed.Value.FilePath}' could not be loaded";
            }
        }

        // The bundled families
        if (failure is null && TryBundled(spec, out var bundled))
        {
            return new ScadResolvedFont(bundled!, ScadFontSource.Bundled, null);
        }

        var reason = failure ?? "no such font family was found";
        return new ScadResolvedFont(
            SystemFontCatalog.LoadBundledFallback(false, false, spec.Bold, spec.Italic),
            ScadFontSource.Bundled,
            $"The font '{spec.Family}' is not available ({reason}); using the bundled {SansFamily} font");
    }

    /// <summary>
    ///     Finds the face of a candidate list that has exactly the requested family, in the closest style.
    /// </summary>
    /// <param name="candidates">The candidates.</param>
    /// <param name="spec">The request.</param>
    /// <returns>The face, or <see langword="null"/> when no candidate has the family.</returns>
    private static SystemFontInfo? FindExact(IReadOnlyList<SystemFontInfo> candidates, ScadFontSpec spec)
    {
        // The catalog may answer with a generic substitute when the family is absent; only the family itself counts
        var match = SystemFontCatalog.FindBestMatchAmong(candidates, spec.Family, spec.Bold, spec.Italic, serif: false, fixedPitch: false);
        return match is not null && string.Equals(match.Value.FamilyName, spec.Family, StringComparison.OrdinalIgnoreCase) ? match : null;
    }

    /// <summary>
    ///     Gets the faces of the additional directories, scanning them on the first call.
    /// </summary>
    /// <returns>The faces; empty when there are no directories.</returns>
    private IReadOnlyList<SystemFontInfo> AdditionalFaces()
    {
        if (_additionalFaces is null)
        {
            _additionalFaces = _options.AdditionalDirectories.Count == 0
                ? []
                : SystemFontCatalog.DiscoverFonts(_options.AdditionalDirectories);
        }

        return _additionalFaces;
    }

    /// <summary>
    ///     Loads a face, remembering the outcome (a face that failed to load is not tried again).
    /// </summary>
    /// <param name="face">The face.</param>
    /// <returns>The font, or <see langword="null"/> when it cannot be loaded.</returns>
    private TrueTypeFont? Load(SystemFontInfo face)
    {
        var key = (face.FilePath, face.FaceIndex);
        if (_loaded.TryGetValue(key, out var cached))
        {
            return cached;
        }

        TrueTypeFont? font;
        try
        {
            font = TrueTypeFont.Load(face.FilePath, face.FaceIndex);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or OverflowException)
        {
            // A damaged, vanished or unreadable file is the model author's problem to hear about, not a crash
            font = null;
        }

        _loaded[key] = font;
        return font;
    }

    /// <summary>
    ///     Maps a family name to one of the bundled fonts.
    /// </summary>
    /// <param name="spec">The request.</param>
    /// <param name="font">The bundled font when the family is one of the bundled ones.</param>
    /// <returns><see langword="true"/> when the family names a bundled family or a generic family.</returns>
    private static bool TryBundled(ScadFontSpec spec, out TrueTypeFont? font)
    {
        font = null;
        var family = spec.Family;
        bool serif;
        bool mono;
        if (Is(family, SansFamily) || Is(family, "sans-serif") || Is(family, "sans"))
        {
            serif = false;
            mono = false;
        }
        else if (Is(family, SerifFamily) || Is(family, "serif"))
        {
            serif = true;
            mono = false;
        }
        else if (Is(family, MonoFamily) || Is(family, "monospace") || Is(family, "mono"))
        {
            serif = false;
            mono = true;
        }
        else
        {
            return false;
        }

        font = SystemFontCatalog.LoadBundledFallback(serif, mono, spec.Bold, spec.Italic);
        return true;
    }

    /// <summary>
    ///     Compares two names ignoring case.
    /// </summary>
    /// <param name="left">The first name.</param>
    /// <param name="right">The second name.</param>
    /// <returns><see langword="true"/> when they are equal ignoring case.</returns>
    private static bool Is(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}

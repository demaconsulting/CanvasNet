// cspell:ignore Liberation
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <summary>
///     The outcome of resolving a <c>font</c> argument.
/// </summary>
/// <param name="Font">The font to lay the text out with; never <see langword="null"/>.</param>
/// <param name="Source">Where the font came from.</param>
/// <param name="Warning">What the model author should be told (the font was not found or could not be loaded), or <see langword="null"/> when the request was met.</param>
internal sealed record ScadResolvedFont(TrueTypeFont Font, FontSource Source, string? Warning);

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
///         <b>Why a thin layer.</b> Finding a font is the job of the core <see cref="FontResolver"/>; this unit owns
///         what is specific to OpenSCAD: it parses the <c>font</c> argument (<see cref="ParseSpec"/>), spells the
///         OpenSCAD family aliases <c>sans</c> and <c>mono</c> as the generic keywords the resolver knows, turns a
///         failed lookup into the warning text shown to the model author, and maps the build's cancellation token and
///         scan bounds onto the resolver. The resolver is owned by the build context, so each build scans the
///         additional directories at most once and answers repeated requests from memory.
///     </para>
///     <para>
///         <b>Order.</b> No font argument (or one with no family) is the bundled Liberation Sans Regular (or the
///         requested style of it) whatever the host has, which keeps default text reproducible. A named family is
///         looked up in the additional directories, then (when enabled) the installed fonts, and then among the
///         bundled families (<c>Liberation Sans</c>, <c>Liberation Serif</c>, <c>Liberation Mono</c> and the generic
///         names <c>sans-serif</c>, <c>serif</c> and <c>monospace</c>, with <c>sans</c> and <c>mono</c>). Only an exact
///         family match counts (ignoring case): the generic substitution of the catalog is deliberately not used,
///         because it would make the result depend on what the host has installed, and the common aliases of other
///         systems (<c>Arial</c>, <c>Times New Roman</c>, ...) are not mapped, so an unknown name is reported. Within
///         a family the face whose bold and italic flags differ least from the request wins, and the first one found
///         wins a tie. A family that is found nowhere resolves to the bundled sans font with a warning; so does a
///         face that was found but cannot be loaded.
///     </para>
///     <para>
///         <b>Additional directories.</b> They are scanned as <see cref="FontDirectoryScanner"/> scans (recursively,
///         never following a link, in a deterministic order, bounded by <see cref="FontScanLimits"/>, header-only); a
///         missing, malformed or unreadable directory, and a file or face that does not parse, are skipped without a
///         diagnostic. The scan polls the cancellation token of the build, so a canceled build stops it with
///         <see cref="OperationCanceledException"/>. A selected face is loaded when it is selected, so a face whose
///         file is removed after the scan resolves to the bundled sans font with a warning.
///     </para>
///     <para>
///         The resolver is used by one build on one thread and holds no state that outlives it (but for the
///         process-wide font load cache of <see cref="SystemFontCatalog.Load"/>).
///     </para>
/// </remarks>
internal sealed class ScadFontResolver
{
    /// <summary>The core resolver that finds the font.</summary>
    private readonly FontResolver _resolver;

    /// <summary>
    ///     Initializes a resolver.
    /// </summary>
    /// <param name="options">The font resolution options, or <see langword="null"/> for the defaults.</param>
    /// <param name="cancellation">The token polled while the additional directories are scanned.</param>
    public ScadFontResolver(ScadFontResolutionOptions? options, CancellationToken cancellation = default)
        : this(options, cancellation, null)
    {
    }

    /// <summary>
    ///     Initializes a resolver with explicit scan bounds.
    /// </summary>
    /// <param name="options">The font resolution options, or <see langword="null"/> for the defaults.</param>
    /// <param name="cancellation">The token polled while the additional directories are scanned.</param>
    /// <param name="limits">The bounds of the scan of the additional directories, or <see langword="null"/> for the defaults of <see cref="FontScanLimits"/>.</param>
    public ScadFontResolver(ScadFontResolutionOptions? options, CancellationToken cancellation, FontScanLimits? limits)
    {
        options ??= new ScadFontResolutionOptions();
        _resolver = new FontResolver(
            new FontResolverOptions
            {
                AdditionalDirectories = options.AdditionalDirectories,
                UseSystemFonts = options.UseSystemFonts,
                MapCommonAliasesToBundled = false,
                ScanLimits = limits ?? new FontScanLimits(),
            },
            cancellation);
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
    /// <exception cref="OperationCanceledException">Thrown when the build is canceled while the additional directories are scanned.</exception>
    public ScadResolvedFont Resolve(ScadFontSpec spec)
    {
        // OpenSCAD's short names are the generic keywords of the core resolver
        var family = spec.Family;
        if (family.Equals("sans", StringComparison.OrdinalIgnoreCase))
        {
            family = "sans-serif";
        }
        else if (family.Equals("mono", StringComparison.OrdinalIgnoreCase))
        {
            family = "monospace";
        }

        var resolved = _resolver.Resolve(family, spec.Bold, spec.Italic, out var failure);
        var warning = failure is null
            ? null
            : $"The font '{spec.Family}' is not available ({failure}); using the bundled Liberation Sans font";
        return new ScadResolvedFont(resolved.Font, resolved.Source, warning);
    }
}

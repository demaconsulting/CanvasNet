using System.Security;
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Svg;

public static partial class SvgCodec
{
    // ================================================================================================
    // Default font resolution (no caller-registered font satisfies the font-family)
    // ================================================================================================

    /// <summary>
    ///     The numeric <c>font-weight</c> at or above which the default font resolution treats the
    ///     requested weight as "bold" - the conventional CSS threshold for choosing the bold member of
    ///     a two-weight (regular/bold) family.
    /// </summary>
    private const int BoldWeightThreshold = 600;

    /// <summary>
    ///     Resolves the font a <c>text</c> element renders with. Never returns <see langword="null"/>
    ///     and never throws for a missing font.
    /// </summary>
    /// <param name="context">
    ///     The per-document render context, supplying the caller-registered
    ///     <see cref="RenderContext.Fonts"/>, <see cref="RenderContext.UseSystemFonts"/>, and the
    ///     per-render resolution caches.
    /// </param>
    /// <param name="fontFamily">The raw, possibly comma-separated, possibly quoted <c>font-family</c> value.</param>
    /// <param name="requestedWeight">The cascaded <c>font-weight</c>.</param>
    /// <param name="requestedStyle">The cascaded <c>font-style</c>.</param>
    /// <returns>The resolved font.</returns>
    /// <remarks>
    ///     Resolution order:
    ///     <list type="number">
    ///         <item>
    ///             <description>
    ///                 A caller-registered font (<see cref="MatchFont"/>) - unchanged precedence and
    ///                 closest-face semantics.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Otherwise, for each family in the <c>font-family</c> list, in order: a generic
    ///                 family keyword maps to the bundled Liberation fonts (<c>sans-serif</c>,
    ///                 <c>system-ui</c>, <c>ui-sans-serif</c>, <c>cursive</c> and <c>fantasy</c> to
    ///                 Sans; <c>serif</c> and <c>ui-serif</c> to Serif; <c>monospace</c> and
    ///                 <c>ui-monospace</c> to Mono); any other name is looked up as an installed font
    ///                 with an exact (case-insensitive) family-name match, when
    ///                 <see cref="RenderContext.UseSystemFonts"/> is enabled.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Otherwise, the bundled Liberation Sans in the requested weight and style.
    ///             </description>
    ///         </item>
    ///     </list>
    ///     The default resolution is cached per render, keyed by the <c>font-family</c> text and the
    ///     bold/italic axes, and a given installed font file is loaded at most once per render.
    /// </remarks>
    private static TrueTypeFont ResolveFont(
        RenderContext context,
        string? fontFamily,
        int requestedWeight,
        SvgFontStyle requestedStyle)
    {
        if (context.Fonts != null)
        {
            var registered = MatchFont(fontFamily, requestedWeight, requestedStyle, context.Fonts);
            if (registered != null)
            {
                return registered;
            }
        }

        var bold = requestedWeight >= BoldWeightThreshold;
        var italic = requestedStyle == SvgFontStyle.Italic;
        var key = (fontFamily ?? string.Empty, bold, italic);
        if (context.ResolvedFontCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var resolved = ResolveDefaultFont(context, fontFamily, bold, italic);
        context.ResolvedFontCache[key] = resolved;
        return resolved;
    }

    /// <summary>
    ///     Performs the uncached default-font resolution described on <see cref="ResolveFont"/>
    ///     (everything after the caller-registered lookup).
    /// </summary>
    /// <param name="context">The per-document render context.</param>
    /// <param name="fontFamily">The raw <c>font-family</c> value, or <see langword="null"/>.</param>
    /// <param name="bold">Whether a bold face is requested.</param>
    /// <param name="italic">Whether an italic face is requested.</param>
    /// <returns>The resolved font.</returns>
    private static TrueTypeFont ResolveDefaultFont(RenderContext context, string? fontFamily, bool bold, bool italic)
    {
        if (!string.IsNullOrWhiteSpace(fontFamily))
        {
            foreach (var candidate in fontFamily.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var name = candidate.Trim('\'', '"').Trim();
                if (name.Length == 0)
                {
                    continue;
                }

                if (TryGetGenericFamily(name, out var serif, out var fixedPitch))
                {
                    return SystemFontCatalog.LoadBundledFallback(serif, fixedPitch, bold, italic);
                }

                if (context.UseSystemFonts)
                {
                    var installed = TryLoadInstalledFont(context, name, bold, italic);
                    if (installed != null)
                    {
                        return installed;
                    }
                }
            }
        }

        return SystemFontCatalog.LoadBundledFallback(serif: false, fixedPitch: false, bold, italic);
    }

    /// <summary>
    ///     Maps a CSS generic font-family keyword onto the bundled Liberation family axes.
    /// </summary>
    /// <param name="name">The unquoted family name.</param>
    /// <param name="serif"><see langword="true"/> when the keyword selects the serif family.</param>
    /// <param name="fixedPitch"><see langword="true"/> when the keyword selects the monospace family.</param>
    /// <returns><see langword="true"/> when <paramref name="name"/> is a recognized generic family keyword.</returns>
    private static bool TryGetGenericFamily(string name, out bool serif, out bool fixedPitch)
    {
        serif = false;
        fixedPitch = false;

        if (name.Equals("sans-serif", StringComparison.OrdinalIgnoreCase)
            || name.Equals("system-ui", StringComparison.OrdinalIgnoreCase)
            || name.Equals("ui-sans-serif", StringComparison.OrdinalIgnoreCase)
            || name.Equals("cursive", StringComparison.OrdinalIgnoreCase)
            || name.Equals("fantasy", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (name.Equals("serif", StringComparison.OrdinalIgnoreCase)
            || name.Equals("ui-serif", StringComparison.OrdinalIgnoreCase))
        {
            serif = true;
            return true;
        }

        if (name.Equals("monospace", StringComparison.OrdinalIgnoreCase)
            || name.Equals("ui-monospace", StringComparison.OrdinalIgnoreCase))
        {
            fixedPitch = true;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Loads an installed font whose family name exactly (case-insensitively) equals
    ///     <paramref name="familyName"/>, choosing its closest bold/italic style.
    /// </summary>
    /// <param name="context">The per-document render context, owning the loaded-file cache.</param>
    /// <param name="familyName">The unquoted family name.</param>
    /// <param name="bold">Whether a bold face is requested.</param>
    /// <param name="italic">Whether an italic face is requested.</param>
    /// <returns>
    ///     The loaded font, or <see langword="null"/> when no installed font has that family name or
    ///     the matching font file cannot be loaded.
    /// </returns>
    /// <remarks>
    ///     <see cref="SystemFontCatalog.FindBestMatch"/> falls back to a generic-family bucket when
    ///     the requested family is not installed; that substitution is deliberately rejected here
    ///     (the result's family name must equal the requested one) so the <c>font-family</c> list's
    ///     next entry - and ultimately the bundled fallback - gets its turn instead.
    /// </remarks>
    private static TrueTypeFont? TryLoadInstalledFont(RenderContext context, string familyName, bool bold, bool italic)
    {
        var match = SystemFontCatalog.FindBestMatch(familyName, bold, italic, serif: false, fixedPitch: false);
        if (match is not { } info || !string.Equals(info.FamilyName, familyName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var fileKey = (info.FilePath, info.FaceIndex);
        if (context.InstalledFontCache.TryGetValue(fileKey, out var cached))
        {
            return cached;
        }

        TrueTypeFont? font;
        try
        {
            font = TrueTypeFont.Load(info.FilePath, info.FaceIndex);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException or OverflowException or SecurityException)
        {
            // A vanished, unreadable or damaged font file is tolerated: the caller moves on to the
            // next family (and the failure is remembered so the file is not re-read)
            font = null;
        }

        context.InstalledFontCache[fileKey] = font;
        return font;
    }
}

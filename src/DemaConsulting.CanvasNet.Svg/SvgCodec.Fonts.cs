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
    ///                 Otherwise the core <see cref="FontResolver"/> resolves the <c>font-family</c> list:
    ///                 for each family, in order, a generic family keyword maps to the bundled Liberation
    ///                 fonts (<c>sans-serif</c>, <c>system-ui</c>, <c>ui-sans-serif</c>, <c>cursive</c> and
    ///                 <c>fantasy</c> to Sans; <c>serif</c> and <c>ui-serif</c> to Serif; <c>monospace</c>
    ///                 and <c>ui-monospace</c> to Mono); any other name is looked up as an installed font
    ///                 with an exact (case-insensitive) family-name match, when
    ///                 <see cref="RenderContext.UseSystemFonts"/> is enabled; and a bundled family name
    ///                 (<c>Liberation Sans</c>, <c>Liberation Serif</c>, <c>Liberation Mono</c>) that no
    ///                 installed font has maps to that bundled font. Quoted names may contain commas.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Otherwise, the bundled Liberation Sans in the requested weight and style.
    ///             </description>
    ///         </item>
    ///     </list>
    ///     The default resolution is cached per render by the render context's resolver, keyed by the
    ///     <c>font-family</c> text and the bold/italic axes, and a given installed font file is loaded at
    ///     most once per process (<see cref="SystemFontCatalog.Load"/>).
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
        return context.DefaultFonts.Resolve(fontFamily, bold, italic, out _).Font;
    }
}

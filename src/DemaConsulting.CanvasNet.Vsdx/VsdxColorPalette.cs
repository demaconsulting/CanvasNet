using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio Rgba Themed THEMEVAL davehoward Foregnd

/// <summary>
///     Resolves a VisioML color cell's raw <c>V</c> value into an <see cref="Rgba32"/>, handling
///     the three literal forms observed across the in-scope fixtures: a hexadecimal color literal
///     (<c>#RRGGBB</c>/<c>#AARRGGBB</c>), a small non-negative integer indexing the built-in
///     24-entry Visio color palette, and the sentinel literal <c>"Themed"</c> (a document-theme
///     color reference, resolved against a parsed <see cref="VsdxTheme"/> when the cell's own
///     formula carries a recognized <c>THEMEVAL("slotName")</c> reference - see
///     <see cref="Resolve(string?, string?, VsdxTheme?, Rgba32)"/>'s own remarks - and falling
///     back to <see cref="ThemedFallback"/> otherwise).
/// </summary>
internal static class VsdxColorPalette
{
    /// <summary>
    ///     The 12 canonical DrawingML <c>&lt;a:clrScheme&gt;</c> slot names recognized by the
    ///     narrow <c>THEMEVAL("slotName")</c> text match in <see cref="Resolve(string?, string?, VsdxTheme?, Rgba32)"/>
    ///     - see that overload's own remarks for why this is deliberately a literal substring
    ///     match, not a general formula parser.
    /// </summary>
    private static readonly string[] CanonicalThemeSlotNames =
    [
        "dk1", "lt1", "dk2", "lt2",
        "accent1", "accent2", "accent3", "accent4", "accent5", "accent6",
        "hlink", "folHlink"
    ];


    /// <summary>
    ///     The built-in Visio color-index palette (indices <c>0</c>-<c>23</c>), sourced from the
    ///     publicly documented <c>RGB</c> ShapeSheet function reference
    ///     (<c>https://learn.microsoft.com/en-us/office/client-developer/visio/rgb-function-visioshapesheet</c>).
    ///     Indices <c>0</c> (black) and <c>1</c> (white) are directly corroborated by sample
    ///     content (<c>davehoward-test9-rect-and-line.vsdx</c>'s document.xml, built-in
    ///     <c>StyleSheet ID="0"</c>: <c>LineColor V="0"</c>, <c>FillForegnd V="1"</c>); indices
    ///     <c>2</c>-<c>23</c> are not directly exercised by any in-scope fixture and are flagged
    ///     here as unverified-by-sample (per the originating plan report's Assumption #1) -
    ///     included for completeness and forward compatibility, not required for this milestone's
    ///     own passing tests.
    /// </summary>
    private static readonly Rgba32[] BuiltInPalette =
    [
        new Rgba32(0, 0, 0, 255), // 0 Black
        new Rgba32(255, 255, 255, 255), // 1 White
        new Rgba32(255, 0, 0, 255), // 2 Red
        new Rgba32(0, 255, 0, 255), // 3 Green
        new Rgba32(0, 0, 255, 255), // 4 Blue
        new Rgba32(255, 255, 0, 255), // 5 Yellow
        new Rgba32(255, 0, 255, 255), // 6 Magenta
        new Rgba32(0, 255, 255, 255), // 7 Cyan
        new Rgba32(128, 0, 0, 255), // 8 Dark Red
        new Rgba32(0, 128, 0, 255), // 9 Dark Green
        new Rgba32(0, 0, 128, 255), // 10 Dark Blue
        new Rgba32(128, 128, 0, 255), // 11 Olive
        new Rgba32(128, 0, 128, 255), // 12 Purple
        new Rgba32(0, 128, 128, 255), // 13 Teal
        new Rgba32(192, 192, 192, 255), // 14 Gray
        new Rgba32(128, 128, 128, 255), // 15 Silver
        new Rgba32(255, 128, 0, 255), // 16 Orange
        new Rgba32(128, 64, 0, 255), // 17 Brown
        new Rgba32(255, 192, 0, 255), // 18 Gold
        new Rgba32(255, 255, 128, 255), // 19 Light Yellow
        new Rgba32(255, 128, 255, 255), // 20 Light Magenta
        new Rgba32(128, 255, 255, 255), // 21 Light Cyan
        new Rgba32(128, 255, 128, 255), // 22 Light Green
        new Rgba32(128, 128, 255, 255) // 23 Light Blue
    ];

    /// <summary>
    ///     The neutral fallback color substituted for the literal sentinel value <c>"Themed"</c>.
    /// </summary>
    /// <remarks>
    ///     A document-theme color reference requires resolving the document's theme part
    ///     (<c>visio/theme/theme1.xml</c>) and its color-scheme slot indirection - see
    ///     <see cref="Resolve(string?, string?, VsdxTheme?, Rgba32)"/> (Milestone 6) for the
    ///     narrow, formula-aware resolution path. This fallback remains the terminal case for
    ///     every "Themed" cell that path does not resolve (no theme part, an unparseable theme, a
    ///     bare <c>THEMEVAL()</c>, or a Visio-internal QuickStyle role-name argument rather than a
    ///     canonical clrScheme slot - overwhelmingly the common real-fixture case). Rather than
    ///     throwing (as the originating Milestone-4 plan report's Assumption #1 had proposed),
    ///     this falls back to a neutral mid-gray, consistent with this package's own convention
    ///     that an unresolved/unsupported color construct "falls back to a
    ///     neutral color" rather than failing the whole parse. This is a deliberate,
    ///     evidence-based deviation from that earlier plan:
    ///     direct inspection of <c>davehoward-test12-colors.vsdx</c>'s <c>visio/document.xml</c>
    ///     shows the built-in <c>StyleSheet ID="3"</c> ("Normal" - the default style nearly every
    ///     ordinary shape resolves to) declares no <c>LineColor</c>/<c>FillForegnd</c>/
    ///     <c>FillPattern</c> cells of its own at all, falling through to
    ///     <c>StyleSheet ID="6"</c> ("Theme"), whose own cells are the literal, terminal value
    ///     <c>V="Themed" F="THEMEVAL()"</c> (not <c>F="Inh"</c>, so this is not a cached copy to
    ///     walk past - it is genuinely the resolved value). Since nearly every ordinary shape in
    ///     every fixture resolves to "Themed" for at least one paint cell unless it carries a
    ///     direct per-shape override, throwing here would fail essentially every fixture's shape
    ///     resolution, contradicting this milestone's explicit instruction to defer Themed
    ///     resolution gracefully rather than reject it.
    /// </remarks>
    public static readonly Rgba32 ThemedFallback = new(128, 128, 128, 255);

    /// <summary>
    ///     Resolves a color cell's raw <c>V</c> value into an <see cref="Rgba32"/>.
    /// </summary>
    /// <param name="rawValue">The cell's raw <c>V</c> value, or <see langword="null"/>/empty when the cell is entirely absent.</param>
    /// <param name="fallback">The color to return when <paramref name="rawValue"/> is absent, empty, or not recognized in any of the three supported forms.</param>
    /// <returns>
    ///     The resolved <see cref="Rgba32"/>: a direct hex-literal parse, a built-in palette
    ///     lookup, <see cref="ThemedFallback"/> for the literal <c>"Themed"</c> sentinel, or
    ///     <paramref name="fallback"/> when none of those apply. Never throws.
    /// </returns>
    public static Rgba32 Resolve(string? rawValue, Rgba32 fallback) => Resolve(rawValue, formula: null, theme: null, fallback);

    /// <summary>
    ///     Resolves a color cell's raw <c>V</c> value into an <see cref="Rgba32"/>, additionally
    ///     resolving the literal sentinel <c>"Themed"</c> against a parsed document theme when
    ///     <paramref name="formula"/> carries a narrowly-recognized <c>THEMEVAL("slotName")</c>
    ///     reference to one of the 12 canonical DrawingML <c>&lt;a:clrScheme&gt;</c> slot names.
    /// </summary>
    /// <remarks>
    ///     This is deliberately a narrow, literal substring match - not a general VisioML formula
    ///     evaluator. A bare <c>THEMEVAL()</c> call (no argument), a call whose argument is a
    ///     Visio-internal QuickStyle role name (for example <c>"FillColor"</c>, <c>"VariantColor3"</c>
    ///     - neither of which is a DrawingML scheme slot name), a more complex formula wrapping
    ///     <c>THEMEVAL(...)</c> in <c>IF</c>/<c>SHADE</c>/<c>TINT</c>, or any
    ///     <paramref name="theme"/> that is itself <see langword="null"/> (no theme part, or an
    ///     unparseable one) all fall through to <see cref="ThemedFallback"/> exactly as the 2-arg
    ///     <see cref="Resolve(string?, Rgba32)"/> overload already does - zero behavior change for
    ///     every existing passing test. See the originating plan report's exhaustive fixture-grep
    ///     evidence: no real <c>.vsdx</c> fixture's own <c>THEMEVAL(...)</c> argument is ever one
    ///     of the 12 canonical slot names, so this path is exercised only by a hand-authored
    ///     synthetic test - it exists for forward-compatibility/design-doc literal compliance, not
    ///     because any real sample needs it.
    /// </remarks>
    /// <param name="rawValue">The cell's raw <c>V</c> value, or <see langword="null"/>/empty when the cell is entirely absent.</param>
    /// <param name="formula">The same cell's raw <c>F</c> value, or <see langword="null"/> when the cell carries no formula (or theme-awareness is not required by the caller).</param>
    /// <param name="theme">The document's parsed theme, or <see langword="null"/> when the document has no usable theme.</param>
    /// <param name="fallback">The color to return when <paramref name="rawValue"/> is absent, empty, or not recognized in any of the supported forms.</param>
    /// <returns>
    ///     The resolved <see cref="Rgba32"/>: a direct hex-literal parse, a built-in palette
    ///     lookup, a resolved theme-scheme-slot color, <see cref="ThemedFallback"/> for an
    ///     unmatched <c>"Themed"</c> sentinel, or <paramref name="fallback"/> when none of those
    ///     apply. Never throws.
    /// </returns>
    public static Rgba32 Resolve(string? rawValue, string? formula, VsdxTheme? theme, Rgba32 fallback)
    {
        if (string.IsNullOrEmpty(rawValue))
        {
            return fallback;
        }

        if (string.Equals(rawValue, "Themed", StringComparison.Ordinal))
        {
            if (theme is not null && formula is not null && TryMatchThemeValSlot(formula, theme, out var themeColor))
            {
                return themeColor;
            }

            return ThemedFallback;
        }

        if (rawValue[0] == '#' && Rgba32.TryParse(rawValue, out var hexColor))
        {
            return hexColor;
        }

        if (int.TryParse(rawValue, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var index) &&
            index >= 0 &&
            index < BuiltInPalette.Length)
        {
            return BuiltInPalette[index];
        }

        return fallback;
    }

    /// <summary>
    ///     Searches <paramref name="formula"/> for the exact literal substring
    ///     <c>THEMEVAL("slotName")</c> (double-quoted, case-sensitive) for one of the 12 canonical
    ///     clrScheme slot names, resolving it against <paramref name="theme"/> when found - see
    ///     <see cref="Resolve(string?, string?, VsdxTheme?, Rgba32)"/>'s own remarks for why this
    ///     is a narrow substring match rather than a formula parser.
    /// </summary>
    /// <param name="formula">The cell's raw <c>F</c> value to search.</param>
    /// <param name="theme">The document's parsed theme.</param>
    /// <param name="color">The matched slot's resolved color, when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="formula"/> contains a recognized <c>THEMEVAL("slotName")</c> reference whose slot resolves to a non-null color on <paramref name="theme"/>.</returns>
    private static bool TryMatchThemeValSlot(string formula, VsdxTheme theme, out Rgba32 color)
    {
        foreach (var slotName in CanonicalThemeSlotNames)
        {
            var needle = $"THEMEVAL(\"{slotName}\")";
            if (formula.Contains(needle, StringComparison.Ordinal) && theme.TryGetSlot(slotName, out color))
            {
                return true;
            }
        }

        color = default;
        return false;
    }
}

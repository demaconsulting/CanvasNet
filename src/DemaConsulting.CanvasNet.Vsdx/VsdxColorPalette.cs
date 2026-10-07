using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio Rgba Themed THEMEVAL davehoward Foregnd

/// <summary>
///     Resolves a VisioML color cell's raw <c>V</c> value into an <see cref="Rgba32"/>, handling
///     the three literal forms observed across the in-scope fixtures: a hexadecimal color literal
///     (<c>#RRGGBB</c>/<c>#AARRGGBB</c>), a small non-negative integer indexing the built-in
///     24-entry Visio color palette, and the sentinel literal <c>"Themed"</c> (a document-theme
///     color reference, deferred in full to Milestone 6 - see <see cref="ThemedFallback"/>'s own
///     remarks).
/// </summary>
internal static class VsdxColorPalette
{
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
    ///     (<c>visio/theme/theme1.xml</c>) and its color-scheme slot indirection - explicitly
    ///     deferred to Milestone 6 by the Implementation Phase Plan. Rather than throwing (as the
    ///     originating plan report's Assumption #1 had proposed), this falls back to a neutral
    ///     mid-gray, per <c>canvas-net-vsdx.md</c>'s Risk Control Measures language that an
    ///     unresolved/unsupported color construct "falls back to a neutral color" rather than
    ///     failing the whole parse. This is a deliberate, evidence-based deviation from the plan:
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
    public static Rgba32 Resolve(string? rawValue, Rgba32 fallback)
    {
        if (string.IsNullOrEmpty(rawValue))
        {
            return fallback;
        }

        if (string.Equals(rawValue, "Themed", StringComparison.Ordinal))
        {
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
}

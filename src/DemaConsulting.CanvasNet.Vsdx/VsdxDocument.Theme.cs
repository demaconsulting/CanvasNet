namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio

/// <summary>
///     Implements the <see cref="VsdxDocument"/> theme loader: lazily resolves and parses
///     <c>visio/theme/theme1.xml</c> (via the already-resolved <c>_themePartPath</c> - see
///     <c>VsdxDocument.Pages.cs</c>) into a <see cref="VsdxTheme"/>, caching the result (including
///     a cached <see langword="null"/> when the package declares no theme relationship, or its
///     theme part cannot be parsed).
/// </summary>
public sealed partial class VsdxDocument
{
    /// <summary>
    ///     The cached, parsed theme, populated lazily by <see cref="GetTheme"/>.
    ///     <see langword="null"/> both before first access and when the package has no usable
    ///     theme - disambiguated from "not yet loaded" by <see cref="_themeLoaded"/>.
    /// </summary>
    private VsdxTheme? _theme;

    /// <summary>Set once <see cref="GetTheme"/> has attempted to resolve/parse the theme, so a <see langword="null"/> result is not re-attempted on every call.</summary>
    private bool _themeLoaded;

    /// <summary>
    ///     Returns this document's parsed theme, resolving and parsing
    ///     <c>visio/theme/theme1.xml</c> on first access and caching the result.
    /// </summary>
    /// <remarks>
    ///     Never throws: a package with no <c>theme</c> relationship, or whose theme part is
    ///     missing/not well-formed XML, resolves to <see langword="null"/> - mirroring
    ///     <c>VsdxDocument.Masters.cs</c>'s own tolerant <c>LoadMasterShape</c>/
    ///     <c>GetMastersIndex</c> pattern for an equally optional, auxiliary part, and
    ///     <c>canvas-net-vsdx.md</c>'s Risk Control Measures mandate that color resolution never
    ///     fail the whole parse for a missing/malformed theme.
    /// </remarks>
    /// <returns>The parsed <see cref="VsdxTheme"/>, or <see langword="null"/> when the package declares no theme relationship, or its theme part cannot be resolved or parsed.</returns>
    private VsdxTheme? GetTheme()
    {
        if (_themeLoaded)
        {
            return _theme;
        }

        _themeLoaded = true;

        if (_themePartPath is null)
        {
            return null;
        }

        try
        {
            var root = LoadPartXmlRoot(_themePartPath);
            _theme = VsdxTheme.Parse(root);
        }
        catch (InvalidDataException)
        {
            _theme = null;
        }

        return _theme;
    }
}

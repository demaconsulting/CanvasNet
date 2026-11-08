// cspell:ignore Hhea hmtx Hmtx Loca Maxp rlh vmin vmax rem dasharray dashoffset
using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Svg.Tests;

/// <summary>
///     Shared helpers for the SVG length-unit and font-size test classes: stream conversion, a
///     synthetic square-glyph font, and pixel measurements that turn a rendered result back into a
///     length (so a test asserts "this attribute rendered N user units wide", not "a magic pixel
///     is on").
/// </summary>
internal static class SvgLengthTestSupport
{
    /// <summary>Wraps <paramref name="svg"/> as a UTF-8 stream for <see cref="SvgCodec"/> to read.</summary>
    /// <param name="svg">The SVG text.</param>
    /// <returns>A readable stream over the text.</returns>
    internal static MemoryStream ToStream(string svg) => new(Encoding.UTF8.GetBytes(svg));

    /// <summary>
    ///     Builds a synthetic font with one mapped glyph, <c>A</c>: a 50-unit-high rectangle
    ///     (<paramref name="glyphWidth"/> units wide, 50 by default so it is a square) on a 100-unit
    ///     em-square, so at font size <c>S</c> the default glyph is an <c>S/2</c> square. The font
    ///     has no <c>x</c> or <c>0</c> glyph, which makes the <c>ex</c>/<c>ch</c> fallback testable.
    /// </summary>
    /// <param name="glyphWidth">The glyph's width in font units; a different width gives a distinguishable "bold" face.</param>
    /// <returns>The synthetic font.</returns>
    internal static TrueTypeFont BuildSquareFont(int glyphWidth = 50)
    {
        var square = SyntheticFontBuilder.SimpleGlyph(
        [
            [(0, 0, true), (glyphWidth, 0, true), (glyphWidth, 50, true), (0, 50, true)]
        ]);

        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(100, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(2))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(100, 0, 0, 2))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([0, 100]))
            .AddTable("loca", SyntheticFontBuilder.Loca([0, square.Length], longFormat: false))
            .AddTable("glyf", square)
            .AddTable("cmap", SyntheticFontBuilder.CmapFormat4(3, 1, [(65, 1)]))
            .Build();

        using var stream = new MemoryStream(data);
        return TrueTypeFont.Load(stream);
    }

    /// <summary>Gets the font dictionary used by the text tests: family <c>TestFont</c> mapped to <see cref="BuildSquareFont"/>.</summary>
    /// <returns>The font dictionary.</returns>
    internal static Dictionary<string, TrueTypeFont> TestFonts() => new() { ["TestFont"] = BuildSquareFont() };

    /// <summary>Renders <paramref name="svg"/> 1:1 onto a surface of the given size using the synthetic test font.</summary>
    /// <param name="svg">The SVG text.</param>
    /// <param name="width">The surface width.</param>
    /// <param name="height">The surface height.</param>
    /// <returns>The rendered surface.</returns>
    internal static Surface Render(string svg, int width, int height)
    {
        using var stream = ToStream(svg);
        return SvgCodec.Load(stream, width, height, TestFonts());
    }

    /// <summary>
    ///     Renders <paramref name="svg"/> 1:1 with a two-face <c>TestFont</c> family: a regular
    ///     (weight 400) square-glyph face and a bold (weight 700) face whose glyph is 80 rather than
    ///     50 units wide, so which weight actually applied is visible in the ink area.
    /// </summary>
    /// <param name="svg">The SVG text.</param>
    /// <param name="width">The surface width.</param>
    /// <param name="height">The surface height.</param>
    /// <returns>The rendered surface.</returns>
    internal static Surface RenderWithFaces(string svg, int width, int height)
    {
        var faces = new Dictionary<string, IReadOnlyList<SvgFontFace>>
        {
            ["TestFont"] =
            [
                new SvgFontFace(BuildSquareFont(), Weight: 400),
                new SvgFontFace(BuildSquareFont(80), Weight: 700)
            ]
        };

        using var stream = ToStream(svg);
        return SvgCodec.LoadWithFontFaces(stream, width, height, faces);
    }

    /// <summary>Sums a row's coverage (alpha / 255), giving the ink width of a horizontal bar in pixels with sub-pixel accuracy.</summary>
    /// <param name="surface">The rendered surface.</param>
    /// <param name="y">The row to measure.</param>
    /// <returns>The row's total coverage in pixels.</returns>
    internal static double RowCoverage(Surface surface, int y)
    {
        var sum = 0.0;
        for (var x = 0; x < surface.Width; x++)
        {
            sum += surface[x, y].A / 255.0;
        }

        return sum;
    }

    /// <summary>Sums a column's coverage (alpha / 255), giving the ink height of a vertical bar in pixels with sub-pixel accuracy.</summary>
    /// <param name="surface">The rendered surface.</param>
    /// <param name="x">The column to measure.</param>
    /// <returns>The column's total coverage in pixels.</returns>
    internal static double ColumnCoverage(Surface surface, int x)
    {
        var sum = 0.0;
        for (var y = 0; y < surface.Height; y++)
        {
            sum += surface[x, y].A / 255.0;
        }

        return sum;
    }

    /// <summary>Sums the whole surface's coverage (alpha / 255): the ink area in pixels.</summary>
    /// <param name="surface">The rendered surface.</param>
    /// <returns>The total coverage.</returns>
    internal static double TotalCoverage(Surface surface)
    {
        var sum = 0.0;
        for (var y = 0; y < surface.Height; y++)
        {
            sum += RowCoverage(surface, y);
        }

        return sum;
    }

    /// <summary>
    ///     Measures the effective font size of a document's single <c>A</c> glyph (rendered in the
    ///     synthetic font) from the ink area (the glyph is an <c>S/2</c> square, so its area is
    ///     <c>S^2/4</c>).
    /// </summary>
    /// <param name="svg">
    ///     A full SVG document whose only ink is one <c>A</c> glyph in the synthetic
    ///     <c>TestFont</c>.
    /// </param>
    /// <param name="width">The surface width (the document should use a matching <c>viewBox</c>).</param>
    /// <param name="height">The surface height.</param>
    /// <returns>The measured font size in user units.</returns>
    internal static double MeasureFontSize(string svg, int width = 200, int height = 200) =>
        2.0 * Math.Sqrt(TotalCoverage(Render(svg, width, height)));

    /// <summary>
    ///     Wraps <paramref name="inner"/> in a 200x200 document and measures the font size of the
    ///     single <c>A</c> glyph it renders.
    /// </summary>
    /// <param name="inner">The markup placed inside the root <c>svg</c> element.</param>
    /// <param name="rootAttributes">Extra attributes for the root element (for example <c>font-size="10"</c>).</param>
    /// <returns>The measured font size.</returns>
    internal static double MeasureFontSizeOf(string inner, string rootAttributes = "") =>
        MeasureFontSize($"<svg viewBox='0 0 200 200' {rootAttributes}>{inner}</svg>");

    /// <summary>Builds a document with a single <c>A</c> text element carrying <paramref name="textAttributes"/>.</summary>
    /// <param name="textAttributes">Attributes placed on the <c>text</c> element.</param>
    /// <returns>The inner markup.</returns>
    internal static string Glyph(string textAttributes) =>
        $"<text x='20' y='150' font-family='TestFont' {textAttributes}>A</text>";
}

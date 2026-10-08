// cspell:ignore vsdx Visio Foregnd

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

/// <summary>
///     Unit-level tests for <see cref="VsdxColorPalette"/>'s built-in 24-entry Visio color-index
///     palette and direct hex-literal resolution, exercised through the public
///     <see cref="VsdxDocument"/> API (a shape's resolved <see cref="VsdxShapeNode.Paint"/>),
///     closing the <c>CanvasNetVsdx-VsdxDocument-ColorTableResolution</c> requirement's own
///     gap identified by the Milestone 8 planning report. See
///     <see cref="VsdxColorPalette.Resolve(string?, string?, VsdxTheme?, Canvas.Rgba32)"/>'s own
///     remarks for why this is only a built-in-index/hex-literal resolver, not a
///     document-declared <c>&lt;Colors&gt;</c> table (never parsed anywhere in this package's
///     source - see the Milestone 8 correction note on <c>CanvasNetVsdx-VsdxDocument-ColorTableResolution</c>).
/// </summary>
public class VsdxColorPaletteTests
{
    /// <summary>A minimal built-in-style-shaped <c>&lt;StyleSheets&gt;</c> block whose ID 0 declares no paint cells of its own, so every test's shape-level cells resolve directly rather than being masked by a StyleSheet default.</summary>
    private const string StyleSheetsXml =
        """
        <StyleSheets>
          <StyleSheet ID="0" Name="No Style">
          </StyleSheet>
        </StyleSheets>
        """;

    /// <summary>
    ///     Proves a <c>FillForegnd</c> cell holding a small non-negative integer resolves against
    ///     the built-in 24-entry Visio color-index palette (index <c>2</c>, documented as pure
    ///     red) rather than being treated as a literal hex value or falling back to the neutral
    ///     default.
    /// </summary>
    [Fact]
    public void VsdxColorPalette_BuiltInPaletteIndex_ResolvesDocumentedRgbValue()
    {
        // Arrange: FillForegnd="2" - the built-in palette's documented "Red" entry.
        var shapeXml =
            """
            <Shape ID="1" Type="Shape" LineStyle="0" FillStyle="0">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Cell N="FillForegnd" V="2"/><Cell N="FillPattern" V="1"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml, StyleSheetsXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var paint = document.GetPageShapes(0)[0].Paint!;

        // Assert
        Assert.Equal(255, paint.FillColor.R);
        Assert.Equal(0, paint.FillColor.G);
        Assert.Equal(0, paint.FillColor.B);
    }

    /// <summary>
    ///     Proves a <c>LineColor</c> cell holding a direct <c>#RRGGBB</c> hex literal resolves
    ///     literally, complementing <see cref="VsdxColorPalette_BuiltInPaletteIndex_ResolvesDocumentedRgbValue"/>'s
    ///     palette-index coverage - both forms must resolve to the same effective color type.
    /// </summary>
    [Fact]
    public void VsdxColorPalette_DirectHexValue_ResolvesLiterally()
    {
        // Arrange
        var shapeXml =
            """
            <Shape ID="1" Type="Shape" LineStyle="0" FillStyle="0">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Cell N="LineColor" V="#abcdef"/><Cell N="LinePattern" V="1"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml, StyleSheetsXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var paint = document.GetPageShapes(0)[0].Paint!;

        // Assert
        Assert.Equal(0xab, paint.StrokeColor.R);
        Assert.Equal(0xcd, paint.StrokeColor.G);
        Assert.Equal(0xef, paint.StrokeColor.B);
    }
}

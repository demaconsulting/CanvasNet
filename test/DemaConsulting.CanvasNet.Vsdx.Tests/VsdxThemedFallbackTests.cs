using DemaConsulting.CanvasNet.Canvas;

// cspell:ignore vsdx Visio Foregnd Themed THEMEVAL

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

/// <summary>
///     Tests for the unthemed fallback of a host-cell <c>THEMEVAL()</c> value: a cell resolving
///     to the literal sentinel <c>"Themed"</c> (for example through the built-in "Theme"
///     StyleSheet, ID <c>6</c>) takes the built-in "No Style" StyleSheet's (ID <c>0</c>) value for
///     the same cell - black line and text, white solid fill - instead of a neutral gray.
///     All packages are synthetic, hand-built XML.
/// </summary>
public class VsdxThemedFallbackTests
{
    /// <summary>
    ///     A <c>&lt;StyleSheets&gt;</c> block mirroring the built-in "No Style"/"Normal"/"Theme"
    ///     chain: ID <c>3</c> chains to ID <c>6</c>, whose paint and character cells are the
    ///     literal <c>"Themed"</c> sentinel; ID <c>0</c> supplies the unthemed base values.
    /// </summary>
    private const string StyleSheetsXml =
        """
        <StyleSheets>
          <StyleSheet ID="0" Name="No Style">
            <Cell N="LineColor" V="0"/><Cell N="LinePattern" V="1"/><Cell N="LineWeight" V="0.0104"/>
            <Cell N="FillForegnd" V="1"/><Cell N="FillPattern" V="1"/>
            <Section N="Character"><Row IX="0"><Cell N="Color" V="0"/><Cell N="Size" V="0.1667"/></Row></Section>
          </StyleSheet>
          <StyleSheet ID="3" Name="Normal" LineStyle="6" FillStyle="6" TextStyle="6">
          </StyleSheet>
          <StyleSheet ID="6" Name="Theme" LineStyle="0" FillStyle="0" TextStyle="0">
            <Cell N="LineColor" V="Themed" F="THEMEVAL()"/><Cell N="LinePattern" V="Themed" F="THEMEVAL()"/>
            <Cell N="LineWeight" V="Themed" F="THEMEVAL()"/>
            <Cell N="FillForegnd" V="Themed" F="THEMEVAL()"/><Cell N="FillPattern" V="Themed" F="THEMEVAL()"/>
            <Section N="Character"><Row IX="0"><Cell N="Color" V="Themed" F="THEMEVAL()"/></Row></Section>
          </StyleSheet>
        </StyleSheets>
        """;

    /// <summary>Builds a one-shape package whose shape resolves styles through ID <c>3</c>.</summary>
    private static VsdxDocument OpenShape(string extraShapeXml = "")
    {
        var shapeXml =
            $"""
            <Shape ID="1" Type="Shape" LineStyle="3" FillStyle="3" TextStyle="3">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              {extraShapeXml}
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
              <Text>Hello</Text>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml, StyleSheetsXml);
        return VsdxDocument.Open(stream);
    }

    /// <summary>Themed line and fill cells inherited through the "Theme" style resolve to the "No Style" values.</summary>
    [Fact]
    public void ThemedPaint_InheritedFromThemeStyle_ResolvesToNoStyleValues()
    {
        using var document = OpenShape();

        var paint = document.GetPageShapes(0)[0].Paint!;

        Assert.True(paint.HasLine);
        Assert.Equal(new Rgba32(0, 0, 0, 255), paint.StrokeColor);
        Assert.Equal(0.0104, paint.StrokeWidthInches, 4);
        Assert.True(paint.HasFill);
        Assert.Equal(new Rgba32(255, 255, 255, 255), paint.FillColor);
    }

    /// <summary>Themed cells declared directly on the shape take the same fallback.</summary>
    [Fact]
    public void ThemedPaint_DeclaredOnShape_ResolvesToNoStyleValues()
    {
        using var document = OpenShape(
            """<Cell N="FillForegnd" V="Themed" F="THEMEVAL()"/><Cell N="LineColor" V="Themed" F="THEMEVAL()"/>""");

        var paint = document.GetPageShapes(0)[0].Paint!;

        Assert.Equal(new Rgba32(255, 255, 255, 255), paint.FillColor);
        Assert.Equal(new Rgba32(0, 0, 0, 255), paint.StrokeColor);
    }

    /// <summary>A non-themed own value still wins over the fallback.</summary>
    [Fact]
    public void ThemedPaint_OwnLiteralValue_IsNotReplaced()
    {
        using var document = OpenShape("""<Cell N="FillForegnd" V="#336699"/>""");

        var paint = document.GetPageShapes(0)[0].Paint!;

        Assert.Equal(new Rgba32(0x33, 0x66, 0x99, 255), paint.FillColor);
    }

    /// <summary>A themed text color resolves to the "No Style" Character color (black), not gray.</summary>
    [Fact]
    public void ThemedTextColor_InheritedFromThemeStyle_ResolvesToNoStyleColor()
    {
        using var document = OpenShape();

        var run = document.GetPageShapes(0)[0].TextRuns![0];

        Assert.Equal(new Rgba32(0, 0, 0, 255), run.Color);
    }
}

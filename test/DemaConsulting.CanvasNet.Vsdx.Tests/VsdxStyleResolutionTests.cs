// cspell:ignore vsdx Visio Foregnd Themed THEMEVAL davehoward

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

/// <summary>
///     Unit-level tests for <c>VsdxDocument.Styles.cs</c>/<c>VsdxDocument.Paint.cs</c>'s
///     StyleSheet chain resolver: the category-specific (<c>LineStyle</c>/<c>FillStyle</c>)
///     parent-pointer walk, the built-in StyleSheet IDs <c>0</c>-<c>3</c> (serialized explicitly
///     by every real-world fixture rather than left implicit), and a shape's own direct cell
///     override taking final precedence over the entire chain.
/// </summary>
public class VsdxStyleResolutionTests
{
    /// <summary>
    ///     A minimal built-in-style-shaped <c>&lt;StyleSheets&gt;</c> block: ID <c>0</c> ("No
    ///     Style", terminal - no further parent, literal black line / white fill), ID <c>1</c>
    ///     chains to ID <c>0</c> for every category, ID <c>3</c> ("Normal") chains to ID <c>1</c>
    ///     for every category and declares no paint cells of its own - mirroring the chain depth
    ///     observed in <c>davehoward-test9-rect-and-line.vsdx</c>'s own document.xml.
    /// </summary>
    private const string StyleSheetsXml =
        """
        <StyleSheets>
          <StyleSheet ID="0" Name="No Style">
            <Cell N="LineColor" V="0"/><Cell N="LinePattern" V="1"/>
            <Cell N="FillForegnd" V="1"/><Cell N="FillPattern" V="1"/>
          </StyleSheet>
          <StyleSheet ID="1" Name="Text Only" LineStyle="0" FillStyle="0" TextStyle="0">
          </StyleSheet>
          <StyleSheet ID="3" Name="Normal" LineStyle="1" FillStyle="1" TextStyle="1">
          </StyleSheet>
        </StyleSheets>
        """;

    /// <summary>
    ///     Proves a shape with <c>LineStyle="3"</c>/<c>FillStyle="3"</c> and no own paint cells
    ///     resolves <c>LineColor</c>/<c>FillForegnd</c> by walking the chain 3 -&gt; 1 -&gt; 0,
    ///     landing on ID 0's literal cells.
    /// </summary>
    [Fact]
    public void StyleResolution_ChainWalk_ResolvesThroughMultipleParents()
    {
        // Arrange
        var shapeXml =
            """
            <Shape ID="1" Type="Shape" LineStyle="3" FillStyle="3">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml, StyleSheetsXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var paint = document.GetPageShapes(0)[0].Paint!;

        // Assert: StyleSheet ID 0's literal LineColor="0" (black) / FillForegnd="1" (white).
        Assert.True(paint.HasLine);
        Assert.Equal(0, paint.StrokeColor.R);
        Assert.Equal(0, paint.StrokeColor.G);
        Assert.Equal(0, paint.StrokeColor.B);
        Assert.True(paint.HasFill);
        Assert.Equal(255, paint.FillColor.R);
        Assert.Equal(255, paint.FillColor.G);
        Assert.Equal(255, paint.FillColor.B);
    }

    /// <summary>
    ///     Proves a shape's own direct, literal cell override takes final precedence over the
    ///     entire StyleSheet chain, even when the shape also declares a <c>LineStyle=</c>
    ///     attribute pointing at a style with its own literal <c>LineColor</c>.
    /// </summary>
    [Fact]
    public void StyleResolution_DirectShapeOverride_TakesPrecedenceOverStyleSheetChain()
    {
        // Arrange: the shape's own LineColor overrides StyleSheet ID 0's LineColor="0" (black).
        var shapeXml =
            """
            <Shape ID="1" Type="Shape" LineStyle="0" FillStyle="0">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Cell N="LineColor" V="#00ff00"/>
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
        Assert.Equal(0, paint.StrokeColor.R);
        Assert.Equal(255, paint.StrokeColor.G);
        Assert.Equal(0, paint.StrokeColor.B);
    }

    /// <summary>
    ///     Proves a <c>FillPattern="0"</c> literal (whether from the shape itself or the chain)
    ///     resolves <see cref="VsdxResolvedPaint.HasFill"/> to <see langword="false"/>.
    /// </summary>
    [Fact]
    public void StyleResolution_FillPatternZero_ResolvesNoFill()
    {
        // Arrange
        var shapeXml =
            """
            <Shape ID="1" Type="Shape" LineStyle="0" FillStyle="0">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Cell N="FillPattern" V="0"/>
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
        Assert.False(paint.HasFill);
    }

    /// <summary>
    ///     Proves the literal sentinel color value <c>"Themed"</c> resolves through
    ///     <see cref="VsdxColorPalette.ThemedFallback"/> rather than throwing - the deliberate,
    ///     evidence-based deviation from the originating plan report's Assumption #1 (see
    ///     <see cref="VsdxColorPalette.ThemedFallback"/>'s own remarks for the fixture evidence).
    /// </summary>
    [Fact]
    public void StyleResolution_ThemedColorSentinel_ResolvesToNeutralFallbackWithoutThrowing()
    {
        // Arrange
        var shapeXml =
            """
            <Shape ID="1" Type="Shape" LineStyle="0" FillStyle="0">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Cell N="FillForegnd" V="Themed" F="THEMEVAL()"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml, StyleSheetsXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var exception = Record.Exception(() => document.GetPageShapes(0));

        // Assert
        Assert.Null(exception);
        var paint = document.GetPageShapes(0)[0].Paint!;
        Assert.Equal(VsdxColorPalette.ThemedFallback, paint.FillColor);
    }
}

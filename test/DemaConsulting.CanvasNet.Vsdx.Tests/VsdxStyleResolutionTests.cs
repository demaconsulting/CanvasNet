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

    /// <summary>
    ///     Proves the StyleSheet chain walk's own documented termination guarantee: a shape
    ///     referencing StyleSheet ID <c>0</c> ("No Style") directly - the chain's own root, with
    ///     no further parent of any kind - resolves ID 0's literal cells immediately, with no
    ///     infinite walk and no fallback-to-caller-default needed, proving the walk is guaranteed
    ///     to terminate at ID 0 exactly as <c>CanvasNetVsdx-VsdxDocument-NoStyleTermination</c>
    ///     requires.
    /// </summary>
    [Fact]
    public void StyleResolution_NoAncestorSuppliesValue_TerminatesAtStyleSheetZeroDefault()
    {
        // Arrange: LineStyle/FillStyle reference ID 0 directly - the chain's own terminal node.
        var shapeXml =
            """
            <Shape ID="1" Type="Shape" LineStyle="0" FillStyle="0">
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
        var exception = Record.Exception(() => document.GetPageShapes(0));

        // Assert: resolves without throwing (no infinite walk), landing directly on ID 0's own
        // literal LineColor="0" (black) / FillForegnd="1" (white) - the same terminal values
        // StyleResolution_ChainWalk_ResolvesThroughMultipleParents proves are reached via the
        // longer 3 -> 1 -> 0 walk.
        Assert.Null(exception);
        var paint = document.GetPageShapes(0)[0].Paint!;
        Assert.True(paint.HasLine);
        Assert.Equal(0, paint.StrokeColor.R);
        Assert.True(paint.HasFill);
        Assert.Equal(255, paint.FillColor.R);
    }

    /// <summary>
    ///     Proves a non-zero, non-one <c>FillPattern</c> value degrades to the same solid-fill
    ///     treatment as <c>FillPattern="1"</c> (using the resolved <c>FillForegnd</c> color)
    ///     rather than throwing - the actual, correct, graceful-degradation behavior the
    ///     <c>FillPatternDeferral</c> requirement describes after its Milestone 8 text
    ///     correction (an earlier revision of that requirement incorrectly claimed the library
    ///     "shall... throw VsdxUnsupportedFeatureException" for this case; it never did).
    /// </summary>
    [Fact]
    public void StyleResolution_DegradesNonSolidFillPatternToSolidFillWithoutThrowing()
    {
        // Arrange: FillPattern="25" is not enumerable as a specific hatch/gradient pattern.
        var shapeXml =
            """
            <Shape ID="1" Type="Shape" LineStyle="0" FillStyle="0">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Cell N="FillForegnd" V="#336699"/><Cell N="FillPattern" V="25"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml, StyleSheetsXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var exception = Record.Exception(() => document.GetPageShapes(0));

        // Assert: degrades to solid fill using the resolved FillForegnd color, never throwing.
        Assert.Null(exception);
        var paint = document.GetPageShapes(0)[0].Paint!;
        Assert.True(paint.HasFill);
        Assert.Equal(0x33, paint.FillColor.R);
        Assert.Equal(0x66, paint.FillColor.G);
        Assert.Equal(0x99, paint.FillColor.B);
    }

    /// <summary>
    ///     Proves Milestone 10's <c>FillForegndTrans</c> resolution (Bug #3 - see
    ///     <c>VsdxDocument.Paint.cs</c>'s <c>ApplyTransparency</c>): a literal
    ///     <c>FillForegndTrans="0.4"</c> cell (40% transparent) reduces the resolved
    ///     <see cref="VsdxResolvedPaint.FillColor"/>'s own alpha channel to 60% of fully opaque,
    ///     rather than the cell being silently ignored (leaving the fill fully opaque) as before
    ///     this milestone. Confirmed against <c>60973.vsdx</c>'s own "Virtual Devices" container
    ///     shape, whose literal 40%-transparent fill was previously painting fully opaque and
    ///     obscuring its children (see the milestone's own completion report for the external
    ///     smoke-test visual evidence).
    /// </summary>
    [Fact]
    public void StyleResolution_FillForegndTrans_ReducesResolvedFillAlpha()
    {
        // Arrange
        var shapeXml =
            """
            <Shape ID="1" Type="Shape" LineStyle="0" FillStyle="0">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Cell N="FillForegnd" V="#7F7F7F"/><Cell N="FillPattern" V="1"/>
              <Cell N="FillForegndTrans" V="0.4"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml, StyleSheetsXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var paint = document.GetPageShapes(0)[0].Paint!;

        // Assert: the resolved fill color keeps its literal RGB, but its alpha is reduced to 60%
        // (0.6 * 255 ≈ 153) of fully opaque.
        Assert.True(paint.HasFill);
        Assert.Equal(0x7F, paint.FillColor.R);
        Assert.Equal(0x7F, paint.FillColor.G);
        Assert.Equal(0x7F, paint.FillColor.B);
        Assert.Equal(153, paint.FillColor.A);
    }

    /// <summary>
    ///     Proves Milestone 10's <c>LineColorTrans</c> resolution (Bug #3's stroke-side
    ///     counterpart - see <c>VsdxDocument.Paint.cs</c>'s <c>ApplyTransparency</c>): a literal
    ///     <c>LineColorTrans="0.4"</c> cell (40% transparent) reduces the resolved
    ///     <see cref="VsdxResolvedPaint.StrokeColor"/>'s own alpha channel to 60% of fully opaque,
    ///     symmetrically with <see cref="StyleResolution_FillForegndTrans_ReducesResolvedFillAlpha"/>'s
    ///     fill-side coverage.
    /// </summary>
    [Fact]
    public void StyleResolution_LineColorTrans_ReducesResolvedStrokeAlpha()
    {
        // Arrange
        var shapeXml =
            """
            <Shape ID="1" Type="Shape" LineStyle="0" FillStyle="0">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Cell N="LineColor" V="#7F7F7F"/><Cell N="LinePattern" V="1"/>
              <Cell N="LineColorTrans" V="0.4"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml, StyleSheetsXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var paint = document.GetPageShapes(0)[0].Paint!;

        // Assert: the resolved stroke color keeps its literal RGB, but its alpha is reduced to
        // 60% (0.6 * 255 ≈ 153) of fully opaque.
        Assert.True(paint.HasLine);
        Assert.Equal(0x7F, paint.StrokeColor.R);
        Assert.Equal(0x7F, paint.StrokeColor.G);
        Assert.Equal(0x7F, paint.StrokeColor.B);
        Assert.Equal(153, paint.StrokeColor.A);
    }

    /// <summary>
    ///     Milestone 10 retry 1, Finding #1 (locking test, no production-code change): proves a
    ///     literal <c>LineColorTrans="1"</c> cell (100% transparent, the full-transparency
    ///     boundary case) reduces the resolved <see cref="VsdxResolvedPaint.StrokeColor"/>'s alpha
    ///     channel all the way to fully transparent (<c>0</c>), matching VisioML's own definition
    ///     of <c>*Trans</c> as a <c>0..1</c> fraction where <c>1</c> means fully transparent.
    ///     Independently re-verified (Milestone 10 quality retry 1) against
    ///     <c>github260.vsdx</c>'s own literal-<c>LineColorTrans="1"</c> shapes (a Lucidchart
    ///     export convention, not a QuickStyle/theme-governed case - this fixture carries no
    ///     <c>theme1.xml</c> part at all): direct pixel-scanning of the actual Visio-reference PNG
    ///     at the exact coordinates of every such shape (including the specific shape quality's
    ///     report cited, <c>com.lucidchart.UMLStartBlock.1</c>, <c>ID="1"</c>) shows none of them
    ///     carries a visible border in real Visio - the literal, unconditional alpha-zero
    ///     interpretation this resolver already implements is spec-correct, not a regression; see
    ///     this milestone's own completion report for the full re-verification evidence.
    /// </summary>
    [Fact]
    public void StyleResolution_LineColorTrans_FullyTransparent_HasNoVisibleStroke()
    {
        // Arrange
        var shapeXml =
            """
            <Shape ID="1" Type="Shape" LineStyle="0" FillStyle="0">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Cell N="LineColor" V="#000000"/><Cell N="LinePattern" V="1"/>
              <Cell N="LineColorTrans" V="1"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml, StyleSheetsXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var paint = document.GetPageShapes(0)[0].Paint!;

        // Assert: the resolved stroke color's alpha is reduced all the way to 0 (fully
        // transparent) - a fully transparent line is effectively invisible when rasterized,
        // matching github260.vsdx's own Visio-reference-confirmed borderless rendering.
        Assert.Equal(0, paint.StrokeColor.A);
    }

    /// <summary>
    ///     Milestone 10 retry 1, Finding #1 (locking test, no production-code change): the
    ///     <c>FillForegndTrans</c> counterpart of
    ///     <see cref="StyleResolution_LineColorTrans_FullyTransparent_HasNoVisibleStroke"/> - a
    ///     literal <c>FillForegndTrans="1"</c> cell (100% transparent) reduces the resolved
    ///     <see cref="VsdxResolvedPaint.FillColor"/>'s alpha channel all the way to fully
    ///     transparent (<c>0</c>), symmetrically with the stroke-side coverage.
    /// </summary>
    [Fact]
    public void StyleResolution_FillForegndTrans_FullyTransparent_HasNoVisibleFill()
    {
        // Arrange
        var shapeXml =
            """
            <Shape ID="1" Type="Shape" LineStyle="0" FillStyle="0">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Cell N="FillForegnd" V="#000000"/><Cell N="FillPattern" V="1"/>
              <Cell N="FillForegndTrans" V="1"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml, StyleSheetsXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var paint = document.GetPageShapes(0)[0].Paint!;

        // Assert: the resolved fill color's alpha is reduced all the way to 0 (fully
        // transparent).
        Assert.Equal(0, paint.FillColor.A);
    }
}

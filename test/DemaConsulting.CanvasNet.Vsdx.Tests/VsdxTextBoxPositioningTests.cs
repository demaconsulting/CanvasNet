// cspell:ignore vsdx Visio davehoward

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

/// <summary>
///     Unit-level tests for <c>VsdxDocument.TextBox.cs</c>'s text-box positioning resolver: the
///     implicit default (shape's own geometry bounding box) and the explicit <c>Txt*</c> override
///     cell set, including Master inheritance of those override cells via the existing, shared
///     <c>MergeCells</c> helper.
/// </summary>
public class VsdxTextBoxPositioningTests
{
    /// <summary>Builds a one-shape package, optionally with Master markup.</summary>
    private static VsdxDocument OpenShape(string extraCellsXml, string? masterXml = null)
    {
        var shapeXml =
            $"""
            <Shape ID="1" Type="Shape" {(masterXml is not null ? "Master=\"1\"" : string.Empty)}>
              <Cell N="PinX" V="3"/><Cell N="PinY" V="4"/><Cell N="Width" V="2"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="1"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              {extraCellsXml}
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
              <Text>Hi</Text>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml, mastersXml: masterXml);
        return VsdxDocument.Open(stream);
    }

    /// <summary>
    ///     Proves a shape with no own <c>Txt*</c> cells at all resolves an implicit default text
    ///     box exactly equal to the shape's own geometry bounding box in shape-local coordinates:
    ///     origin <c>(0, 0)</c>, size <c>(Width, Height)</c>, <c>LocPin</c> at the box's own
    ///     center-equivalent (no pin offset), <c>Angle</c> zero.
    /// </summary>
    [Fact]
    public void TextBox_NoTxtCells_ResolvesImplicitDefaultEqualToShapeGeometryBox()
    {
        // Arrange / Act
        using var document = OpenShape(string.Empty);
        var textBox = document.GetPageShapes(0)[0].TextBox!;

        // Assert: implicit box spans [0, Width] x [0, Height] in shape-local space, pinned at its
        // own center (TxtPinX/Y == TxtLocPinX/Y == half of Width/Height) and unrotated.
        Assert.Equal(2.0, textBox.TxtWidth, 6);
        Assert.Equal(1.0, textBox.TxtHeight, 6);
        Assert.Equal(0.0, textBox.TxtAngle, 6);
        Assert.Equal(1.0, textBox.TxtPinX, 6);
        Assert.Equal(0.5, textBox.TxtPinY, 6);
    }

    /// <summary>
    ///     Proves explicit <c>TxtPinX</c>/<c>TxtPinY</c>/<c>TxtWidth</c>/<c>TxtHeight</c> cells
    ///     override the implicit default entirely.
    /// </summary>
    [Fact]
    public void TextBox_ExplicitTxtCells_OverrideImplicitDefault()
    {
        // Arrange / Act
        using var document = OpenShape(
            """
            <Cell N="TxtPinX" V="0.25"/><Cell N="TxtPinY" V="0.1"/>
            <Cell N="TxtWidth" V="0.5"/><Cell N="TxtHeight" V="0.2"/>
            <Cell N="TxtLocPinX" V="0.25"/><Cell N="TxtLocPinY" V="0.1"/>
            """);
        var textBox = document.GetPageShapes(0)[0].TextBox!;

        // Assert
        Assert.Equal(0.25, textBox.TxtPinX, 6);
        Assert.Equal(0.1, textBox.TxtPinY, 6);
        Assert.Equal(0.5, textBox.TxtWidth, 6);
        Assert.Equal(0.2, textBox.TxtHeight, 6);
    }

    /// <summary>
    ///     Proves an instance shape that omits <c>Txt*</c> cells entirely but whose Master shape
    ///     declares them inherits the Master's own explicit text-box override, via the same
    ///     <c>MergeCells</c> cell-fallback rule Milestone 3 already established for geometry/
    ///     paint cells.
    /// </summary>
    [Fact]
    public void TextBox_MasterDeclaresExplicitTxtCells_InstanceInheritsThem()
    {
        // Arrange
        const string masterXml =
            """
            <Shape ID="2" Type="Shape">
              <Cell N="PinX" V="3"/><Cell N="PinY" V="4"/><Cell N="Width" V="2"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="1"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Cell N="TxtPinX" V="0.4"/><Cell N="TxtPinY" V="0.3"/>
              <Cell N="TxtWidth" V="0.6"/><Cell N="TxtHeight" V="0.3"/>
              <Cell N="TxtLocPinX" V="0.3"/><Cell N="TxtLocPinY" V="0.15"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;

        // Act
        using var document = OpenShape(string.Empty, masterXml);
        var textBox = document.GetPageShapes(0)[0].TextBox!;

        // Assert
        Assert.Equal(0.4, textBox.TxtPinX, 6);
        Assert.Equal(0.6, textBox.TxtWidth, 6);
        Assert.Equal(0.3, textBox.TxtHeight, 6);
    }
}

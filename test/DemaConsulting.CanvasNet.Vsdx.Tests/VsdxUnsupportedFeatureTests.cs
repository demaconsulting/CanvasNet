// cspell:ignore vsdx Visio

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

/// <summary>
///     Tests for the unsupported-feature diagnostics channel
///     (<see cref="VsdxRenderOptions.UnsupportedFeatureHandler"/> /
///     <see cref="VsdxRenderOptions.ThrowOnUnsupportedFeature"/>) using synthetic in-memory packages.
/// </summary>
public class VsdxUnsupportedFeatureTests
{
    /// <summary>A shape with one genuinely unrecognized (made-up) geometry row type.</summary>
    private const string UnknownRowShape =
        """
        <Shape ID="1" Type="Shape">
          <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
          <Cell N="LocPinX" V="0"/><Cell N="LocPinY" V="0"/><Cell N="Angle" V="0"/>
          <Section N="Geometry" IX="0">
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="FutureRowKind" IX="2"><Cell N="X" V="1"/><Cell N="Y" V="1"/></Row>
            <Row T="FutureRowKind" IX="3"><Cell N="X" V="1"/><Cell N="Y" V="0"/></Row>
          </Section>
        </Shape>
        """;

    /// <summary>A connector whose EndArrow index (99) is not in the supported catalog.</summary>
    private const string UnknownArrowConnector =
        """
        <Shape ID="2" Type="Shape">
          <Cell N="PinX" V="2"/><Cell N="PinY" V="2"/><Cell N="Width" V="2"/><Cell N="Height" V="0"/>
          <Cell N="LocPinX" V="1"/><Cell N="LocPinY" V="0"/><Cell N="Angle" V="0"/>
          <Cell N="BeginX" V="1"/><Cell N="BeginY" V="2"/><Cell N="EndX" V="3"/><Cell N="EndY" V="2"/>
          <Cell N="LineColor" V="#000000"/><Cell N="LineWeight" V="0.02"/><Cell N="LinePattern" V="1"/>
          <Cell N="EndArrow" V="99"/>
          <Section N="Geometry" IX="0">
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="LineTo" IX="2"><Cell N="X" V="2"/><Cell N="Y" V="0"/></Row>
          </Section>
        </Shape>
        """;

    /// <summary>The handler receives each distinct feature exactly once per Render, in token order, and on every render.</summary>
    [Fact]
    public void Render_UnsupportedConstructs_ReportedOncePerDistinctFeaturePerRender()
    {
        using var stream = VsdxTestPackages.BuildPackage(UnknownRowShape + UnknownArrowConnector);
        using var document = VsdxDocument.Open(stream);
        var received = new List<VsdxUnsupportedFeature>();
        var options = new VsdxRenderOptions { UnsupportedFeatureHandler = received.Add };

        using var first = document.Render(0, 20, 20, options);
        using var second = document.Render(0, 20, 20, options);

        var tokens = received.Select(f => f.Feature).ToList();
        Assert.Equal(
            new[] { "vsdx-arrowhead-99", "vsdx-geometry-row-FutureRowKind", "vsdx-arrowhead-99", "vsdx-geometry-row-FutureRowKind" },
            tokens);
        Assert.All(received, f => Assert.False(string.IsNullOrWhiteSpace(f.Message)));
    }

    /// <summary>Default options (no handler, not strict) render tolerantly without throwing.</summary>
    [Fact]
    public void Render_UnsupportedConstructs_DefaultOptionsDoNotThrow()
    {
        using var stream = VsdxTestPackages.BuildPackage(UnknownRowShape + UnknownArrowConnector);
        using var document = VsdxDocument.Open(stream);

        using var surface = document.Render(0, 20, 20);

        Assert.Equal(20, surface.Width);
    }

    /// <summary>Strict mode throws VsdxUnsupportedFeatureException carrying the feature token.</summary>
    [Fact]
    public void Render_UnsupportedConstructs_StrictModeThrowsWithFeatureToken()
    {
        using var stream = VsdxTestPackages.BuildPackage(UnknownRowShape);
        using var document = VsdxDocument.Open(stream);

        var exception = Assert.Throws<VsdxUnsupportedFeatureException>(
            () => document.Render(0, 20, 20, new VsdxRenderOptions { ThrowOnUnsupportedFeature = true }));

        Assert.Equal("vsdx-geometry-row-FutureRowKind", exception.Feature);
    }

    /// <summary>A bare THEMEVAL() fill cannot be resolved, so the gray fallback is reported rather than silent.</summary>
    [Fact]
    public void Render_UnresolvableThemedFill_ReportsThemedColor()
    {
        const string shape =
            """
            <Shape ID="1" Type="Shape">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0"/><Cell N="LocPinY" V="0"/><Cell N="Angle" V="0"/>
              <Cell N="FillForegnd" V="Themed" F="THEMEVAL()"/><Cell N="FillPattern" V="1"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
                <Row T="LineTo" IX="2"><Cell N="X" V="1"/><Cell N="Y" V="0"/></Row>
                <Row T="LineTo" IX="3"><Cell N="X" V="1"/><Cell N="Y" V="1"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shape);
        using var document = VsdxDocument.Open(stream);
        var received = new List<VsdxUnsupportedFeature>();

        using var surface = document.Render(0, 20, 20, new VsdxRenderOptions { UnsupportedFeatureHandler = received.Add });

        Assert.Contains(received, f => f.Feature == "vsdx-themed-color");
    }

    /// <summary>A page with only supported constructs reports nothing and strict mode does not throw.</summary>
    [Fact]
    public void Render_FullySupportedPage_ReportsNothing()
    {
        const string shape =
            """
            <Shape ID="1" Type="Shape">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0"/><Cell N="LocPinY" V="0"/><Cell N="Angle" V="0"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
                <Row T="LineTo" IX="2"><Cell N="X" V="1"/><Cell N="Y" V="1"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shape);
        using var document = VsdxDocument.Open(stream);
        var received = new List<VsdxUnsupportedFeature>();

        using var surface = document.Render(
            0, 20, 20, new VsdxRenderOptions { UnsupportedFeatureHandler = received.Add, ThrowOnUnsupportedFeature = true });

        Assert.Empty(received);
    }
}

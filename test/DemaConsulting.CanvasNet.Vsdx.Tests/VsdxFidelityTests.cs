// cspell:ignore vsdx Visio

using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

/// <summary>
///     Rendering-fidelity tests (line patterns, text decoration, text/layer hiding, glyph
///     fallback, geometry) using synthetic in-memory packages only. Pixel assertions are
///     structural (run counts, presence/absence of ink), never timing based.
/// </summary>
public class VsdxFidelityTests
{
    /// <summary>Builds a horizontal stroked line from x=1 to x=7 inches at y=5.5 inches with the given extra cells.</summary>
    private static string BuildLine(string extraCells) =>
        $"""
        <Shape ID="1" Type="Shape">
          <Cell N="PinX" V="1"/><Cell N="PinY" V="5.5"/><Cell N="Width" V="6"/><Cell N="Height" V="0"/>
          <Cell N="LocPinX" V="0"/><Cell N="LocPinY" V="0"/><Cell N="Angle" V="0"/>
          <Cell N="LineColor" V="#000000"/><Cell N="LineWeight" V="0.02"/>
          <Cell N="FillPattern" V="0"/>{extraCells}
          <Section N="Geometry" IX="0">
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="LineTo" IX="2"><Cell N="X" V="6"/><Cell N="Y" V="0"/></Row>
          </Section>
        </Shape>
        """;

    /// <summary>Counts contiguous painted runs along pixel row <paramref name="y"/> in <c>[x0, x1)</c>.</summary>
    private static int CountRuns(Surface surface, int y, int x0, int x1)
    {
        var runs = 0;
        var inRun = false;
        for (var x = x0; x < x1; x++)
        {
            var painted = surface[x, y].R < 128;
            if (painted && !inRun)
            {
                runs++;
            }

            inRun = painted;
        }

        return runs;
    }

    /// <summary>Renders a line with the given pattern and returns the run count along its center row.</summary>
    private static int RunsForPattern(string pattern)
    {
        using var stream = VsdxTestPackages.BuildPackage(BuildLine($"""<Cell N="LinePattern" V="{pattern}"/>"""));
        using var document = VsdxDocument.Open(stream);
        using var surface = document.Render(0, 100);
        return CountRuns(surface, 550, 120, 680);
    }

    /// <summary>A solid pattern is one continuous run.</summary>
    [Fact]
    public void Render_LinePatternSolid_IsOneRun() => Assert.Equal(1, RunsForPattern("1"));

    /// <summary>Dash (2), dot (3), dash-dot (4) all break the line into many runs.</summary>
    [Theory]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("4")]
    [InlineData("16")]
    public void Render_LinePatternDashed_IsBrokenIntoRuns(string pattern) =>
        Assert.True(RunsForPattern(pattern) > 10, $"pattern {pattern} should be dashed");

    /// <summary>A dot is finer than a dash, so it produces more runs over the same length.</summary>
    [Fact]
    public void Render_LinePatternDot_HasMoreRunsThanDash() =>
        Assert.True(RunsForPattern("3") > RunsForPattern("2"));

    /// <summary>An unsupported pattern (custom 23) draws solid and is reported.</summary>
    [Fact]
    public void Render_LinePatternCustom_ReportedAndSolid()
    {
        using var stream = VsdxTestPackages.BuildPackage(BuildLine("""<Cell N="LinePattern" V="23"/>"""));
        using var document = VsdxDocument.Open(stream);
        var received = new List<VsdxUnsupportedFeature>();

        using var surface = document.Render(0, 100, new VsdxRenderOptions { UnsupportedFeatureHandler = received.Add });

        Assert.Equal(1, CountRuns(surface, 550, 120, 680));
        Assert.Contains(received, f => f.Feature == "vsdx-linepattern-23");
    }

    /// <summary>An approximated pattern (7) still dashes and is reported as approximated.</summary>
    [Fact]
    public void Render_LinePatternApproximated_Reported()
    {
        using var stream = VsdxTestPackages.BuildPackage(BuildLine("""<Cell N="LinePattern" V="7"/>"""));
        using var document = VsdxDocument.Open(stream);
        var received = new List<VsdxUnsupportedFeature>();

        using var surface = document.Render(0, 100, new VsdxRenderOptions { UnsupportedFeatureHandler = received.Add });

        Assert.True(CountRuns(surface, 550, 120, 680) > 5);
        Assert.Contains(received, f => f.Feature == "vsdx-linepattern-7-approximated");
    }
}

// cspell:ignore vsdx Visio Strikethru

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

/// <summary>
///     Tests for Character-section text decorations and case/style flags (underline,
///     strikethrough, double strikethrough, overline, small caps, all caps, transparency) using
///     synthetic in-memory packages. Structural assertions only; no timing.
/// </summary>
public class VsdxTextDecorationTests
{
    /// <summary>Opens a one-shape package with the given Character row cells and text.</summary>
    private static VsdxDocument Open(string characterCells, string text = "Hello World")
    {
        var shapeXml =
            $"""
            <Shape ID="1" Type="Shape">
              <Cell N="PinX" V="2"/><Cell N="PinY" V="2"/><Cell N="Width" V="3"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0"/><Cell N="LocPinY" V="0"/><Cell N="Angle" V="0"/>
              <Cell N="LinePattern" V="0"/><Cell N="FillPattern" V="0"/>
              <Section N="Geometry" IX="0">
                <Cell N="NoFill" V="1"/><Cell N="NoLine" V="1"/>
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
              <Section N="Character"><Row IX="0"><Cell N="Font" V="Arial"/><Cell N="Size" V="0.25"/><Cell N="Color" V="#000000"/>{characterCells}</Row></Section>
              <Text><cp IX="0"/>{text}</Text>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        return VsdxDocument.Open(stream);
    }

    /// <summary>Style bit 2 resolves to an underline, producing one bar spanning the whole text below the baseline.</summary>
    [Fact]
    public void TextDecoration_StyleBit2_ProducesUnderlineBarBelowBaseline()
    {
        using var document = Open("""<Cell N="Style" V="4"/>""");
        var shape = document.GetPageShapes(0)[0];

        Assert.Equal(VsdxTextDecoration.Underline, shape.TextRuns![0].Decoration);
        var bar = Assert.Single(shape.TextLayout!.DecorationBars);
        var baseline = shape.TextLayout.Glyphs[0].OriginYInches;
        Assert.True(bar.CenterYInches < baseline);
        Assert.True(bar.X1Inches > bar.X0Inches);
        Assert.Equal(shape.TextLayout.Glyphs[0].OriginXInches, bar.X0Inches, 6);
    }

    /// <summary>The underline is drawn into the rendered surface: more ink than the same text without it.</summary>
    [Fact]
    public void TextDecoration_Underline_AddsInkWhenRendered()
    {
        static int Ink(string cells)
        {
            using var document = Open(cells);
            using var surface = document.Render(0, 100);
            var count = 0;
            for (var y = 0; y < surface.Height; y++)
            {
                for (var x = 0; x < surface.Width; x++)
                {
                    if (surface[x, y].R < 128)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        Assert.True(Ink("""<Cell N="Style" V="4"/>""") > Ink("""<Cell N="Style" V="0"/>"""));
    }

    /// <summary>Strikethru, DoubleStrikethrough and Overline cells produce 1, 2 and 1 bars; a strike sits above the baseline and an overline above the strike.</summary>
    [Fact]
    public void TextDecoration_StrikeDoubleStrikeOverline_ProduceBars()
    {
        using var strike = Open("""<Cell N="Strikethru" V="1"/>""");
        using var doubleStrike = Open("""<Cell N="DoubleStrikethrough" V="1"/>""");
        using var over = Open("""<Cell N="Overline" V="1"/>""");

        var strikeLayout = strike.GetPageShapes(0)[0].TextLayout!;
        var doubleLayout = doubleStrike.GetPageShapes(0)[0].TextLayout!;
        var overLayout = over.GetPageShapes(0)[0].TextLayout!;

        Assert.Single(strikeLayout.DecorationBars);
        Assert.Equal(2, doubleLayout.DecorationBars.Count);
        Assert.Single(overLayout.DecorationBars);
        Assert.True(strikeLayout.DecorationBars[0].CenterYInches > strikeLayout.Glyphs[0].OriginYInches);
        Assert.True(overLayout.DecorationBars[0].CenterYInches > strikeLayout.DecorationBars[0].CenterYInches);
    }

    /// <summary>With no decoration cells, no bars are produced.</summary>
    [Fact]
    public void TextDecoration_None_ProducesNoBars()
    {
        using var document = Open("""<Cell N="Style" V="1"/>""");

        Assert.Empty(document.GetPageShapes(0)[0].TextLayout!.DecorationBars);
    }

    /// <summary>Style bit 3 (small caps) upper-cases lowercase letters at a reduced size; Case=1 upper-cases at full size.</summary>
    [Fact]
    public void TextCase_SmallCapsAndAllCaps_TransformRuns()
    {
        using var small = Open("""<Cell N="Style" V="8"/>""", "Ab");
        using var caps = Open("""<Cell N="Case" V="1"/>""", "Ab");

        var smallRuns = small.GetPageShapes(0)[0].TextRuns!;
        Assert.Equal(["A", "B"], smallRuns.Select(r => r.Text).ToArray());
        Assert.True(smallRuns[1].SizeInches < smallRuns[0].SizeInches);

        var capsRun = Assert.Single(caps.GetPageShapes(0)[0].TextRuns!);
        Assert.Equal("AB", capsRun.Text);
    }

    /// <summary>ColorTrans reduces the run color's alpha.</summary>
    [Fact]
    public void TextColor_Transparency_ReducesAlpha()
    {
        using var document = Open("""<Cell N="ColorTrans" V="0.5"/>""");

        Assert.Equal(128, document.GetPageShapes(0)[0].TextRuns![0].Color.A);
    }

    /// <summary>Superscript/subscript (Pos) is not supported and is reported.</summary>
    [Fact]
    public void TextPos_Superscript_IsReported()
    {
        using var document = Open("""<Cell N="Pos" V="1"/>""");
        var received = new List<VsdxUnsupportedFeature>();

        using var surface = document.Render(0, 50, new VsdxRenderOptions { UnsupportedFeatureHandler = received.Add });

        Assert.Contains(received, f => f.Feature == "vsdx-text-superscript-subscript");
    }
}

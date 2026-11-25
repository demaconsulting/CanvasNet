using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.MathMl.Layout;
using static DemaConsulting.CanvasNet.MathMl.Tests.MathMlTestHelpers;

namespace DemaConsulting.CanvasNet.MathMl.Tests.Layout;

/// <summary>
///     Subsystem tests for the <c>Layout</c> subsystem: a row of tokens laid out through the engine with the real math
///     font, checked against the metrics of the font and the operator spacing.
/// </summary>
public class LayoutTests
{
    /// <summary>
    ///     Validates the box of <c>x + 1</c> at 100 px: the advances of the glyphs plus the Default spacing of the
    ///     operator, and the ink ascent and descent rounded out.
    /// </summary>
    [Fact]
    public void Layout_TokenRow_MatchesFontMetricsAndSpacing()
    {
        // Arrange: the advances and ink of the glyphs drawn (the italic x, the plus, the one)
        var font = MathFont.Default;
        double Advance(int codePoint)
        {
            var (glyphFont, glyph) = font.Resolve(codePoint);
            return MathFont.Advance(glyphFont, glyph, 100);
        }

        double Top(int codePoint)
        {
            var (glyphFont, glyph) = font.Resolve(codePoint);
            return -font.InkBounds(glyphFont, glyph, 100).Y;
        }

        var space = 500 / 18.0;

        // Act
        var box = LayoutText(MathMlTestHelpers.Math("<mi>x</mi><mo>+</mo><mn>1</mn>"));

        // Assert
        Assert.Equal(Advance(0x1D465) + space + Advance('+') + space + Advance('1'), box.Width, 9);
        Assert.Equal(System.Math.Ceiling(new[] { Top(0x1D465), Top('+'), Top('1') }.Max()), box.Ascent);
        Assert.Equal([0, Advance(0x1D465) + space, Advance(0x1D465) + space + Advance('+') + space], box.Children.Select(child => child.X));
    }

    /// <summary>
    ///     Validates that a script level scales the tokens with the MATH constants: at depth 1 the glyphs are 60% as wide.
    /// </summary>
    [Fact]
    public void Layout_ScriptLevel_ScalesTokensWithMathConstants()
    {
        // Act
        var plain = LayoutText(MathMlTestHelpers.Math("<mn>1234</mn>"));
        var script = LayoutText(MathMlTestHelpers.Math("<mstyle scriptlevel=\"1\"><mn>1234</mn></mstyle>"));

        // Assert
        Assert.Equal(plain.Width * 0.6, script.Width, 9);
    }

    /// <summary>
    ///     Validates that an element this version does not draw is laid out as a row of its children, and that the element,
    ///     an attribute without effect and an invalid attribute value are reported while the layout continues.
    /// </summary>
    [Fact]
    public void Layout_UnsupportedElementsAndAttributes_AreRowsWithReports()
    {
        // Arrange
        var sink = new MathMlReportSink(100, false);

        // Act
        var box = LayoutText(MathMlTestHelpers.Math("<mfrac><mi>a</mi><mn>2</mn></mfrac><mi dir=\"rtl\">b</mi><mn mathcolor=\"bogus\">3</mn>"), 100, sink);
        var warnings = new List<MathMlDiagnostic>();
        var unsupported = new List<MathMlUnsupportedFeature>();
        sink.Deliver(warnings.Add, unsupported.Add);

        // Assert: the fraction is a row (its children side by side on its baseline), and the three tokens follow it
        var fraction = Flatten(box).Single(entry => entry.Box.Element?.Name == "mfrac").Box;
        Assert.Equal([0, fraction.Children[0].Box.Width], fraction.Children.Select(child => child.X));
        Assert.All(fraction.Children, child => Assert.Equal(0, child.Y));
        Assert.Equal(3, box.Children.Count);
        Assert.Equal([MathMlDiagnosticCodes.ElementUnsupported, MathMlDiagnosticCodes.AttributeUnsupported], unsupported.Select(report => report.Feature));
        Assert.Equal(MathMlDiagnosticCodes.AttributeInvalid, Assert.Single(warnings).Code);
    }

    /// <summary>
    ///     Validates the bounds of a layout through the subsystem: a work budget smaller than the equation and a canceled
    ///     token stop it, and the same equation lays out within the default limits.
    /// </summary>
    [Fact]
    public void Layout_Bounds_WorkBudgetAndCancellation_StopTheLayout()
    {
        // Arrange
        var root = Tree(MathMlTestHelpers.Math("<mrow><mi>x</mi><mo>+</mo><mn>12</mn></mrow>"));
        using var source = new CancellationTokenSource();
        source.Cancel();
        LayoutEngine Engine(MathMlLimits limits, CancellationToken token) => new(MathFont.Default, limits, new MathMlReportSink(100, false), token);
        MathStyle RootStyle(LayoutEngine engine) => engine.Styles.ResolveRoot(root, 16, new Rgba32(0, 0, 0, 255), null);

        // Act
        var small = Engine(new MathMlLimits { MaxLayoutWork = 3 }, TestContext.Current.CancellationToken);
        var work = Assert.Throws<MathMlRenderException>(() => small.Layout(root, RootStyle(small)));
        var canceled = Engine(MathMlLimits.Default, source.Token);
        var cancellation = Assert.ThrowsAny<OperationCanceledException>(() => canceled.Layout(root, RootStyle(canceled)));
        var normal = Engine(MathMlLimits.Default, TestContext.Current.CancellationToken);
        var box = normal.Layout(root, RootStyle(normal));

        // Assert
        Assert.Equal(MathMlDiagnosticCodes.LimitExceeded, work.Code);
        Assert.Contains("MathMlLimits.MaxLayoutWork", work.Message, StringComparison.Ordinal);
        Assert.NotNull(cancellation);
        Assert.True(box.Width > 0);
    }
}

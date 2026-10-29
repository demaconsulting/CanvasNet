using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     Tests of how <see cref="VegaLiteDocument"/> measures and renders a view with the layout and the painter: the natural
///     size, the title on the canvas, the scale and the fit to an explicit size.
/// </summary>
public class VegaLiteDocumentLayoutTests
{
    /// <summary>Opaque white.</summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Validates that Measure reports exactly the size Render produces, for sizes, titles, paddings and scales.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Measure_ReportsExactlyTheRenderedSize()
    {
        // Arrange
        string[] specs =
        [
            "{}",
            "{\"width\":123,\"height\":45}",
            "{\"title\":\"Quarterly report\"}",
            "{\"title\":{\"text\":\"Left\",\"subtitle\":\"sub\",\"orient\":\"left\"}}",
            "{\"width\":20,\"title\":\"A very wide title that overflows the little plot\"}",
            "{\"padding\":{\"left\":1,\"top\":2,\"right\":3,\"bottom\":4},\"title\":{\"text\":\"T\",\"orient\":\"bottom\"}}",
            "{\"autosize\":\"none\",\"width\":50,\"height\":50,\"title\":\"X\"}",
            "{\"width\":100.4,\"height\":50.5,\"title\":\"T\"}",
        ];
        VegaLiteRenderOptions[] optionSets =
        [
            new(),
            new() { Scale = 2 },
            new() { Scale = 1.5 },
            new() { Scale = 0.5 },
        ];

        // Act / Assert
        foreach (var spec in specs)
        {
            var document = VegaLiteDocument.Parse(spec);
            foreach (var options in optionSets)
            {
                var measured = document.Measure(options, Ct);
                using var surface = document.Render(options, Ct);
                Assert.Equal(new VegaLiteSize(surface.Width, surface.Height), measured);
            }
        }
    }

    /// <summary>
    ///     Validates the natural size of titled views against hand-derived numbers.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Measure_NaturalSizeFollowsPaddingTitleAndAutosize()
    {
        // Act / Assert: the Vega-Lite 6 default view and padding
        Assert.Equal(new VegaLiteSize(310, 310), VegaLiteDocument.Parse("{}").Measure(cancellationToken: Ct));

        // Assert: explicit size and padding; a title adds 13 + 4 above
        Assert.Equal(new VegaLiteSize(240, 140), VegaLiteDocument.Parse("{\"width\":200,\"height\":100,\"padding\":20}").Measure(cancellationToken: Ct));
        Assert.Equal(new VegaLiteSize(310, 327), VegaLiteDocument.Parse("{\"title\":\"Title\"}").Measure(cancellationToken: Ct));

        // Assert: a title and a subtitle add 27 + 4 above (the subtitle's box starts one pixel above its anchor)
        Assert.Equal(new VegaLiteSize(310, 341), VegaLiteDocument.Parse("{\"title\":{\"text\":\"Title\",\"subtitle\":\"Subtitle\"}}").Measure(cancellationToken: Ct));

        // Assert: autosize none keeps the given size; configuration changes the default view size; the caller's configuration wins
        Assert.Equal(new VegaLiteSize(110, 110), VegaLiteDocument.Parse("{\"autosize\":\"none\",\"width\":100,\"height\":100,\"title\":\"T\"}").Measure(cancellationToken: Ct));
        Assert.Equal(new VegaLiteSize(130, 90), VegaLiteDocument.Parse("{\"config\":{\"view\":{\"continuousWidth\":120,\"continuousHeight\":80}}}").Measure(cancellationToken: Ct));
        var themed = new VegaLiteRenderOptions { Config = "{\"view\":{\"continuousWidth\":60},\"padding\":0}" };
        Assert.Equal(new VegaLiteSize(60, 300), VegaLiteDocument.Parse("{}").Measure(themed, Ct));

        // Assert: the render options' container size is used for a container width
        var container = new VegaLiteRenderOptions { ContainerWidth = 500, ContainerHeight = 200 };
        Assert.Equal(new VegaLiteSize(500, 200), VegaLiteDocument.Parse("{\"width\":\"container\",\"height\":\"container\"}").Measure(container, Ct));
    }

    /// <summary>
    ///     Validates that the title is painted where the layout puts it: inside the band above the plot, centered on it.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Render_TitleIsPaintedAboveThePlotAndCenteredOnIt()
    {
        // Arrange: the title band is the 17 pixels from y = 5 (padding) to y = 22 (the plot's top edge)
        var document = VegaLiteDocument.Parse("{\"title\":\"Quarterly report\"}");

        // Act
        using var surface = document.Render(cancellationToken: Ct);
        using var band = surface.Crop(0, 0, surface.Width, 22);

        // Assert: the canvas holds the title band, the plot's top border is on row 22
        Assert.Equal((310, 327), (surface.Width, surface.Height));
        Assert.Equal(new Rgba32(221, 221, 221, 255), surface[150, 22]);
        var ink = VegaLiteTestHelpers.InkBounds(band, White);
        Assert.NotNull(ink);

        // Assert: the ink is inside the band (not in the padding above row 5, not in the plot) and centered on x = 155
        Assert.InRange(ink.Value.Top, 5, 8);
        Assert.InRange(ink.Value.Bottom, 14, 20);
        Assert.InRange((ink.Value.Left + ink.Value.Right) / 2.0, 153, 157);

        // Assert: the plot area itself has no ink except the border
        Assert.Equal(White, surface[150, 200]);
    }

    /// <summary>
    ///     Validates the scale and the uniform fit to an explicit size: equal to rendering at the matching scale, and centered.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Render_ExplicitSizeFitsUniformlyAndCenters()
    {
        // Arrange
        var document = VegaLiteDocument.Parse("{\"title\":\"Quarterly report\"}");
        var red = new Rgba32(200, 10, 20, 255);

        // Act: 620 by 654 is exactly twice the natural 310 by 327, so the fit is the scale 2 render
        using var scaled = document.Render(new VegaLiteRenderOptions { Scale = 2 }, Ct);
        using var fitted = document.Render(620, 654, cancellationToken: Ct);
        using var wide = document.Render(1000, 327, new VegaLiteRenderOptions { Background = red }, Ct);
        using var tall = document.Render(310, 700, new VegaLiteRenderOptions { Background = red }, Ct);

        // Assert
        Assert.Equal(VegaLiteTestHelpers.Snapshot(scaled), VegaLiteTestHelpers.Snapshot(fitted));

        // Assert: the wide target scales by 1 and centers: (1000 - 310) / 2 = 345 pixels of background on each side
        Assert.True(VegaLiteTestHelpers.RegionIs(wide, 0, 0, 345, 327, red));
        Assert.True(VegaLiteTestHelpers.RegionIs(wide, 655, 0, 1000, 327, red));
        Assert.Equal(new Rgba32(221, 221, 221, 255), wide[345 + 5, 150]);

        // Assert: the tall target scales by 1 and centers vertically: (700 - 327) / 2 = 186.5, so rows 0 to 185 are background
        Assert.True(VegaLiteTestHelpers.RegionIs(tall, 0, 0, 310, 186, red));
        Assert.True(VegaLiteTestHelpers.RegionIs(tall, 0, 514, 310, 700, red));
    }

    /// <summary>
    ///     Validates that rendering a titled view is deterministic and independent of the fonts that are installed.
    /// </summary>
    /// <returns>A task that completes when the concurrent renders are done.</returns>
    [Fact]
    public async Task VegaLiteDocument_Render_TitledViewIsDeterministicAcrossThreadsAndFontOptions()
    {
        // Arrange
        var document = VegaLiteDocument.Parse("{\"title\":{\"text\":\"Quarterly report\",\"subtitle\":\"All regions\"}}");
        using var first = document.Render(cancellationToken: Ct);
        var baseline = VegaLiteTestHelpers.Snapshot(first);
        var token = Ct;

        // Act: concurrently, and with system fonts allowed (generic families still use the bundled fonts)
        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(
            () =>
            {
                using var surface = document.Render(cancellationToken: token);
                return VegaLiteTestHelpers.Snapshot(surface);
            },
            token)).ToArray();
        var concurrent = await Task.WhenAll(tasks);
        using var withSystemFonts = document.Render(new VegaLiteRenderOptions { Fonts = new VegaLiteFontOptions { UseSystemFonts = true } }, Ct);

        // Assert
        foreach (var snapshot in concurrent)
        {
            Assert.Equal(baseline, snapshot);
        }

        Assert.Equal(baseline, VegaLiteTestHelpers.Snapshot(withSystemFonts));
        Assert.False(VegaLiteTestHelpers.AllPixelsAre(first, White));
    }
}

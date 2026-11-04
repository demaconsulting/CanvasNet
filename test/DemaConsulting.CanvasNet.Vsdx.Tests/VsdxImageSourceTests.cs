// cspell:ignore vsdx visio
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

/// <summary>
///     Tests of <see cref="VsdxImageSource"/>: the first page of a Visio drawing as an embedded picture - drawn at the
///     size of its placement (fitted without distortion), over a transparent background, with unsupported features as
///     diagnostics.
/// </summary>
public class VsdxImageSourceTests
{
    /// <summary>A shape with one geometry row type the library does not know, which it reports as unsupported.</summary>
    private const string UnknownRowShape =
        """
        <Shape ID="1" Type="Shape">
          <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
          <Cell N="LocPinX" V="0"/><Cell N="LocPinY" V="0"/><Cell N="Angle" V="0"/>
          <Section N="Geometry" IX="0">
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="FutureRowKind" IX="2"><Cell N="X" V="1"/><Cell N="Y" V="1"/></Row>
          </Section>
        </Shape>
        """;

    /// <summary>A shape that is a filled rectangle, so the page has something drawn.</summary>
    private const string RectangleShape =
        """
        <Shape ID="1" Type="Shape">
          <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
          <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
          <Cell N="FillForegnd" V="#FF0000"/><Cell N="FillPattern" V="1"/>
          <Section N="Geometry" IX="0">
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="LineTo" IX="2"><Cell N="X" V="1"/><Cell N="Y" V="0"/></Row>
            <Row T="LineTo" IX="3"><Cell N="X" V="1"/><Cell N="Y" V="1"/></Row>
            <Row T="LineTo" IX="4"><Cell N="X" V="0"/><Cell N="Y" V="1"/></Row>
            <Row T="LineTo" IX="5"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
          </Section>
        </Shape>
        """;

    /// <summary>
    ///     Builds the bytes of a drawing.
    /// </summary>
    /// <param name="shapes">The shapes of the page.</param>
    /// <returns>The bytes of the package.</returns>
    private static byte[] Package(string shapes)
    {
        using var stream = VsdxTestPackages.BuildPackage(shapes);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    /// <summary>
    ///     Creates a request for a drawing.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="width">The requested width, or <see langword="null"/>.</param>
    /// <param name="height">The requested height, or <see langword="null"/>.</param>
    /// <param name="strict">Whether an unsupported feature must throw.</param>
    /// <param name="remaining">The pixels the source may allocate.</param>
    /// <returns>The request.</returns>
    private static ImageSourceRequest Request(byte[] bytes, int? width = null, int? height = null, bool strict = false, long remaining = long.MaxValue)
    {
        return new ImageSourceRequest
        {
            Href = "drawing.vsdx",
            FileName = "drawing.vsdx",
            Content = bytes,
            PixelWidth = width,
            PixelHeight = height,
            ThrowOnUnsupportedFeature = strict,
            RemainingPixels = remaining
        };
    }

    /// <summary>
    ///     Test that the page is drawn at its natural size at 96 dots per inch over a transparent background.
    /// </summary>
    [Fact]
    public void VsdxImageSource_Render_NoRequestedSize_DrawsNaturalSizeOnTransparentBackground()
    {
        // Arrange: the natural size as the library reports it
        var bytes = Package(RectangleShape);
        using var document = VsdxDocument.Open(new MemoryStream(bytes));
        var page = document.GetPageSize(0);
        var expectedWidth = (int)Math.Round(page.WidthEmu / 9525d, MidpointRounding.AwayFromZero);
        var expectedHeight = (int)Math.Round(page.HeightEmu / 9525d, MidpointRounding.AwayFromZero);

        // Act
        var result = new VsdxImageSource().Render(Request(bytes));
        using var surface = result.Surface;

        // Assert: the size is the page at 96 dpi, and the background is not painted white
        Assert.Equal((expectedWidth, expectedHeight), (surface.Width, surface.Height));
        Assert.Contains(AllPixels(surface), p => p.A == 0);
        Assert.Contains(AllPixels(surface), p => p.A > 0);
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     Test that the page is fitted inside the requested box without distortion: the larger side reaches the box.
    /// </summary>
    [Fact]
    public void VsdxImageSource_Render_RequestedBox_FitsPageInsideBox()
    {
        // Arrange
        var bytes = Package(RectangleShape);
        using var natural = new VsdxImageSource().Render(Request(bytes)).Surface;

        // Act
        using var fitted = new VsdxImageSource().Render(Request(bytes, 100, 100)).Surface;

        // Assert: inside the box, touching it on one side, with the aspect ratio of the page
        Assert.True(fitted.Width <= 100 && fitted.Height <= 100);
        Assert.Equal(100, Math.Max(fitted.Width, fitted.Height));
        Assert.Equal((double)natural.Width / natural.Height, (double)fitted.Width / fitted.Height, 0.05);
    }

    /// <summary>
    ///     Test that a size beyond <see cref="ImageSourceRequest.RemainingPixels"/> is refused with
    ///     <see cref="ImageSourceBudgetException"/> before the page is drawn (the allocation stays far below the 64 MB
    ///     of the picture), and that a box whose area is the budget is drawn.
    /// </summary>
    [Fact]
    public void VsdxImageSource_Render_SizeBeyondRemainingPixels_IsRefusedBeforeDrawing()
    {
        // Arrange
        var bytes = Package(RectangleShape);
        var source = new VsdxImageSource();

        // Act: a 4000 by 4000 box against a budget of 1000 pixels
        var before = GC.GetAllocatedBytesForCurrentThread();
        var ex = Assert.Throws<ImageSourceBudgetException>(() => source.Render(Request(bytes, 4000, 4000, remaining: 1000)));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        using var fits = source.Render(Request(bytes, 100, 100, remaining: 100 * 100)).Surface;

        // Assert: the page is fitted inside the box, so its size is at most the box and at least the largest side of it
        Assert.Equal(1000L, ex.RemainingPixels);
        Assert.True(ex.RequestedPixels > 1000 && ex.RequestedPixels <= 16_000_000L);
        Assert.True(allocated < 32 * 1024 * 1024, $"{allocated} bytes were allocated before the refusal.");
        Assert.Equal(100, Math.Max(fits.Width, fits.Height));
    }

    /// <summary>
    ///     Test that an unsupported feature is returned as a note with the source <c>vsdx</c> and the feature as its code,
    ///     and that the strict request throws instead.
    /// </summary>
    [Fact]
    public void VsdxImageSource_Render_UnsupportedFeature_ReturnsDiagnosticOrThrowsWhenStrict()
    {
        // Arrange
        var bytes = Package(UnknownRowShape);
        var source = new VsdxImageSource();

        // Act
        var result = source.Render(Request(bytes));
        using var surface = result.Surface;

        // Assert
        var note = Assert.Single(result.Diagnostics);
        Assert.Equal(("vsdx", ImageDiagnosticSeverity.Unsupported, "vsdx-geometry-row-FutureRowKind"), (note.Source, note.Severity, note.Code));
        Assert.Throws<VsdxUnsupportedFeatureException>(() => source.Render(Request(bytes, strict: true)));
    }

    /// <summary>
    ///     Test that content that is not a drawing, a request without file content, a null request and a canceled request
    ///     are rejected.
    /// </summary>
    [Fact]
    public void VsdxImageSource_Render_InvalidInput_Throws()
    {
        // Arrange
        var source = new VsdxImageSource();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => source.Render(Request([1, 2, 3])));
        Assert.Throws<InvalidOperationException>(() => source.Render(new ImageSourceRequest { Href = "camera:front" }));
        Assert.Throws<ArgumentNullException>(() => source.Render(null!));
        Assert.Throws<OperationCanceledException>(() => source.Render(new ImageSourceRequest { Href = "a", FileName = "a.vsdx", CancellationToken = cts.Token }));
    }

    /// <summary>
    ///     Test the identity of the source: its name, its extension, and that it claims no scheme.
    /// </summary>
    [Fact]
    public void VsdxImageSource_Properties_NameExtensionAndNoSchemes()
    {
        // Arrange
        var source = new VsdxImageSource();

        // Act / Assert
        Assert.Equal("vsdx", source.Name);
        Assert.Equal([".vsdx"], source.Extensions);
        Assert.Empty(source.Schemes);
    }

    /// <summary>
    ///     Enumerates the pixels of a surface.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <returns>Every pixel, row by row.</returns>
    private static IEnumerable<Rgba32> AllPixels(Surface surface)
    {
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                yield return surface[x, y];
            }
        }
    }
}

// cspell:ignore href hrefs
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Tests.ImageSources;

/// <summary>
///     Unit tests for the pixel budget of <see cref="FolderImageResolver"/>
///     (<see cref="FolderImageResolverOptions.MaxDecodedPixels"/>): a picture that does not fit is refused with a warning
///     before anything is allocated, the budget is cumulative over every embed, a source that returns more than it was
///     asked for is charged, and the requested size is capped.
/// </summary>
public class FolderImageResolverBudgetTests
{
    /// <summary>The name of the extension the stubs claim.</summary>
    private const string Ext = ".stub";

    /// <summary>The largest file the resolver reads, in bytes.</summary>
    private const long FileLimit = DemaConsulting.CanvasNet.Files.FileReferenceOptions.DefaultMaxFileBytes;

    /// <summary>
    ///     Encodes a solid picture as a PNG file (it compresses to a few kilobytes at any size).
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The bytes.</returns>
    private static byte[] SolidPng(int width, int height)
    {
        using var surface = StubImageSource.Solid(width, height, StubImageSource.Green);
        using var stream = new MemoryStream();
        PngCodec.Save(surface, stream);
        return stream.ToArray();
    }

    /// <summary>
    ///     Test that a PNG whose header declares 8192 by 8192 pixels, placed in a 1 by 1 rectangle, is refused from its
    ///     header: it is a warning with the code <c>svg-image-too-large</c>, nothing is drawn, and nothing close to the
    ///     256 MiB of the decoded picture is allocated (the file has no pixel data, so decoding it would also throw).
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_HugeRasterAtTinyPlacement_IsRefusedBeforeDecoding()
    {
        // Arrange: a 33-byte file that declares the largest picture
        using var folder = new ImageTestFolder();
        folder.WriteBytes("huge.png", PngHeaderFixtures.Declaring(Surface.MaxDimension, Surface.MaxDimension));
        var resolver = new FolderImageResolver(folder.Path, [new RasterImageSource()]);

        // Act: the allocation of the whole call is measured on this thread
        var before = GC.GetAllocatedBytesForCurrentThread();
        var surface = resolver.Resolve("huge.png", 1, 1);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert: refused with the stable code, as a warning and not an exception, and the decoded size was never allocated
        Assert.Null(surface);
        var note = Assert.Single(resolver.Diagnostics);
        Assert.Equal(("svg", ImageDiagnosticSeverity.Warning, FolderImageResolver.TooLargeCode), (note.Source, note.Severity, note.Code));
        Assert.Contains("'huge.png'", note.Message, StringComparison.Ordinal);
        Assert.Empty(resolver.Embedded);
        Assert.True(allocated < 8 * 1024 * 1024, $"{allocated} bytes were allocated; the 256 MiB picture must not be.");
    }

    /// <summary>
    ///     Test that the budget is cumulative over every embed: pictures that are placed in tiny rectangles still cost
    ///     their natural size, so the first ones are drawn and the later ones are refused with a warning each.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_ManyRasters_ExhaustBudgetAndLaterOnesAreRefused()
    {
        // Arrange: a budget for three 1000 by 1000 pictures and a half
        using var folder = new ImageTestFolder();
        folder.WriteBytes("a.png", SolidPng(1000, 1000));
        var resolver = new FolderImageResolver(
            folder.Path,
            [new RasterImageSource()],
            new FolderImageResolverOptions { MaxDecodedPixels = 3_500_000 });

        // Act: five embeds of the same picture, each in a 1 by 1 rectangle
        var surfaces = Enumerable.Range(0, 5).Select(_ => resolver.Resolve("a.png", 1, 1)).ToList();

        // Assert: three are drawn at their natural size, two are refused, and each refusal is a warning
        Assert.Equal([true, true, true, false, false], surfaces.Select(s => s is not null));
        Assert.All(surfaces.OfType<Surface>(), s => Assert.Equal((1000, 1000), (s.Width, s.Height)));
        Assert.Equal(3, resolver.Embedded.Count);
        Assert.Equal(2, resolver.Diagnostics.Count(d => d.Code == FolderImageResolver.TooLargeCode));
        Assert.DoesNotContain(resolver.Diagnostics, d => d.Code == FolderImageResolver.UnresolvedCode);
        foreach (var surface in surfaces)
        {
            surface?.Dispose();
        }
    }

    /// <summary>
    ///     Test that a custom source which does not charge the budget and returns a surface larger than the one that
    ///     was requested is charged the excess by the resolver: the surface that fits is kept and charged, and the
    ///     surface that does not fit is discarded (disposed) and refused with a warning.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_SourceNeverCharges_ReturnedSurfaceIsChargedAndRefusedWhenOverBudget()
    {
        // Arrange: a source that ignores the request size and always returns 20 by 20 (400 pixels); the budget is 500
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "x");
        var returned = new List<Surface>();
        var stub = new StubImageSource(
            "greedy",
            [Ext],
            null,
            _ =>
            {
                var surface = StubImageSource.Solid(20, 20, StubImageSource.Green);
                returned.Add(surface);
                return new ImageSourceResult(surface);
            });
        var resolver = new FolderImageResolver(folder.Path, [stub], new FolderImageResolverOptions { MaxDecodedPixels = 500 });

        // Act: the first is charged 400 and drawn, the second would need 400 more of the 100 that remain
        using var first = resolver.Resolve("a.stub", 10, 10);
        var second = resolver.Resolve("a.stub", 10, 10);

        // Assert
        Assert.NotNull(first);
        Assert.Null(second);
        Assert.Equal(StubImageSource.Green, first[0, 0]);
        Assert.Throws<ObjectDisposedException>(() => returned[1][0, 0]);
        Assert.Single(resolver.Embedded);
        var note = Assert.Single(resolver.Diagnostics);
        Assert.Equal(FolderImageResolver.TooLargeCode, note.Code);
    }

    /// <summary>
    ///     Test that a source that never calls <see cref="ImageSourceRequest.EnsureWithinBudget(int, int)"/> is charged
    ///     the size of the surface it returns, so the live budget the next request sees shrinks by the returned size and
    ///     not by the size requested.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_SourceReturnsMoreThanRequested_ChargesReturnedSize()
    {
        // Arrange: a source that returns 20 by 20 whatever the request says, and records the live remaining on entry
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "x");
        var seen = new List<(long Remaining, long Charged)>();
        var stub = new StubImageSource(
            "big",
            [Ext],
            null,
            request =>
            {
                seen.Add((request.RemainingPixels, request.ChargedPixels));
                return new ImageSourceResult(StubImageSource.Solid(20, 20, StubImageSource.Green));
            });
        var resolver = new FolderImageResolver(folder.Path, [stub], new FolderImageResolverOptions { MaxDecodedPixels = 1000 });

        // Act
        resolver.Resolve("a.stub", 2, 2)?.Dispose();
        resolver.Resolve("a.stub", 2, 2)?.Dispose();

        // Assert: nothing is charged before the source runs (1000), the first returned surface cost 400 (600 left), and the
        // source charged nothing itself
        Assert.Equal([(1000L, 0L), (600L, 0L)], seen);
        Assert.Equal(200L, resolver.RemainingPixels);
        Assert.Empty(resolver.Diagnostics);
    }

    /// <summary>
    ///     Test that a source which checks the budget itself, as every vector source of the repository does with the
    ///     size it is about to draw at, is refused before it allocates anything: the refusal is a warning and not an
    ///     exception, and the budget it was refused for is not charged.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_SourceRefusesBeforeDrawing_IsWarningAndNotCharged()
    {
        // Arrange: a vector-like source that wants 1000 by 1000 for one link and 10 by 10 for another, with a budget of 500,000
        using var folder = new ImageTestFolder();
        folder.WriteText("vec.stub", "x");
        folder.WriteText("small.stub", "x");
        var allocated = 0;
        var seen = new List<long>();
        var stub = new StubImageSource(
            "vector",
            [Ext],
            null,
            request =>
            {
                seen.Add(request.RemainingPixels);
                var size = request.Href.StartsWith("vec", StringComparison.Ordinal) ? 1000 : 10;
                request.EnsureWithinBudget(size, size);
                allocated++;
                return new ImageSourceResult(StubImageSource.Solid(size, size, StubImageSource.Green));
            });
        var resolver = new FolderImageResolver(folder.Path, [stub], new FolderImageResolverOptions { MaxDecodedPixels = 500_000 });

        // Act: the big one is refused, and then a small one is drawn
        var refused = resolver.Resolve("vec.stub", 10, 10);
        using var drawn = resolver.Resolve("small.stub", 10, 10);

        // Assert: the source saw the whole budget both times (the refusal charged nothing), nothing was allocated for the
        // refusal, and only the drawn picture is charged
        Assert.Null(refused);
        Assert.NotNull(drawn);
        Assert.Equal(1, allocated);
        Assert.Equal([500_000L, 500_000L], seen);
        Assert.Equal(499_900L, resolver.RemainingPixels);
        var note = Assert.Single(resolver.Diagnostics);
        Assert.Equal(FolderImageResolver.TooLargeCode, note.Code);
        Assert.Contains("1000000 pixels", note.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the resolver itself refuses a placement that does not fit in the budget, before it calls any
    ///     source: a placement of the largest size is more than the default budget.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_PlacementLargerThanBudget_IsRefusedWithoutCallingSource()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "x");
        var stub = new StubImageSource("stub", [Ext]);
        var resolver = new FolderImageResolver(folder.Path, [stub]);

        // Act: 8192 by 8192 is 67,108,864 pixels, more than the default 64,000,000
        var surface = resolver.Resolve("a.stub", Surface.MaxDimension, Surface.MaxDimension);

        // Assert: the source was not called, and the refusal does not count as an image that was loaded
        Assert.Null(surface);
        Assert.Empty(stub.Requests);
        var note = Assert.Single(resolver.Diagnostics);
        Assert.Equal(FolderImageResolver.TooLargeCode, note.Code);
    }

    /// <summary>
    ///     Test that the requested size is clamped to 1 to <see cref="Surface.MaxDimension"/> per side before it reaches
    ///     the source, and that a refusal for the budget does not use up the limit of images.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_RequestedSize_IsClampedPerSide()
    {
        // Arrange: a budget that no clamped request can exceed
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "x");
        var stub = new StubImageSource("stub", [Ext], null, _ => new ImageSourceResult(StubImageSource.Solid(1, 1, StubImageSource.Green)));
        var resolver = new FolderImageResolver(
            folder.Path,
            [stub],
            new FolderImageResolverOptions { MaxDecodedPixels = long.MaxValue, MaxImages = 3 });

        // Act
        resolver.Resolve("a.stub", 100_000, int.MaxValue)?.Dispose();
        resolver.Resolve("a.stub", 0, -5)?.Dispose();
        resolver.Resolve("a.stub", 30, 20)?.Dispose();

        // Assert
        Assert.Equal(
            [(Surface.MaxDimension, Surface.MaxDimension), (1, 1), (30, 20)],
            stub.Requests.Select(r => (r.PixelWidth!.Value, r.PixelHeight!.Value)));
        Assert.Empty(resolver.Diagnostics);
    }

    /// <summary>
    ///     Test that a refusal for the budget is not counted against <see cref="FolderImageResolverOptions.MaxImages"/>.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_RefusedForBudget_DoesNotCountAsLoadedImage()
    {
        // Arrange: room for one image only, and a budget that refuses the first request
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "x");
        var stub = new StubImageSource("stub", [Ext]);
        var resolver = new FolderImageResolver(folder.Path, [stub], new FolderImageResolverOptions { MaxImages = 1, MaxDecodedPixels = 100 });

        // Act
        var refused = resolver.Resolve("a.stub", 20, 20);
        using var drawn = resolver.Resolve("a.stub", 5, 5);

        // Assert: the second is drawn, so the first did not use the one image
        Assert.Null(refused);
        Assert.NotNull(drawn);
        Assert.Equal([FolderImageResolver.TooLargeCode], resolver.Diagnostics.Select(d => d.Code));
    }

    /// <summary>
    ///     Test that nested pictures draw on the same budget as the file that embeds them: a file that links to a picture
    ///     can only use what the outer picture left.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_NestedPictures_ShareTheBudget()
    {
        // Arrange: an outer source that charges its own 10 by 10 surface first, then loads a nested 20 by 20 picture twice;
        // the budget is 450
        using var folder = new ImageTestFolder();
        folder.WriteText("outer.stub", "inner.in");
        folder.WriteText("inner.in", "x");
        var inner = new StubImageSource("inner", [".in"], null, _ => new ImageSourceResult(StubImageSource.Solid(20, 20, StubImageSource.Green)));
        var nestedResults = new List<Surface?>();
        var outer = new StubImageSource(
            "outer",
            [Ext],
            null,
            request =>
            {
                request.EnsureWithinBudget(10, 10);
                var nested = request.Resolver!;
                nestedResults.Add(nested.Resolve("inner.in", 1, 1));
                nestedResults.Add(nested.Resolve("inner.in", 1, 1));
                return new ImageSourceResult(StubImageSource.Solid(10, 10, StubImageSource.Green));
            });
        var resolver = new FolderImageResolver(folder.Path, [inner, outer], new FolderImageResolverOptions { MaxDecodedPixels = 450 });

        // Act
        using var surface = resolver.Resolve("outer.stub", 10, 10);

        // Assert: the outer picture is charged 100, so 350 remain, and the nested 20 by 20 picture (400) fits neither time
        Assert.NotNull(surface);
        Assert.All(nestedResults, Assert.Null);
        Assert.Equal(2, resolver.Diagnostics.Count(d => d.Code == FolderImageResolver.TooLargeCode));
    }

    /// <summary>
    ///     Test that a direct render fails loudly with <see cref="ImageSourceBudgetException"/> instead of returning a
    ///     warning: for a requested size that does not fit, and for a picture whose source refuses.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Render_OverBudget_ThrowsBudgetException()
    {
        // Arrange: a 100 by 100 raster and a budget of 5000 pixels
        using var folder = new ImageTestFolder();
        folder.WriteBytes("a.png", SolidPng(100, 100));
        var resolver = new FolderImageResolver(
            folder.Path,
            [new RasterImageSource()],
            new FolderImageResolverOptions { MaxDecodedPixels = 5000 });

        // Act
        var byPath = Assert.Throws<ImageSourceBudgetException>(() => resolver.Render("a.png"));
        var byRequest = Assert.Throws<ImageSourceBudgetException>(() => resolver.Render("a.png", 100, 100));
        using var stream = new MemoryStream(SolidPng(100, 100));
        var byStream = Assert.Throws<ImageSourceBudgetException>(() => resolver.Render(stream, "b.png"));

        // Assert: the numbers say why (a natural-size render has no size to check early, so the source itself is refused
        // for the 10,000 pixels its header declares)
        Assert.Equal((10_000L, 5000L), (byPath.RequestedPixels, byPath.RemainingPixels));
        Assert.Equal((10_000L, 5000L), (byRequest.RequestedPixels, byRequest.RemainingPixels));
        Assert.Equal(10_000L, byStream.RequestedPixels);
        Assert.Empty(resolver.Diagnostics);
    }

    /// <summary>
    ///     Test that a direct render of a source that returns more than it was asked for is refused when the returned
    ///     surface does not fit, and that a picture that fits is charged so the pictures it embeds have less left.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Render_SourceReturnsTooMuch_ThrowsAndDiscardsSurface()
    {
        // Arrange: a source that ignores the budget and returns 20 by 20; the budget is 300
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "x");
        Surface? returned = null;
        var stub = new StubImageSource(
            "greedy",
            [Ext],
            null,
            _ =>
            {
                returned = StubImageSource.Solid(20, 20, StubImageSource.Green);
                return new ImageSourceResult(returned);
            });
        var resolver = new FolderImageResolver(folder.Path, [stub], new FolderImageResolverOptions { MaxDecodedPixels = 300 });

        // Act
        var ex = Assert.Throws<ImageSourceBudgetException>(() => resolver.Render("a.stub"));

        // Assert
        Assert.Equal(400L, ex.RequestedPixels);
        Assert.Throws<ObjectDisposedException>(() => returned![0, 0]);
    }

    /// <summary>
    ///     Test that the budget has a documented default and that a negative budget is rejected; zero refuses everything.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Constructor_MaxDecodedPixels_DefaultNegativeAndZero()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "x");
        var stub = new StubImageSource("stub", [Ext]);

        // Act
        var negative = Assert.Throws<ArgumentOutOfRangeException>(
            () => new FolderImageResolver(folder.Path, [stub], new FolderImageResolverOptions { MaxDecodedPixels = -1 }));
        var zero = new FolderImageResolver(folder.Path, [stub], new FolderImageResolverOptions { MaxDecodedPixels = 0 });
        var refused = zero.Resolve("a.stub", 1, 1);

        // Assert
        Assert.Contains("MaxDecodedPixels", negative.ParamName, StringComparison.Ordinal);
        Assert.Equal(64_000_000L, new FolderImageResolverOptions().MaxDecodedPixels);
        Assert.Null(refused);
        Assert.Empty(stub.Requests);
        Assert.Equal(FolderImageResolver.TooLargeCode, Assert.Single(zero.Diagnostics).Code);
    }

    /// <summary>
    ///     Test that a source which is given a budget of the default size and draws normally is not affected: the
    ///     picture is returned at its natural size, with no notes, and the source sees the whole budget.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_NormalRaster_IsUnaffectedByTheBudget()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteBytes("a.png", SolidPng(40, 30));
        var resolver = new FolderImageResolver(folder.Path, [new RasterImageSource()]);

        // Act
        using var surface = resolver.Resolve("a.png", 20, 15);

        // Assert
        Assert.NotNull(surface);
        Assert.Equal((40, 30), (surface.Width, surface.Height));
        Assert.Empty(resolver.Diagnostics);
        Assert.Equal(("a.png", "png", 40, 30), (resolver.Embedded[0].Href, resolver.Embedded[0].Format, resolver.Embedded[0].Width, resolver.Embedded[0].Height));
    }

    /// <summary>
    ///     Test that a source that charges its own surface first leaves the nested pictures and any larger surface only
    ///     what is live: with a budget of 20,000 and a 100 by 100 own surface (10,000), a nested 60 by 100 picture fits
    ///     and leaves 4,000, a second nested 100 by 100 picture is refused with <c>svg-image-too-large</c>, and a source
    ///     that returns a 150 by 100 surface (5,000 more than it charged) is refused by the resolver, so the total
    ///     never exceeds the budget and the refused source gives back what it charged.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_NestedAndLargerSurface_ShareLiveBudget()
    {
        // Arrange: an outer source that charges 100 by 100, loads two nested pictures, then returns a larger surface
        using var folder = new ImageTestFolder();
        folder.WriteText("outer.stub", "x");
        folder.WriteText("inner.in", "x");
        var inner = new StubImageSource(
            "inner",
            [".in"],
            null,
            request =>
            {
                request.EnsureWithinBudget(request.PixelWidth!.Value, request.PixelHeight!.Value);
                return new ImageSourceResult(StubImageSource.Solid(request.PixelWidth.Value, request.PixelHeight.Value, StubImageSource.Green));
            });
        Surface? nestedFits = null;
        Surface? nestedRefused = null;
        var remainingSeen = new List<long>();
        var outer = new StubImageSource(
            "outer",
            [Ext],
            null,
            request =>
            {
                remainingSeen.Add(request.RemainingPixels);
                request.EnsureWithinBudget(100, 100);
                remainingSeen.Add(request.RemainingPixels);
                nestedFits = request.Resolver!.Resolve("inner.in", 60, 100);
                remainingSeen.Add(request.RemainingPixels);
                nestedRefused = request.Resolver.Resolve("inner.in", 100, 100);
                return new ImageSourceResult(StubImageSource.Solid(150, 100, StubImageSource.Green));
            });
        var resolver = new FolderImageResolver(folder.Path, [inner, outer], new FolderImageResolverOptions { MaxDecodedPixels = 20_000 });

        // Act
        var drawn = resolver.Resolve("outer.stub", 100, 100);

        // Assert: the live remaining was 20,000, then 10,000 after the own charge, then 4,000 after the nested picture; the
        // second nested picture (10,000 > 4,000) and the 150 by 100 surface (5,000 more than charged, 4,000 left) are
        // refused; the outer's own 10,000 went back, the nested picture stays charged
        Assert.Equal([20_000L, 10_000L, 4_000L], remainingSeen);
        Assert.NotNull(nestedFits);
        Assert.Equal((60, 100), (nestedFits.Width, nestedFits.Height));
        Assert.Null(nestedRefused);
        Assert.Null(drawn);
        Assert.Equal(2, resolver.Diagnostics.Count(d => d.Code == FolderImageResolver.TooLargeCode));
        Assert.Equal(["inner.in"], resolver.Embedded.Select(e => e.Href));
        Assert.Equal(0L, Assert.Single(outer.Requests).ChargedPixels);
        Assert.Equal(14_000L, resolver.RemainingPixels);
        nestedFits.Dispose();
    }

    /// <summary>
    ///     Test that a source which uses exactly what is left after its nested picture is accepted and spends the whole
    ///     budget: the 140 by 100 surface is 4,000 more than the 10,000 the source charged and exactly 4,000 remain.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_LargerSurfaceThatFitsAfterNestedPicture_IsAcceptedAndSpendsTheBudget()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("outer.stub", "x");
        folder.WriteText("inner.in", "x");
        var inner = new StubImageSource(
            "inner",
            [".in"],
            null,
            request =>
            {
                request.EnsureWithinBudget(request.PixelWidth!.Value, request.PixelHeight!.Value);
                return new ImageSourceResult(StubImageSource.Solid(request.PixelWidth.Value, request.PixelHeight.Value, StubImageSource.Green));
            });
        var outer = new StubImageSource(
            "outer",
            [Ext],
            null,
            request =>
            {
                request.EnsureWithinBudget(100, 100);
                request.Resolver!.Resolve("inner.in", 60, 100)?.Dispose();
                return new ImageSourceResult(StubImageSource.Solid(140, 100, StubImageSource.Green));
            });
        var resolver = new FolderImageResolver(folder.Path, [inner, outer], new FolderImageResolverOptions { MaxDecodedPixels = 20_000 });

        // Act
        using var drawn = resolver.Resolve("outer.stub", 100, 100);

        // Assert: 10,000 charged + 6,000 nested + 4,000 more than charged is the whole budget
        Assert.NotNull(drawn);
        Assert.Equal((140, 100), (drawn.Width, drawn.Height));
        Assert.Equal(0L, resolver.RemainingPixels);
        Assert.Empty(resolver.Diagnostics);
    }

    /// <summary>
    ///     Test that a source which draws exactly the size it charged is not charged twice, even when that is the whole
    ///     budget, and that nothing else fits afterwards.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_SourceChargingExactlyTheBudget_IsNotChargedTwice()
    {
        // Arrange: the budget is exactly the requested 100 by 100; the source charges it, then tries one more pixel
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "x");
        ImageSourceBudgetException? tooMuch = null;
        var stub = new StubImageSource(
            "vector",
            [Ext],
            null,
            request =>
            {
                request.EnsureWithinBudget(request.PixelWidth!.Value, request.PixelHeight!.Value);
                tooMuch = Assert.Throws<ImageSourceBudgetException>(() => request.EnsureWithinBudget(1, 1));
                return new ImageSourceResult(StubImageSource.Solid(request.PixelWidth.Value, request.PixelHeight.Value, StubImageSource.Green));
            });
        var resolver = new FolderImageResolver(folder.Path, [stub], new FolderImageResolverOptions { MaxDecodedPixels = 10_000 });

        // Act
        using var drawn = resolver.Resolve("a.stub", 100, 100);

        // Assert: drawn, nothing left, the charge stays on the request, and the refusal reports the live numbers
        Assert.NotNull(drawn);
        Assert.Equal((10_000L, 0L), (stub.Requests[0].ChargedPixels, resolver.RemainingPixels));
        Assert.Equal((1L, 0L), (tooMuch!.RequestedPixels, tooMuch.RemainingPixels));
        Assert.Empty(resolver.Diagnostics);
    }

    /// <summary>
    ///     Test the regression of the stale snapshot: a source that first loads a 6,000-pixel nested picture (charged
    ///     live, so 14,000 of 20,000 remain) and then asks for an 18,000-pixel surface of its own is refused before it
    ///     allocates anything; the link is a <c>svg-image-too-large</c> warning, the parent's own charge is zero, and
    ///     nothing beyond the nested picture is left charged.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_OwnSurfaceAfterNestedCharge_IsRefusedByLiveRemaining()
    {
        // Arrange: the parent resolves a 60 by 100 nested picture, then wants 180 by 100 for itself
        using var folder = new ImageTestFolder();
        folder.WriteText("outer.stub", "x");
        folder.WriteText("inner.in", "x");
        var inner = new StubImageSource(
            "inner",
            [".in"],
            null,
            request =>
            {
                request.EnsureWithinBudget(request.PixelWidth!.Value, request.PixelHeight!.Value);
                return new ImageSourceResult(StubImageSource.Solid(request.PixelWidth.Value, request.PixelHeight.Value, StubImageSource.Green));
            });
        long remainingAfterNested = 0;
        var allocated = false;
        var outer = new StubImageSource(
            "outer",
            [Ext],
            null,
            request =>
            {
                request.Resolver!.Resolve("inner.in", 60, 100)?.Dispose();
                remainingAfterNested = request.RemainingPixels;
                request.EnsureWithinBudget(180, 100);
                allocated = true;
                return new ImageSourceResult(StubImageSource.Solid(180, 100, StubImageSource.Green));
            });
        var resolver = new FolderImageResolver(folder.Path, [inner, outer], new FolderImageResolverOptions { MaxDecodedPixels = 20_000 });

        // Act
        var drawn = resolver.Resolve("outer.stub", 10, 10);

        // Assert
        Assert.Null(drawn);
        Assert.False(allocated);
        Assert.Equal(14_000L, remainingAfterNested);
        var note = Assert.Single(resolver.Diagnostics);
        Assert.Equal(FolderImageResolver.TooLargeCode, note.Code);
        Assert.Contains("18000 pixels", note.Message, StringComparison.Ordinal);
        Assert.Equal(0L, Assert.Single(outer.Requests).ChargedPixels);
        Assert.Equal(14_000L, resolver.RemainingPixels);
    }

    /// <summary>
    ///     Test that sibling pictures of one document accumulate on the live budget: each charged picture lowers what the
    ///     next one sees, so the pictures that fit are drawn and the one that no longer fits is refused by its source.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_NestedSiblings_AccumulateCharges()
    {
        // Arrange: a parent that charges 100 and loads three 20 by 20 pictures; the budget is 1,000
        using var folder = new ImageTestFolder();
        folder.WriteText("outer.stub", "x");
        folder.WriteText("inner.in", "x");
        var inner = new StubImageSource(
            "inner",
            [".in"],
            null,
            request =>
            {
                request.EnsureWithinBudget(20, 20);
                return new ImageSourceResult(StubImageSource.Solid(20, 20, StubImageSource.Green));
            });
        var siblings = new List<Surface?>();
        var remainingSeen = new List<long>();
        var outer = new StubImageSource(
            "outer",
            [Ext],
            null,
            request =>
            {
                request.EnsureWithinBudget(10, 10);
                for (var i = 0; i < 3; i++)
                {
                    remainingSeen.Add(request.RemainingPixels);
                    siblings.Add(request.Resolver!.Resolve("inner.in", 20, 20));
                }

                return new ImageSourceResult(StubImageSource.Solid(10, 10, StubImageSource.Green));
            });
        var resolver = new FolderImageResolver(folder.Path, [inner, outer], new FolderImageResolverOptions { MaxDecodedPixels = 1000 });

        // Act
        using var drawn = resolver.Resolve("outer.stub", 10, 10);

        // Assert: 900, 500, 100 remained before each sibling; the first two are drawn (and stay charged), the third is not
        Assert.NotNull(drawn);
        Assert.Equal([900L, 500L, 100L], remainingSeen);
        Assert.Equal([true, true, false], siblings.Select(s => s is not null));
        Assert.Equal(1, resolver.Diagnostics.Count(d => d.Code == FolderImageResolver.TooLargeCode));
        Assert.Equal(100L, resolver.RemainingPixels);
        foreach (var sibling in siblings)
        {
            sibling?.Dispose();
        }
    }

    /// <summary>
    ///     Test that a source which fails after it charged its surface gives the charge back, so a failed source leaks no
    ///     budget: for a source that throws an ordinary exception (the link is a failure, as before) and for one that
    ///     is refused for a second surface (the link is a warning).
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_SourceFailsAfterCharging_RefundsTheCharge()
    {
        // Arrange: one source charges 100 then throws, the other charges 100 and is refused for a second, huge surface
        using var folder = new ImageTestFolder();
        folder.WriteText("throws.stub", "x");
        folder.WriteText("refused.stub", "x");
        var stub = new StubImageSource(
            "failing",
            [Ext],
            null,
            request =>
            {
                request.EnsureWithinBudget(10, 10);
                if (request.Href.StartsWith("throws", StringComparison.Ordinal))
                {
                    throw new InvalidDataException("broken");
                }

                request.EnsureWithinBudget(1000, 1000);
                return new ImageSourceResult(StubImageSource.Solid(10, 10, StubImageSource.Green));
            });
        var resolver = new FolderImageResolver(folder.Path, [stub], new FolderImageResolverOptions { MaxDecodedPixels = 5000 });

        // Act
        var failure = Assert.Throws<ImageSourceException>(() => resolver.Resolve("throws.stub", 10, 10));
        var remainingAfterFailure = resolver.RemainingPixels;
        var refused = resolver.Resolve("refused.stub", 10, 10);

        // Assert: both charges of 100 were given back
        Assert.IsType<InvalidDataException>(failure.InnerException);
        Assert.Equal(5000L, remainingAfterFailure);
        Assert.Null(refused);
        Assert.Equal(5000L, resolver.RemainingPixels);
        Assert.Equal(FolderImageResolver.TooLargeCode, Assert.Single(resolver.Diagnostics).Code);
        Assert.All(stub.Requests, request => Assert.Equal(0L, request.ChargedPixels));
    }

    /// <summary>
    ///     Test that a direct render gives a failed source's charge back too: a source that throws after it charged
    ///     leaves the budget as it was.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Render_SourceFailsAfterCharging_RefundsTheCharge()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "x");
        var stub = new StubImageSource(
            "failing",
            [Ext],
            null,
            request =>
            {
                request.EnsureWithinBudget(10, 10);
                throw new InvalidDataException("broken");
            });
        var resolver = new FolderImageResolver(folder.Path, [stub], new FolderImageResolverOptions { MaxDecodedPixels = 5000 });

        // Act
        Assert.Throws<ImageSourceException>(() => resolver.Render("a.stub"));

        // Assert
        Assert.Equal(5000L, resolver.RemainingPixels);
    }

    /// <summary>
    ///     Test that an image beyond the image-count limit is refused before its file is read: a file larger than the
    ///     resolver can read would otherwise be reported as too large instead of as one image too many.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_ImageCountExhausted_NeverReadsTheEmbeddedFile()
    {
        // Arrange: the limit is zero images, and the file is larger than the 16 MiB the resolver reads
        using var folder = new ImageTestFolder();
        var path = folder.Combine("big.stub");
        using (var file = File.Create(path))
        {
            file.SetLength(FileLimit + 1);
        }

        var stub = new StubImageSource("stub", [Ext]);
        var resolver = new FolderImageResolver(folder.Path, [stub], new FolderImageResolverOptions { MaxImages = 0 });

        // Act
        var surface = resolver.Resolve("big.stub", 1, 1);

        // Assert: the reason is the count limit, so the file was never opened
        Assert.Null(surface);
        Assert.Empty(stub.Requests);
        var note = Assert.Single(resolver.Diagnostics);
        Assert.Equal(FolderImageResolver.UnresolvedCode, note.Code);
        Assert.Contains("more than 0 images", note.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the file engine of the resolver reads exactly <see cref="FolderImageResolverOptions.MaxImages"/> distinct embedded files
    ///     for a render by path (which also reads the top-level file) and for a render of a stream (which does not):
    ///     files that are read but refused for the pixel budget do not count as images, so only the file limit of the
    ///     engine stops the third one.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Render_PathAndStream_SandboxFileSlotsMatchTheImageLimit()
    {
        // Arrange: a top-level file that links to three files at a size that never fits (so none is counted as an image),
        // a budget of one pixel for the 1 by 1 picture of the top-level file, and a limit of two images
        using var folder = new ImageTestFolder();
        folder.WriteText("top.stub", "x");
        foreach (var name in new[] { "a.in", "b.in", "c.in" })
        {
            folder.WriteText(name, "x");
        }

        var inner = new StubImageSource("inner", [".in"]);
        var options = new FolderImageResolverOptions { MaxImages = 2, MaxDecodedPixels = 1 };
        var top = new StubImageSource(
            "top",
            [Ext],
            null,
            request =>
            {
                foreach (var name in new[] { "a.in", "b.in", "c.in" })
                {
                    request.Resolver!.Resolve(name, 2, 2);
                }

                return new ImageSourceResult(StubImageSource.Solid(1, 1, StubImageSource.Green));
            });
        var byPath = new FolderImageResolver(folder.Path, [inner, top], options);
        var byStream = new FolderImageResolver(folder.Path, [inner, top], options);
        using var content = new MemoryStream([(byte)'x']);

        // Act
        byPath.Render("top.stub").Surface.Dispose();
        byStream.Render(content, "top.stub").Surface.Dispose();

        // Assert: the first two embedded files are read and refused for the budget; the third finds no free file slot
        string[] expected = [FolderImageResolver.TooLargeCode, FolderImageResolver.TooLargeCode, FolderImageResolver.UnresolvedCode];
        Assert.Equal(expected, byPath.Diagnostics.Select(d => d.Code));
        Assert.Equal(expected, byStream.Diagnostics.Select(d => d.Code));
    }

    /// <summary>
    ///     Test that <see cref="FolderImageResolver.RemainingPixels"/> is the whole budget at the start and decreases by
    ///     what a picture is charged, and that a refusal charges nothing.
    /// </summary>
    [Fact]
    public void FolderImageResolver_RemainingPixels_StartsFullAndDecreasesAfterEmbed()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "x");
        var stub = new StubImageSource("stub", [Ext], null, _ => new ImageSourceResult(StubImageSource.Solid(10, 10, StubImageSource.Green)));
        var resolver = new FolderImageResolver(folder.Path, [stub], new FolderImageResolverOptions { MaxDecodedPixels = 1000 });

        // Act
        var atStart = resolver.RemainingPixels;
        resolver.Resolve("a.stub", 10, 10)?.Dispose();
        var afterEmbed = resolver.RemainingPixels;
        resolver.Resolve("a.stub", 100, 100)?.Dispose();
        var afterRefusal = resolver.RemainingPixels;

        // Assert
        Assert.Equal(1000L, atStart);
        Assert.Equal(900L, afterEmbed);
        Assert.Equal(900L, afterRefusal);
        Assert.Equal(FolderImageResolver.TooLargeCode, Assert.Single(resolver.Diagnostics).Code);
    }
}

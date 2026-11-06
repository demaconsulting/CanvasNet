// cspell:ignore hrefs embedder
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Tests.ImageSources;

/// <summary>
///     Unit tests for the contract types of the image sources: <see cref="ImageSourceRequest"/> (its sizing rule and
///     its content), <see cref="ImageSourceResult"/>, <see cref="ImageSourceDiagnostic"/>,
///     <see cref="EmbeddedImage"/> and <see cref="ImageSourceException"/>.
/// </summary>
public class ImageSourceContractTests
{
    /// <summary>
    ///     Creates a request with a requested size.
    /// </summary>
    /// <param name="width">The requested width.</param>
    /// <param name="height">The requested height.</param>
    /// <returns>The request.</returns>
    private static ImageSourceRequest Sized(int? width, int? height)
    {
        return new ImageSourceRequest { Href = "x", PixelWidth = width, PixelHeight = height };
    }

    /// <summary>
    ///     Test the sizing rule of <see cref="ImageSourceRequest.SizeFor(int, int, bool)"/>: natural, one dimension, both
    ///     (the box or fitted inside it), and the clamping.
    /// </summary>
    /// <param name="requestedWidth">The requested width.</param>
    /// <param name="requestedHeight">The requested height.</param>
    /// <param name="fitInside">Whether to fit inside the box.</param>
    /// <param name="expectedWidth">The expected width.</param>
    /// <param name="expectedHeight">The expected height.</param>
    [Theory]
    [InlineData(null, null, false, 200, 100)]
    [InlineData(null, null, true, 200, 100)]
    [InlineData(50, null, false, 50, 25)]
    [InlineData(null, 50, false, 100, 50)]
    [InlineData(30, 30, false, 30, 30)]
    [InlineData(30, 30, true, 30, 15)]
    [InlineData(400, 100, true, 200, 100)]
    [InlineData(1, null, false, 1, 1)]
    [InlineData(0, -5, false, 1, 1)]
    [InlineData(20000, 20000, false, 8192, 8192)]
    [InlineData(20000, null, false, 8192, 8192)]
    public void ImageSourceRequest_SizeFor_AppliesSizingRule(int? requestedWidth, int? requestedHeight, bool fitInside, int expectedWidth, int expectedHeight)
    {
        // Arrange: a picture of 200 by 100 pixels
        var request = Sized(requestedWidth, requestedHeight);

        // Act
        var size = request.SizeFor(200, 100, fitInside);

        // Assert
        Assert.Equal((expectedWidth, expectedHeight), size);
    }

    /// <summary>
    ///     Test that a natural size below one pixel is rejected, and a natural size above the maximum is clamped.
    /// </summary>
    [Fact]
    public void ImageSourceRequest_SizeFor_InvalidOrHugeNaturalSize_ThrowsOrClamps()
    {
        // Arrange
        var request = Sized(null, null);

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => request.SizeFor(0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => request.SizeFor(10, 0));
        Assert.Equal((Surface.MaxDimension, 5), request.SizeFor(100000, 5));
    }

    /// <summary>
    ///     Test that a request with a file name has content that opens as a read-only seekable stream, over both an
    ///     array-backed and a non-array memory, and that an empty file still counts as having content.
    /// </summary>
    [Fact]
    public void ImageSourceRequest_OpenContent_FileRequest_ReturnsReadOnlySeekableStream()
    {
        // Arrange: an array slice, a memory that is not array-backed, and an empty file
        var bytes = new byte[] { 9, 1, 2, 3, 9 };
        var slice = new ImageSourceRequest { Href = "a", FileName = "a", Content = new ReadOnlyMemory<byte>(bytes, 1, 3) };
        var native = new ImageSourceRequest { Href = "a", FileName = "a", Content = new ForeignMemory([4, 5]).Memory };
        var empty = new ImageSourceRequest { Href = "a", FileName = "a" };

        // Act
        using var sliceStream = slice.OpenContent();
        using var nativeStream = native.OpenContent();
        using var emptyStream = empty.OpenContent();

        // Assert
        Assert.True(slice.HasContent);
        Assert.True(empty.HasContent);
        Assert.False(sliceStream.CanWrite);
        Assert.True(sliceStream.CanSeek);
        var copy = new MemoryStream();
        sliceStream.CopyTo(copy);
        Assert.Equal([1, 2, 3], copy.ToArray());
        var copy2 = new MemoryStream();
        nativeStream.CopyTo(copy2);
        Assert.Equal([4, 5], copy2.ToArray());
        Assert.Equal(0, emptyStream.Length);
    }

    /// <summary>
    ///     Test that a request without a file name (a scheme-claimed request) has no content and refuses to open it.
    /// </summary>
    [Fact]
    public void ImageSourceRequest_OpenContent_SchemeRequest_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new ImageSourceRequest { Href = "test-scheme:frame" };

        // Act / Assert
        Assert.False(request.HasContent);
        Assert.Null(request.FileName);
        Assert.Null(request.Directory);
        Assert.Null(request.Resolver);
        Assert.False(request.ThrowOnUnsupportedFeature);
        Assert.Equal(CancellationToken.None, request.CancellationToken);
        var error = Assert.Throws<InvalidOperationException>(() => request.OpenContent());
        Assert.Contains("test-scheme:frame", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a result needs a surface, has empty notes by default and keeps what it is given.
    /// </summary>
    [Fact]
    public void ImageSourceResult_Constructor_RequiresSurfaceAndDefaultsToEmpty()
    {
        // Arrange
        using var surface = new Surface(2, 2);

        // Act
        var plain = new ImageSourceResult(surface);
        var full = new ImageSourceResult(surface)
        {
            Diagnostics = [new ImageSourceDiagnostic("s", ImageDiagnosticSeverity.Info, "c", "m")],
            Echo = ["e"],
            Format = "f"
        };

        // Assert
        Assert.Throws<ArgumentNullException>(() => new ImageSourceResult(null!));
        Assert.Same(surface, plain.Surface);
        Assert.Empty(plain.Diagnostics);
        Assert.Empty(plain.Echo);
        Assert.Null(plain.Format);
        Assert.Equal(("s", ImageDiagnosticSeverity.Info, "c", "m"), (full.Diagnostics[0].Source, full.Diagnostics[0].Severity, full.Diagnostics[0].Code, full.Diagnostics[0].Message));
        Assert.Equal(["e"], full.Echo);
        Assert.Equal("f", full.Format);
    }

    /// <summary>
    ///     Test that a hand-built request has no limit, that a request given an <see cref="ImageBudget"/> charges
    ///     <see cref="ImageSourceRequest.EnsureWithinBudget(int, int)"/> against it (the sizes of several calls add up,
    ///     exactly the remainder fits), that a call that does not fit throws <see cref="ImageSourceBudgetException"/>
    ///     with the numbers and charges nothing, and that the exception message names them.
    /// </summary>
    [Fact]
    public void ImageSourceRequest_EnsureWithinBudget_ChargesLiveBudgetAndThrowsOnlyWhenOver()
    {
        // Arrange
        var unlimited = Sized(null, null);
        var budget = new ImageBudget(600);
        var limited = new ImageSourceRequest { Href = "x", Budget = budget };

        // Act
        unlimited.EnsureWithinBudget(Surface.MaxDimension, Surface.MaxDimension);
        limited.EnsureWithinBudget(0, 1000);
        limited.EnsureWithinBudget(20, 29);
        var ex = Assert.Throws<ImageSourceBudgetException>(() => limited.EnsureWithinBudget(21, 1));
        limited.EnsureWithinBudget(20, 1);

        // Assert
        Assert.Equal(long.MaxValue, Sized(null, null).RemainingPixels);
        Assert.Equal((long)Surface.MaxDimension * Surface.MaxDimension, unlimited.ChargedPixels);
        Assert.Equal((21L, 20L), (ex.RequestedPixels, ex.RemainingPixels));
        Assert.Contains("21", ex.Message, StringComparison.Ordinal);
        Assert.Contains("20", ex.Message, StringComparison.Ordinal);
        Assert.Equal((600L, 0L, 0L), (limited.ChargedPixels, limited.RemainingPixels, budget.Remaining));
    }

    /// <summary>
    ///     Test that requests that share one <see cref="ImageBudget"/> see each other's charges live, and that each
    ///     keeps its own <see cref="ImageSourceRequest.ChargedPixels"/>.
    /// </summary>
    [Fact]
    public void ImageSourceRequest_EnsureWithinBudget_SharedBudget_SeesChargesOfOtherRequests()
    {
        // Arrange: two requests on one budget of 1000 pixels
        var budget = new ImageBudget(1000);
        var first = new ImageSourceRequest { Href = "a", Budget = budget };
        var second = new ImageSourceRequest { Href = "b", Budget = budget };

        // Act
        first.EnsureWithinBudget(10, 60);
        var ex = Assert.Throws<ImageSourceBudgetException>(() => second.EnsureWithinBudget(10, 41));

        // Assert
        Assert.Equal(400L, second.RemainingPixels);
        Assert.Equal((410L, 400L), (ex.RequestedPixels, ex.RemainingPixels));
        Assert.Equal((600L, 0L), (first.ChargedPixels, second.ChargedPixels));
    }

    /// <summary>
    ///     Test that a budget starts with all its pixels and rejects a negative maximum; zero is valid.
    /// </summary>
    [Fact]
    public void ImageBudget_Constructor_StartsFullAndRejectsNegative()
    {
        // Arrange / Act
        var budget = new ImageBudget(250);
        var zero = new ImageBudget(0);

        // Assert
        Assert.Equal(250L, budget.Remaining);
        Assert.Equal(0L, zero.Remaining);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImageBudget(-1));
    }

    /// <summary>
    ///     Test that the lists a result is given are copied into read-only snapshots: changing the original lists
    ///     afterwards does not change the result, the values cannot be changed through a cast, null becomes empty, and
    ///     <c>with</c> keeps working.
    /// </summary>
    [Fact]
    public void ImageSourceResult_Init_CopiesListsIntoReadOnlySnapshots()
    {
        // Arrange
        using var surface = new Surface(2, 2);
        var note = new ImageSourceDiagnostic("s", ImageDiagnosticSeverity.Info, "c", "m");
        var notes = new List<ImageSourceDiagnostic> { note };
        var echo = new List<string> { "e" };
        var result = new ImageSourceResult(surface) { Diagnostics = notes, Echo = echo };

        // Act
        notes.Add(note);
        echo.Add("f");
        var renamed = result with { Format = "x" };
        var replaced = result with { Echo = ["g"] };
        var nulls = new ImageSourceResult(surface) { Diagnostics = null!, Echo = null! };

        // Assert
        Assert.Equal([note], result.Diagnostics);
        Assert.Equal(["e"], result.Echo);
        ReadOnlyListAssert.IsReadOnly(result.Diagnostics, note);
        ReadOnlyListAssert.IsReadOnly(result.Echo, "x");
        Assert.Equal("x", renamed.Format);
        Assert.Equal(["e"], renamed.Echo);
        Assert.Equal(["g"], replaced.Echo);
        Assert.Empty(nulls.Diagnostics);
        Assert.Empty(nulls.Echo);
    }

    /// <summary>
    ///     Test the shape of the small value types: severity names, the embedded image record and its depth.
    /// </summary>
    [Fact]
    public void ImageSourceContract_ValueTypes_HaveDocumentedShape()
    {
        // Arrange / Act
        var embedded = new EmbeddedImage("a.png", "png", 4, 3) { Depth = 2 };

        // Assert
        Assert.Equal(["Info", "Warning", "Unsupported"], Enum.GetNames<ImageDiagnosticSeverity>());
        Assert.Equal(("a.png", "png", 4, 3, 2), (embedded.Href, embedded.Format, embedded.Width, embedded.Height, embedded.Depth));
        Assert.Equal(0, new EmbeddedImage("a", "b", 1, 1).Depth);
        Assert.Equal("svg-image-unresolved", FolderImageResolver.UnresolvedCode);
        Assert.Equal("svg-image-too-large", FolderImageResolver.TooLargeCode);
        var options = new FolderImageResolverOptions();
        Assert.Equal((4, 64, false), (options.MaxDepth, options.MaxImages, options.ThrowOnUnsupportedFeature));
        Assert.Equal(CancellationToken.None, options.CancellationToken);
    }

    /// <summary>
    ///     Test that the exception keeps the link, the embedder and the inner exception.
    /// </summary>
    [Fact]
    public void ImageSourceException_Constructor_KeepsHrefEmbedderAndInner()
    {
        // Arrange
        var inner = new InvalidDataException("broken");

        // Act
        var exception = new ImageSourceException("failed", "a.png", "outer.svg", inner);
        var top = new ImageSourceException("failed", "a.png", null, inner);

        // Assert
        Assert.Equal("failed", exception.Message);
        Assert.Equal("a.png", exception.Href);
        Assert.Equal("outer.svg", exception.Embedder);
        Assert.Same(inner, exception.InnerException);
        Assert.Null(top.Embedder);
    }

    /// <summary>
    ///     Test that a new request has the empty fragment and no options, and that both can be set.
    /// </summary>
    [Fact]
    public void ImageSourceRequest_Fragment_DefaultsToEmptyAndNoOptions()
    {
        // Arrange
        var fragment = ImageFragment.Parse("page=2");
        var options = new ContractOptions();

        // Act
        var plain = new ImageSourceRequest { Href = "a.pdf" };
        var full = new ImageSourceRequest { Href = "a.pdf#page=2", Fragment = fragment, Options = options };

        // Assert
        Assert.Same(ImageFragment.Empty, plain.Fragment);
        Assert.Null(plain.Options);
        Assert.Same(fragment, full.Fragment);
        Assert.Same(options, full.Options);
    }

    /// <summary>
    ///     Test that the detail, location and page of a note take part in equality and hashing, and default to null.
    /// </summary>
    [Fact]
    public void ImageSourceDiagnostic_Equals_ComparesDetailLocationAndPage()
    {
        // Arrange
        var location = new ImageDiagnosticLocation("a.scad", 3, 4, null);
        var plain = new ImageSourceDiagnostic("scad", ImageDiagnosticSeverity.Warning, "scad-warning", "m");
        var full = plain with { Detail = "scad-anchor-invalid", Location = location, Page = 2 };
        var same = plain with { Detail = "scad-anchor-invalid", Location = new ImageDiagnosticLocation("a.scad", 3, 4, null), Page = 2 };

        // Act / Assert
        Assert.Null(plain.Detail);
        Assert.Null(plain.Location);
        Assert.Null(plain.Page);
        Assert.Equal(full, same);
        Assert.Equal(full.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(plain, full);
        Assert.NotEqual(plain, plain with { Detail = "x" });
        Assert.NotEqual(plain, plain with { Location = location });
        Assert.NotEqual(plain, plain with { Page = 1 });
    }

    /// <summary>
    ///     Test that the refused exception keeps its code and message and validates its arguments.
    /// </summary>
    [Fact]
    public void ImageSourceRefusedException_Constructor_KeepsCodeAndMessageAndValidates()
    {
        // Act
        var exception = new ImageSourceRefusedException("image-page-out-of-range", "page 3 does not exist");

        // Assert
        Assert.Equal("image-page-out-of-range", exception.Code);
        Assert.Equal("page 3 does not exist", exception.Message);
        Assert.Throws<ArgumentException>(() => new ImageSourceRefusedException("", "m"));
        Assert.Throws<ArgumentNullException>(() => new ImageSourceRefusedException(null!, "m"));
        Assert.Throws<ArgumentNullException>(() => new ImageSourceRefusedException("c", null!));
    }

    /// <summary>
    ///     Test the shape of the location record: value equality and null members.
    /// </summary>
    [Fact]
    public void ImageDiagnosticLocation_Equality_IsByValue()
    {
        // Act / Assert
        Assert.Equal(new ImageDiagnosticLocation("f", 1, 2, "/p"), new ImageDiagnosticLocation("f", 1, 2, "/p"));
        Assert.NotEqual(new ImageDiagnosticLocation("f", 1, 2, null), new ImageDiagnosticLocation("f", 1, 3, null));
        Assert.Null(new ImageDiagnosticLocation(null, null, null, null).Pointer);
    }

    /// <summary>
    ///     Options of a direct render for the tests above.
    /// </summary>
    private sealed class ContractOptions : IImageSourceOptions
    {
    }

    /// <summary>
    ///     A memory owner that is not backed by a plain array, to reach the copying branch of
    ///     <see cref="ImageSourceRequest.OpenContent"/>.
    /// </summary>
    private sealed class ForeignMemory : System.Buffers.MemoryManager<byte>
    {
        /// <summary>The bytes.</summary>
        private readonly byte[] _bytes;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ForeignMemory"/> class.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        public ForeignMemory(byte[] bytes)
        {
            _bytes = bytes;
        }

        /// <inheritdoc />
        public override Span<byte> GetSpan()
        {
            return _bytes;
        }

        /// <inheritdoc />
        public override System.Buffers.MemoryHandle Pin(int elementIndex = 0)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void Unpin()
        {
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
        }
    }
}

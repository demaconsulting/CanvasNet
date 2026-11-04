// cspell:ignore href hrefs fsecret embedder
using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Files;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Tests.ImageSources;

/// <summary>
///     Unit tests for <see cref="FolderImageResolver"/>: where a link may point, which source draws it, the limits, the
///     notes, the errors, the URI schemes, and the absence of caching. Stub sources stand in for the formats.
/// </summary>
public class FolderImageResolverTests
{
    /// <summary>The name of the extension the default stub claims.</summary>
    private const string Ext = ".stub";

    /// <summary>
    ///     Creates the default stub: it claims <see cref="Ext"/> and draws a 4 by 4 green surface.
    /// </summary>
    /// <returns>The stub.</returns>
    private static StubImageSource Stub()
    {
        return new StubImageSource("stub", [Ext]);
    }

    /// <summary>
    ///     Creates a stub that renders the file it is given by resolving the link that the file's text holds (a chain).
    /// </summary>
    /// <param name="extensions">The claimed extensions.</param>
    /// <returns>The stub.</returns>
    private static StubImageSource Chaining(params string[] extensions)
    {
        return new StubImageSource(
            "chain",
            extensions,
            null,
            request =>
            {
                var next = Encoding.UTF8.GetString(request.Content.Span).Trim();
                if (next.Length > 0)
                {
                    request.Resolver!.Resolve(next, 10, 10)?.Dispose();
                }

                return new ImageSourceResult(StubImageSource.Solid(4, 4, StubImageSource.Green));
            });
    }

    /// <summary>
    ///     Test that a relative link reaches the source with the content, name, folder, size, switches and token of the
    ///     request, and that the surface is the one the source drew.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_RelativeFile_PassesCompleteRequestToSource()
    {
        // Arrange: a file in a sub folder, and a resolver with the switches set
        using var folder = new ImageTestFolder();
        folder.WriteText("sub/a.stub", "hello");
        var stub = Stub();
        using var cts = new CancellationTokenSource();
        var options = new FolderImageResolverOptions { ThrowOnUnsupportedFeature = true, CancellationToken = cts.Token };
        var resolver = new FolderImageResolver(folder.Path, [stub], options);

        // Act
        using var surface = resolver.Resolve("sub/a.stub", 30, 20);

        // Assert
        Assert.NotNull(surface);
        Assert.Equal(StubImageSource.Green, surface[0, 0]);
        var request = Assert.Single(stub.Requests);
        Assert.Equal("sub/a.stub", request.Href);
        Assert.Equal("sub/a.stub", request.FileName);
        Assert.Equal(folder.Combine("sub"), request.Directory);
        Assert.True(request.HasContent);
        Assert.Equal("hello", Encoding.UTF8.GetString(request.Content.Span));
        Assert.Equal((30, 20), (request.PixelWidth, request.PixelHeight));
        Assert.True(request.ThrowOnUnsupportedFeature);
        Assert.Equal(cts.Token, request.CancellationToken);
        Assert.NotNull(request.Resolver);
        Assert.Empty(resolver.Diagnostics);
    }

    /// <summary>
    ///     Test that a link that leaves the folder, is absolute, a drive or UNC path, an alternate data stream, a network
    ///     URL or contains a control character is an unresolved warning, never reaches a source, and never reads the file.
    /// </summary>
    /// <param name="href">The link.</param>
    [Theory]
    [InlineData("../secret.stub")]
    [InlineData("sub/../../secret.stub")]
    [InlineData("..%2Fsecret.stub")]
    [InlineData("%2e%2e/secret.stub")]
    [InlineData("..\\secret.stub")]
    [InlineData("/secret.stub")]
    [InlineData("\\secret.stub")]
    [InlineData("C:/secret.stub")]
    [InlineData("C:\\secret.stub")]
    [InlineData("\\\\server\\share\\secret.stub")]
    [InlineData("secret.stub::$DATA")]
    [InlineData("secret.stub%00.stub")]
    [InlineData("http://example.invalid/secret.stub")]
    [InlineData("https://example.invalid/secret.stub")]
    [InlineData("ftp://example.invalid/secret.stub")]
    [InlineData("file://host/share/secret.stub")]
    public void FolderImageResolver_Resolve_EscapingReference_IsUnresolvedWarning(string href)
    {
        // Arrange: a valid file one folder above the folder of the resolver
        using var folder = new ImageTestFolder();
        Directory.CreateDirectory(folder.Combine("site"));
        folder.WriteText("secret.stub", "secret");
        var stub = Stub();
        var resolver = new FolderImageResolver(folder.Combine("site"), [stub]);

        // Act
        var surface = resolver.Resolve(href, 10, 10);

        // Assert: nothing was drawn, the source was never called, and the one note is the warning naming the link
        Assert.Null(surface);
        Assert.Empty(stub.Requests);
        var note = Assert.Single(resolver.Diagnostics);
        Assert.Equal(("svg", ImageDiagnosticSeverity.Warning, FolderImageResolver.UnresolvedCode), (note.Source, note.Severity, note.Code));
        Assert.Contains($"'{href}'", note.Message, StringComparison.Ordinal);
        Assert.Empty(resolver.Embedded);
    }

    /// <summary>
    ///     Test that a <c>file:</c> URI inside the folder is loaded, that one outside it is refused, and that a
    ///     percent-encoded relative path is decoded.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_FileUriAndEncodedPath_InsideFolderIsDrawnOutsideIsNot()
    {
        // Arrange: files inside and outside the folder, and a file name with a space
        using var folder = new ImageTestFolder();
        folder.WriteText("outside.stub", "x");
        folder.WriteText("site/inside.stub", "x");
        folder.WriteText("site/my img.stub", "x");
        var outside = new Uri(folder.Combine("outside.stub")).AbsoluteUri;
        var inside = new Uri(folder.Combine("site", "inside.stub")).AbsoluteUri;
        var stub = Stub();
        var resolver = new FolderImageResolver(folder.Combine("site"), [stub]);

        // Act
        var refused = resolver.Resolve(outside, 10, 10);
        using var viaUri = resolver.Resolve(inside, 10, 10);
        using var viaEncoding = resolver.Resolve("my%20img.stub", 10, 10);

        // Assert: only the outside link is a warning
        Assert.Null(refused);
        Assert.NotNull(viaUri);
        Assert.NotNull(viaEncoding);
        Assert.Equal(FolderImageResolver.UnresolvedCode, Assert.Single(resolver.Diagnostics).Code);
        Assert.Equal(["inside.stub", "my img.stub"], stub.Requests.Select(r => r.FileName));
    }

    /// <summary>
    ///     Test that a <c>file:</c> URI with a control character is refused.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_FileUriWithControlCharacter_IsUnresolvedWarning()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        var stub = Stub();
        var resolver = new FolderImageResolver(folder.Path, [stub]);

        // Act
        var surface = resolver.Resolve("file:///x%00.stub", 10, 10);

        // Assert
        Assert.Null(surface);
        Assert.Empty(stub.Requests);
        Assert.Equal(FolderImageResolver.UnresolvedCode, Assert.Single(resolver.Diagnostics).Code);
    }

    /// <summary>
    ///     Test that a reference through a symbolic link is refused.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_SymbolicLink_IsUnresolvedWarning()
    {
        // Arrange: a file outside the folder and a link to it inside (links may need privileges on Windows)
        using var folder = new ImageTestFolder();
        Directory.CreateDirectory(folder.Combine("site"));
        folder.WriteText("real.stub", "x");
        try
        {
            File.CreateSymbolicLink(folder.Combine("site", "link.stub"), folder.Combine("real.stub"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Assert.Skip("Symbolic links cannot be created here.");
            return;
        }

        var stub = Stub();
        var resolver = new FolderImageResolver(folder.Combine("site"), [stub]);

        // Act
        var surface = resolver.Resolve("link.stub", 10, 10);

        // Assert
        Assert.Null(surface);
        Assert.Empty(stub.Requests);
        Assert.Equal(FolderImageResolver.UnresolvedCode, Assert.Single(resolver.Diagnostics).Code);
    }

    /// <summary>
    ///     Test that a missing file and a file of an extension nobody claims are warnings, not errors, and that the
    ///     warning for the unknown extension lists the extensions that are claimed.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_MissingAndUnknownFiles_AreWarnings()
    {
        // Arrange: one link to nothing and one to a text file
        using var folder = new ImageTestFolder();
        folder.WriteText("notes.txt", "not a picture");
        var resolver = new FolderImageResolver(folder.Path, [Stub()]);

        // Act
        var missing = resolver.Resolve("missing.stub", 10, 10);
        var unknown = resolver.Resolve("notes.txt", 10, 10);

        // Assert
        Assert.Null(missing);
        Assert.Null(unknown);
        Assert.Equal(2, resolver.Diagnostics.Count(d => d.Code == FolderImageResolver.UnresolvedCode));
        Assert.Contains("not found", resolver.Diagnostics[0].Message, StringComparison.Ordinal);
        Assert.Contains(Ext, resolver.Diagnostics[1].Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the longest claimed extension picks the source and that the match ignores case: a compound
    ///     <c>.vl.json</c> beats <c>.json</c>.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_LongestSuffixAndCaseInsensitive_PicksSource()
    {
        // Arrange: one source claims .json, another .vl.json
        using var folder = new ImageTestFolder();
        folder.WriteText("a.vl.json", "x");
        folder.WriteText("b.json", "x");
        folder.WriteText("C.VL.JSON", "x");
        var plain = new StubImageSource("plain", [".json"]);
        var chart = new StubImageSource("chart", [".vl.json"]);
        var resolver = new FolderImageResolver(folder.Path, [plain, chart]);

        // Act
        resolver.Resolve("a.vl.json", 10, 10)?.Dispose();
        resolver.Resolve("b.json", 10, 10)?.Dispose();
        resolver.Resolve("C.VL.JSON", 10, 10)?.Dispose();

        // Assert
        Assert.Equal(["a.vl.json", "C.VL.JSON"], chart.Requests.Select(r => r.FileName));
        Assert.Equal(["b.json"], plain.Requests.Select(r => r.FileName));
    }

    /// <summary>
    ///     Test that when two sources claim the same extension the later one in the sequence wins, whichever order the
    ///     sequence is given in.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_TwoSourcesClaimExtension_LaterWins()
    {
        // Arrange: a built-in-like source first and a caller's source after it, then the reverse
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "x");
        var first = new StubImageSource("first", [Ext]);
        var second = new StubImageSource("second", [Ext], null, _ => new ImageSourceResult(StubImageSource.Solid(2, 2, StubImageSource.Blue)));
        var forward = new FolderImageResolver(folder.Path, [first, second]);
        var backward = new FolderImageResolver(folder.Path, [second, first]);

        // Act
        using var fromForward = forward.Resolve("a.stub", 10, 10);
        using var fromBackward = backward.Resolve("a.stub", 10, 10);

        // Assert: the last source in each sequence drew
        Assert.Equal(StubImageSource.Blue, fromForward![0, 0]);
        Assert.Equal(StubImageSource.Green, fromBackward![0, 0]);
        Assert.Equal("second", forward.Embedded.Single().Format);
        Assert.Equal("first", backward.Embedded.Single().Format);
    }

    /// <summary>
    ///     Test that an <see cref="ImageSourceSet"/> given to the constructor is used as is, so a set extended with an
    ///     override takes precedence.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_ImageSourceSetWithAddedSource_OverridesEarlierSource()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "x");
        var builtIn = Stub();
        var mine = new StubImageSource("mine", [Ext]);
        var resolver = new FolderImageResolver(folder.Path, new ImageSourceSet([builtIn]).Add(mine));

        // Act
        resolver.Resolve("a.stub", 10, 10)?.Dispose();

        // Assert
        Assert.Empty(builtIn.Requests);
        Assert.Single(mine.Requests);
    }

    /// <summary>
    ///     Test that a file embedding itself, and two files embedding each other, stop with a warning.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_Cycles_TerminateWithWarning()
    {
        // Arrange: self.stub links itself; a.stub and b.stub link each other
        using var folder = new ImageTestFolder();
        folder.WriteText("self.stub", "self.stub");
        folder.WriteText("a.stub", "b.stub");
        folder.WriteText("b.stub", "a.stub");
        var selfResolver = new FolderImageResolver(folder.Path, [Chaining(Ext)]);
        var pairResolver = new FolderImageResolver(folder.Path, [Chaining(Ext)]);

        // Act
        selfResolver.Resolve("self.stub", 10, 10)?.Dispose();
        pairResolver.Resolve("a.stub", 10, 10)?.Dispose();

        // Assert: each cycle is cut at the first repeated file, and says so
        Assert.Contains("cycle", Assert.Single(selfResolver.Diagnostics).Message, StringComparison.Ordinal);
        Assert.Contains("cycle", Assert.Single(pairResolver.Diagnostics).Message, StringComparison.Ordinal);
        Assert.Single(selfResolver.Embedded);
        Assert.Equal(2, pairResolver.Embedded.Count);
    }

    /// <summary>
    ///     Test that a chain of files deeper than the limit stops with a warning, after exactly as many files as the
    ///     limit allows, and that the depth of every embedded image is reported.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_TooDeep_TerminatesWithWarning()
    {
        // Arrange: s0 links s1 links s2 and so on, for more files than the limit allows
        using var folder = new ImageTestFolder();
        const int maxDepth = 3;
        for (var i = 0; i < maxDepth + 3; i++)
        {
            folder.WriteText($"s{i}.stub", i == maxDepth + 2 ? string.Empty : $"s{i + 1}.stub");
        }

        var resolver = new FolderImageResolver(folder.Path, [Chaining(Ext)], new FolderImageResolverOptions { MaxDepth = maxDepth });

        // Act
        resolver.Resolve("s0.stub", 10, 10)?.Dispose();

        // Assert: the chain was cut once, with a note about the depth, after maxDepth files
        var note = Assert.Single(resolver.Diagnostics);
        Assert.Contains("levels deep", note.Message, StringComparison.Ordinal);
        Assert.Equal(maxDepth, resolver.Embedded.Count);
        Assert.Equal([2, 1, 0], resolver.Embedded.Select(e => e.Depth));
    }

    /// <summary>
    ///     Test that a document that embeds more images than the limit stops at the limit, counting a repeated link to
    ///     the same file each time.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_TooMany_StopsAtLimitWithWarnings()
    {
        // Arrange: more links to one small file than the limit
        using var folder = new ImageTestFolder();
        folder.WriteText("p.stub", "x");
        var stub = Stub();
        var resolver = new FolderImageResolver(folder.Path, [stub], new FolderImageResolverOptions { MaxImages = 3 });

        // Act
        for (var i = 0; i < 6; i++)
        {
            resolver.Resolve("p.stub", 1, 1)?.Dispose();
        }

        // Assert: the first ones were drawn, and every further link is a warning about the limit
        Assert.Equal(3, stub.Requests.Count);
        Assert.Equal(3, resolver.Embedded.Count);
        var notes = resolver.Diagnostics.Where(d => d.Code == FolderImageResolver.UnresolvedCode).ToList();
        Assert.Equal(3, notes.Count);
        Assert.All(notes, n => Assert.Contains("more than 3", n.Message, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that the notes and printed text of a source reach the resolver, with the link in front of each note,
    ///     and that the embedded image is listed with the format the source reported.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_SourceWithNotes_ForwardsDiagnosticsEchoAndEmbedded()
    {
        // Arrange: a source that reports a note, prints a line and names its format
        using var folder = new ImageTestFolder();
        folder.WriteText("part.stub", "x");
        var stub = new StubImageSource(
            "stub",
            [Ext],
            null,
            _ => new ImageSourceResult(StubImageSource.Solid(7, 5, StubImageSource.Green))
            {
                Diagnostics = [new ImageSourceDiagnostic("scad", ImageDiagnosticSeverity.Unsupported, "offset", "offset is not drawn")],
                Echo = ["from-the-model"],
                Format = "model"
            });
        var resolver = new FolderImageResolver(folder.Path, [stub]);

        // Act
        resolver.Resolve("part.stub", 40, 40)?.Dispose();

        // Assert
        var note = Assert.Single(resolver.Diagnostics);
        Assert.Equal(("scad", ImageDiagnosticSeverity.Unsupported, "offset"), (note.Source, note.Severity, note.Code));
        Assert.Equal("part.stub: offset is not drawn", note.Message);
        Assert.Equal(["from-the-model"], resolver.Echo);
        Assert.Equal(new EmbeddedImage("part.stub", "model", 7, 5), resolver.Embedded.Single());
    }

    /// <summary>
    ///     Test that a failure of a source is an <see cref="ImageSourceException"/> that names the link, has no embedder at
    ///     the top level and keeps the original exception as its inner exception.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_SourceThrows_WrapsInImageSourceException()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("bad.stub", "x");
        var failure = new InvalidDataException("corrupt");
        var stub = new StubImageSource("stub", [Ext], null, _ => throw failure);
        var resolver = new FolderImageResolver(folder.Path, [stub]);

        // Act
        var error = Assert.Throws<ImageSourceException>(() => resolver.Resolve("bad.stub", 10, 10));

        // Assert
        Assert.Equal("bad.stub", error.Href);
        Assert.Null(error.Embedder);
        Assert.Same(failure, error.InnerException);
        Assert.Contains("corrupt", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a failure deep in a chain keeps the innermost embedder and link instead of being wrapped again.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_NestedSourceThrows_KeepsInnermostEmbedderAndHref()
    {
        // Arrange: outer.stub links sub/inner.stub, whose source fails
        using var folder = new ImageTestFolder();
        folder.WriteText("outer.stub", "sub/inner.stub");
        folder.WriteText("sub/inner.stub", "inner");
        var failure = new InvalidDataException("corrupt");
        var stub = new StubImageSource(
            "stub",
            [Ext],
            null,
            request =>
            {
                var text = Encoding.UTF8.GetString(request.Content.Span);
                if (text == "inner")
                {
                    throw failure;
                }

                request.Resolver!.Resolve(text, 10, 10)?.Dispose();
                return new ImageSourceResult(StubImageSource.Solid(1, 1, StubImageSource.Green));
            });
        var resolver = new FolderImageResolver(folder.Path, [stub]);

        // Act
        var error = Assert.Throws<ImageSourceException>(() => resolver.Resolve("outer.stub", 10, 10));

        // Assert: the link is the one written in outer.stub and the embedder is outer.stub; the inner type is intact
        Assert.Equal("sub/inner.stub", error.Href);
        Assert.Equal("outer.stub", error.Embedder);
        Assert.Same(failure, error.InnerException);
    }

    /// <summary>
    ///     Test that cancellation passes through as <see cref="OperationCanceledException"/> and is not wrapped.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_SourceThrowsCancellation_IsNotWrapped()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "x");
        var stub = new StubImageSource("stub", [Ext], null, _ => throw new OperationCanceledException());
        var resolver = new FolderImageResolver(folder.Path, [stub]);

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => resolver.Resolve("a.stub", 10, 10));
    }

    /// <summary>
    ///     Test that an already canceled token stops the resolver before any source is called, and that the token reaches
    ///     the sources.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_CanceledToken_ThrowsBeforeCallingSource()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "x");
        var stub = Stub();
        using var cts = new CancellationTokenSource();
        var resolver = new FolderImageResolver(folder.Path, [stub], new FolderImageResolverOptions { CancellationToken = cts.Token });
        resolver.Resolve("a.stub", 10, 10)?.Dispose();
        cts.Cancel();

        // Act / Assert: the second call is refused and the source saw the token only once
        Assert.Throws<OperationCanceledException>(() => resolver.Resolve("a.stub", 10, 10));
        Assert.Single(stub.Requests);
        Assert.Equal(cts.Token, stub.Requests[0].CancellationToken);
    }

    /// <summary>
    ///     Test that nothing is cached: two calls for the same link invoke the source twice and return two different
    ///     surfaces, so a live source yields a fresh picture each time.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_SameLinkTwice_InvokesSourceEachTimeWithoutCaching()
    {
        // Arrange: a source that draws a different color on every call
        using var folder = new ImageTestFolder();
        folder.WriteText("live.stub", "x");
        var frame = 0;
        var stub = new StubImageSource(
            "live",
            [Ext],
            null,
            _ => new ImageSourceResult(StubImageSource.Solid(2, 2, new Rgba32((byte)(++frame * 10), 0, 0, 255))));
        var resolver = new FolderImageResolver(folder.Path, [stub]);

        // Act
        using var first = resolver.Resolve("live.stub", 10, 10);
        using var second = resolver.Resolve("live.stub", 10, 10);

        // Assert
        Assert.Equal(2, stub.Requests.Count);
        Assert.NotSame(first, second);
        Assert.Equal(new Rgba32(10, 0, 0, 255), first![0, 0]);
        Assert.Equal(new Rgba32(20, 0, 0, 255), second![0, 0]);
    }

    /// <summary>
    ///     Test that a link relative to an embedded file is resolved from the folder of that file, through the scoped
    ///     resolver in the request.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_NestedRelativeLink_ResolvesFromFolderOfEmbeddingFile()
    {
        // Arrange: sub/outer.stub links img.stub (which exists only in sub); the top folder has none
        using var folder = new ImageTestFolder();
        folder.WriteText("sub/outer.stub", "img.stub");
        folder.WriteText("sub/img.stub", string.Empty);
        var stub = Chaining(Ext);
        var resolver = new FolderImageResolver(folder.Path, [stub]);

        // Act
        resolver.Resolve("sub/outer.stub", 10, 10)?.Dispose();

        // Assert: both files were drawn and nothing is reported
        Assert.Empty(resolver.Diagnostics);
        Assert.Equal(["sub/outer.stub", "sub/img.stub"], stub.Requests.Select(r => r.FileName));
        Assert.Equal(["img.stub", "sub/outer.stub"], resolver.Embedded.Select(e => e.Href));
    }

    /// <summary>
    ///     Test that a resolver without a folder leaves every link without a scheme unresolved, and still serves
    ///     schemes.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_NoFolder_FilesAreUnresolvedButSchemesWork()
    {
        // Arrange
        var fileSource = Stub();
        var schemeSource = new StubImageSource("live", null, ["test-scheme"]);
        var resolver = new FolderImageResolver(null, [fileSource, schemeSource]);

        // Act
        var file = resolver.Resolve("a.stub", 10, 10);
        using var live = resolver.Resolve("test-scheme:frame", 10, 10);

        // Assert
        Assert.Null(file);
        Assert.NotNull(live);
        Assert.Empty(fileSource.Requests);
        Assert.Equal(FolderImageResolver.UnresolvedCode, Assert.Single(resolver.Diagnostics).Code);
    }

    /// <summary>
    ///     Test that a link whose scheme a source claims is handed to it as written, without the file system: no
    ///     content, no file name, no folder, and the raw text including the case of the scheme.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_ClaimedScheme_GivesSourceRawHrefWithoutFileAccess()
    {
        // Arrange: a folder that does not exist, so any file access would fail
        var stub = new StubImageSource("live", null, ["test-scheme"]);
        var resolver = new FolderImageResolver(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N")), [stub]);

        // Act
        using var surface = resolver.Resolve("Test-Scheme:../../any thing%20here", 12, 9);

        // Assert
        Assert.NotNull(surface);
        var request = Assert.Single(stub.Requests);
        Assert.Equal("Test-Scheme:../../any thing%20here", request.Href);
        Assert.False(request.HasContent);
        Assert.True(request.Content.IsEmpty);
        Assert.Null(request.FileName);
        Assert.Null(request.Directory);
        Assert.Equal((12, 9), (request.PixelWidth, request.PixelHeight));
        Assert.Throws<InvalidOperationException>(() => request.OpenContent());
        Assert.Empty(resolver.Diagnostics);
        Assert.Equal("live", resolver.Embedded.Single().Format);
    }

    /// <summary>
    ///     Test that a nested relative link of a scheme-claimed source resolves against the root folder of the
    ///     resolver, because the link has no folder of its own.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_ClaimedSchemeNestedRelativeLink_ResolvesFromRootFolder()
    {
        // Arrange: the scheme source asks its scoped resolver for a file that is in the root folder
        using var folder = new ImageTestFolder();
        folder.WriteText("overlay.stub", "x");
        var files = Stub();
        var live = new StubImageSource(
            "live",
            null,
            ["test-scheme"],
            request =>
            {
                request.Resolver!.Resolve("overlay.stub", 4, 4)?.Dispose();
                return new ImageSourceResult(StubImageSource.Solid(2, 2, StubImageSource.Blue));
            });
        var resolver = new FolderImageResolver(folder.Path, [files, live]);

        // Act
        resolver.Resolve("test-scheme:frame", 10, 10)?.Dispose();

        // Assert
        Assert.Equal("overlay.stub", Assert.Single(files.Requests).FileName);
        Assert.Equal(0, resolver.Embedded.Single(e => e.Format == "live").Depth);
        Assert.Equal(1, resolver.Embedded.Single(e => e.Format == "stub").Depth);
    }

    /// <summary>
    ///     Test that a scheme-claimed source that resolves its own link again is cut as a cycle, and that a nested link
    ///     beyond the depth limit is refused.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_ClaimedSchemeCycleAndDepth_AreWarnings()
    {
        // Arrange: a source that links itself and another link
        var self = new StubImageSource(
            "live",
            null,
            ["test-scheme"],
            request =>
            {
                request.Resolver!.Resolve("test-scheme:again", 4, 4)?.Dispose();
                request.Resolver.Resolve("test-scheme:other", 4, 4)?.Dispose();
                return new ImageSourceResult(StubImageSource.Solid(1, 1, StubImageSource.Green));
            });
        var resolver = new FolderImageResolver(null, [self], new FolderImageResolverOptions { MaxDepth = 1 });

        // Act
        resolver.Resolve("test-scheme:again", 10, 10)?.Dispose();

        // Assert: the nested links are refused (one cycle, one over the depth), the top-level one is drawn
        Assert.Equal(2, resolver.Diagnostics.Count(d => d.Code == FolderImageResolver.UnresolvedCode));
        Assert.Contains(resolver.Diagnostics, d => d.Message.Contains("cycle", StringComparison.Ordinal));
        Assert.Contains(resolver.Diagnostics, d => d.Message.Contains("levels deep", StringComparison.Ordinal));
        Assert.Single(resolver.Embedded);
    }

    /// <summary>
    ///     Test that links of a claimed scheme count against the image limit.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_ClaimedSchemeLinks_CountAgainstImageLimit()
    {
        // Arrange
        var live = new StubImageSource("live", null, ["test-scheme"]);
        var resolver = new FolderImageResolver(null, [live], new FolderImageResolverOptions { MaxImages = 2 });

        // Act
        for (var i = 0; i < 4; i++)
        {
            resolver.Resolve($"test-scheme:frame{i}", 10, 10)?.Dispose();
        }

        // Assert
        Assert.Equal(2, live.Requests.Count);
        Assert.Equal(2, resolver.Diagnostics.Count(d => d.Message.Contains("more than 2", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Test that http and https links are unresolved warnings by default, even when a file source exists, and that
    ///     a caller can claim them with a source of its own.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_HttpAndHttps_UnresolvedByDefaultButClaimable()
    {
        // Arrange: one resolver with only a file source and one with a source that claims http and https
        using var folder = new ImageTestFolder();
        var plain = new FolderImageResolver(folder.Path, [Stub()]);
        var web = new StubImageSource("web", null, ["http", "https"]);
        var claiming = new FolderImageResolver(folder.Path, [Stub(), web]);

        // Act
        var refused = plain.Resolve("https://example.invalid/a.stub", 10, 10);
        using var viaHttp = claiming.Resolve("http://example.invalid/a.png", 10, 10);
        using var viaHttps = claiming.Resolve("HTTPS://example.invalid/b.png", 10, 10);

        // Assert
        Assert.Null(refused);
        Assert.Equal(FolderImageResolver.UnresolvedCode, Assert.Single(plain.Diagnostics).Code);
        Assert.Equal(["http://example.invalid/a.png", "HTTPS://example.invalid/b.png"], web.Requests.Select(r => r.Href));
        Assert.Empty(claiming.Diagnostics);
    }

    /// <summary>
    ///     Test that a scheme nobody claims is an unresolved warning that says so.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_UnclaimedScheme_IsUnresolvedWarning()
    {
        // Arrange
        var resolver = new FolderImageResolver(null, [new StubImageSource("live", null, ["test-scheme"])]);

        // Act
        var surface = resolver.Resolve("other:frame", 10, 10);

        // Assert
        Assert.Null(surface);
        Assert.Contains("'other'", Assert.Single(resolver.Diagnostics).Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a source may not claim the schemes <c>file</c> and <c>data</c>, a single-letter scheme, or a
    ///     malformed one, and that the error names the source.
    /// </summary>
    /// <param name="scheme">The scheme the source claims.</param>
    [Theory]
    [InlineData("file")]
    [InlineData("FILE")]
    [InlineData("data")]
    [InlineData("c")]
    [InlineData("1abc")]
    [InlineData("a b")]
    [InlineData("camera:")]
    [InlineData("")]
    public void FolderImageResolver_Constructor_InvalidOrReservedScheme_ThrowsArgumentException(string scheme)
    {
        // Arrange
        var source = new StubImageSource("greedy", null, [scheme]);

        // Act
        var error = Assert.Throws<ArgumentException>(() => new FolderImageResolver(null, [source]));

        // Assert
        Assert.Contains("greedy", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a <c>file:</c> link stays a path inside the folder even when a source claims every scheme it can.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_FileScheme_IsHandledByResolverNotSources()
    {
        // Arrange: a file inside the folder and a scheme source that must never see the file link
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "x");
        var files = Stub();
        var live = new StubImageSource("live", null, ["test-scheme"]);
        var resolver = new FolderImageResolver(folder.Path, [files, live]);

        // Act
        using var surface = resolver.Resolve(new Uri(folder.Combine("a.stub")).AbsoluteUri, 10, 10);

        // Assert
        Assert.NotNull(surface);
        Assert.Empty(live.Requests);
        Assert.Single(files.Requests);
    }

    /// <summary>
    ///     Test that the extension and scheme lists are the sorted union of what the sources claim.
    /// </summary>
    [Fact]
    public void FolderImageResolver_ExtensionsAndSchemes_AreSortedUnionOfSources()
    {
        // Arrange
        var resolver = new FolderImageResolver(
            null,
            [new StubImageSource("a", [".b", ".a"], ["zeta"]), new StubImageSource("b", [".A", ".c.d"], ["alpha"])]);

        // Act / Assert
        Assert.Equal([".a", ".b", ".c.d"], resolver.Extensions);
        Assert.Equal(["alpha", "zeta"], resolver.Schemes);
    }

    /// <summary>
    ///     Test that the lists of a resolver (claims, notes, text and embedded images) cannot be changed by casting them
    ///     to an array, a list or <see cref="IList{T}"/>.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Collections_AreReadOnly()
    {
        // Arrange
        var resolver = new FolderImageResolver(null, [new StubImageSource("a", [".a"], ["alpha"])]);

        // Act / Assert
        ReadOnlyListAssert.IsReadOnly(resolver.Extensions, ".x");
        ReadOnlyListAssert.IsReadOnly(resolver.Schemes, "x");
        ReadOnlyListAssert.IsReadOnly(resolver.Diagnostics, new ImageSourceDiagnostic("s", ImageDiagnosticSeverity.Info, "c", "m"));
        ReadOnlyListAssert.IsReadOnly(resolver.Echo, "x");
        ReadOnlyListAssert.IsReadOnly(resolver.Embedded, new EmbeddedImage("a", "b", 1, 1));
    }

    /// <summary>
    ///     Test that invalid arguments are rejected by the constructor.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Constructor_InvalidArguments_Throw()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => new FolderImageResolver(null, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FolderImageResolver(null, [], new FolderImageResolverOptions { MaxDepth = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FolderImageResolver(null, [], new FolderImageResolverOptions { MaxImages = -1 }));
        Assert.Throws<ArgumentException>(() => new FolderImageResolver(string.Empty, []));
        Assert.Throws<ArgumentException>(() => new FolderImageResolver(null, [new StubImageSource("bad", ["png"])]));
        Assert.Throws<ArgumentNullException>(() => new FolderImageResolver(null, []).Resolve(null!, 1, 1));
    }

    /// <summary>
    ///     Test that rendering a file by path returns what the source drew with the format filled in, passes the
    ///     requested size, and does not count against the image limit.
    /// </summary>
    [Fact]
    public void FolderImageResolver_RenderPath_File_ReturnsResultWithFormat()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("top.stub", "x");
        var stub = Stub();
        var resolver = new FolderImageResolver(folder.Path, [stub], new FolderImageResolverOptions { MaxImages = 0 });

        // Act
        var result = resolver.Render("top.stub", 50, null);
        result.Surface.Dispose();

        // Assert
        Assert.Equal((50, (int?)null), (stub.Requests[0].PixelWidth, stub.Requests[0].PixelHeight));
        Assert.Empty(resolver.Embedded);
        Assert.Equal("top.stub", stub.Requests[0].FileName);
    }

    /// <summary>
    ///     Test that a result from a render by path reports the name of the source as format when the source reported none.
    /// </summary>
    [Fact]
    public void FolderImageResolver_RenderPath_SourceWithoutFormat_UsesSourceName()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("top.stub", "x");
        var resolver = new FolderImageResolver(folder.Path, [Stub()]);

        // Act
        var result = resolver.Render("top.stub");
        result.Surface.Dispose();

        // Assert
        Assert.Equal("stub", result.Format);
    }

    /// <summary>
    ///     Test that rendering by path fails loudly: a missing file, a refused path, an unknown extension, a failing
    ///     source, and a resolver without a folder.
    /// </summary>
    [Fact]
    public void FolderImageResolver_RenderPath_Failures_ThrowSpecificExceptions()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("notes.txt", "x");
        folder.WriteText("bad.stub", "x");
        var failing = new StubImageSource("failing", [Ext], null, _ => throw new InvalidDataException("corrupt"));
        var resolver = new FolderImageResolver(folder.Path, [failing]);

        // Act / Assert
        Assert.Throws<FileNotFoundException>(() => resolver.Render("missing.stub"));
        Assert.Throws<SandboxException>(() => resolver.Render("../x.stub"));
        var unsupported = Assert.Throws<NotSupportedException>(() => resolver.Render("notes.txt"));
        Assert.Contains(Ext, unsupported.Message, StringComparison.Ordinal);
        var wrapped = Assert.Throws<ImageSourceException>(() => resolver.Render("bad.stub"));
        Assert.IsType<InvalidDataException>(wrapped.InnerException);
        Assert.Null(wrapped.Embedder);
        Assert.Throws<InvalidOperationException>(() => new FolderImageResolver(null, [failing]).Render("bad.stub"));
        Assert.Throws<ArgumentNullException>(() => resolver.Render((string)null!));
    }

    /// <summary>
    ///     Test that a file rendered by path that links itself stops at once as a cycle.
    /// </summary>
    [Fact]
    public void FolderImageResolver_RenderPath_FileLinkingItself_StopsAsCycle()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("self.stub", "self.stub");
        var resolver = new FolderImageResolver(folder.Path, [Chaining(Ext)]);

        // Act
        resolver.Render("self.stub").Surface.Dispose();

        // Assert
        Assert.Contains("cycle", Assert.Single(resolver.Diagnostics).Message, StringComparison.Ordinal);
        Assert.Empty(resolver.Embedded);
    }

    /// <summary>
    ///     Test that rendering a stream gives the source the bytes, the file name, the folder and the size, and that
    ///     it fails for an unknown extension and for a stream larger than the limit.
    /// </summary>
    [Fact]
    public void FolderImageResolver_RenderStream_PassesBytesAndFolder_AndRejectsUnknownOrHuge()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        var stub = Stub();
        var resolver = new FolderImageResolver(folder.Path, [stub]);
        using var content = new MemoryStream([1, 2, 3]);

        // Act
        var result = resolver.Render(content, "virtual/name.stub", 8, 6);
        result.Surface.Dispose();

        // Assert
        var request = Assert.Single(stub.Requests);
        Assert.Equal([1, 2, 3], request.Content.ToArray());
        Assert.Equal("virtual/name.stub", request.FileName);
        Assert.Equal(folder.Path, request.Directory);
        Assert.Equal((8, 6), (request.PixelWidth, request.PixelHeight));
        Assert.Equal("stub", result.Format);
        Assert.Throws<NotSupportedException>(() => resolver.Render(new MemoryStream(), "x.unknown"));
        using var huge = new MemoryStream(new byte[(16 * 1024 * 1024) + 1]);
        Assert.Throws<InvalidDataException>(() => resolver.Render(huge, "x.stub"));
    }

    /// <summary>
    ///     Test that rendering a stream without a folder gives the source no directory and no way to resolve files.
    /// </summary>
    [Fact]
    public void FolderImageResolver_RenderStream_NoFolder_SourceHasNoDirectory()
    {
        // Arrange
        var stub = Stub();
        var resolver = new FolderImageResolver(null, [stub]);

        // Act
        resolver.Render(new MemoryStream([1]), "a.stub").Surface.Dispose();

        // Assert
        Assert.Null(stub.Requests[0].Directory);
        Assert.NotNull(stub.Requests[0].Resolver);
    }
}

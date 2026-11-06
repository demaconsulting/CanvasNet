// cspell:ignore href hrefs yqqq
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Tests.ImageSources;

/// <summary>
///     Unit tests for how <see cref="FolderImageResolver"/> handles the fragment of a link: it reaches the source parsed,
///     parameters the source does not declare and duplicates are warnings, a source can decline a link, the limits refuse
///     a hostile fragment before anything is read, and a direct render passes caller options to the source.
/// </summary>
public class FolderImageResolverFragmentTests
{
    /// <summary>The extension the stubs claim.</summary>
    private const string Ext = ".stub";

    /// <summary>
    ///     Options of a direct render for tests.
    /// </summary>
    private sealed class TestOptions : IImageSourceOptions
    {
    }

    /// <summary>
    ///     Test that the fragment of a file link reaches the source parsed, while the link, the file name and the query
    ///     handling are unchanged.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_FileLinkWithFragment_FragmentReachesSource()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "a");
        var stub = new FragmentStubImageSource("stub", ["page", "view"], [Ext]);
        var resolver = new FolderImageResolver(folder.Path, [stub]);

        // Act
        using var surface = resolver.Resolve("a.stub?v=2#page=2&view=front", 10, 10);

        // Assert: the raw link, the raw fragment, the parsed parameters; the query is not interpreted; no notes; no options
        Assert.NotNull(surface);
        var request = Assert.Single(stub.Requests);
        Assert.Equal("a.stub?v=2#page=2&view=front", request.Href);
        Assert.Equal("a.stub", request.FileName);
        Assert.Equal("page=2&view=front", request.Fragment.Raw);
        Assert.Equal(
            [new ImageFragmentParameter("page", "2"), new ImageFragmentParameter("view", "front")],
            request.Fragment.Parameters);
        Assert.Null(request.Options);
        Assert.Empty(resolver.Diagnostics);
    }

    /// <summary>
    ///     Test that a link without a fragment gives the source the empty fragment.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_LinkWithoutFragment_RequestHasEmptyFragment()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "a");
        var stub = new StubImageSource("stub", [Ext]);
        var resolver = new FolderImageResolver(folder.Path, [stub]);

        // Act
        using var surface = resolver.Resolve("a.stub?v=2", 10, 10);

        // Assert
        Assert.NotNull(surface);
        Assert.Same(ImageFragment.Empty, Assert.Single(stub.Requests).Fragment);
        Assert.Empty(resolver.Diagnostics);
    }

    /// <summary>
    ///     Test that the first hash starts the fragment, so a question mark after it is part of the fragment text and not
    ///     a query.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_QuestionMarkAfterHash_BelongsToFragment()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "a");
        var stub = new FragmentStubImageSource("stub", ["page"], [Ext]);
        var resolver = new FolderImageResolver(folder.Path, [stub]);

        // Act
        using var surface = resolver.Resolve("a.stub#page=2?x=1#y", 10, 10);

        // Assert
        Assert.NotNull(surface);
        var request = Assert.Single(stub.Requests);
        Assert.Equal("page=2?x=1#y", request.Fragment.Raw);
        Assert.Equal([new ImageFragmentParameter("page", "2?x=1#y")], request.Fragment.Parameters);
    }

    /// <summary>
    ///     Test that a parameter the source does not declare is a warning and the picture is still drawn.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_UnknownParameter_WarnsAndStillDraws()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "a");
        var stub = new FragmentStubImageSource("stub", ["page"], [Ext]);
        var resolver = new FolderImageResolver(folder.Path, [stub]);

        // Act
        using var surface = resolver.Resolve("a.stub#page=1&Foo=2&foo=3&bar", 10, 10);

        // Assert: drawn; one warning per unknown name (Foo and foo are one name), in order of first appearance
        Assert.NotNull(surface);
        Assert.Equal(2, resolver.Diagnostics.Count);
        Assert.All(
            resolver.Diagnostics,
            d =>
            {
                Assert.Equal(FolderImageResolver.FragmentUnknownParameterCode, d.Code);
                Assert.Equal(ImageDiagnosticSeverity.Warning, d.Severity);
                Assert.Equal("svg", d.Source);
                Assert.Empty(d.Origin);
            });
        Assert.Contains("'Foo'", resolver.Diagnostics[0].Message, StringComparison.Ordinal);
        Assert.Contains("'bar'", resolver.Diagnostics[1].Message, StringComparison.Ordinal);
        Assert.Contains("'a.stub#page=1&Foo=2&foo=3&bar'", resolver.Diagnostics[0].Message, StringComparison.Ordinal);
        Assert.Single(resolver.Embedded);
    }

    /// <summary>
    ///     Test that a source that does not implement <see cref="IFragmentImageSource"/> declares nothing, so every
    ///     parameter of a file link is unknown.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_SourceWithoutInterface_EveryParameterIsUnknown()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "a");
        var stub = new StubImageSource("stub", [Ext]);
        var resolver = new FolderImageResolver(folder.Path, [stub]);

        // Act
        using var surface = resolver.Resolve("a.stub#anything", 10, 10);

        // Assert
        Assert.NotNull(surface);
        var note = Assert.Single(resolver.Diagnostics);
        Assert.Equal(FolderImageResolver.FragmentUnknownParameterCode, note.Code);
        Assert.Contains("'anything'", note.Message, StringComparison.Ordinal);
        Assert.Contains("'stub' source", note.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the same parameter given twice with the same value is silent, and with different values is a warning
    ///     where the last value is the one the source sees.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_DuplicateParameter_WarnsOnlyWhenValuesDiffer()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "a");
        var stub = new FragmentStubImageSource("stub", ["page"], [Ext]);
        var resolver = new FolderImageResolver(folder.Path, [stub]);

        // Act
        using var same = resolver.Resolve("a.stub#page=2&page=2", 10, 10);
        var noteCountAfterSame = resolver.Diagnostics.Count;
        using var different = resolver.Resolve("a.stub#page=1&PAGE=2", 10, 10);

        // Assert
        Assert.Equal(0, noteCountAfterSame);
        var note = Assert.Single(resolver.Diagnostics);
        Assert.Equal(FolderImageResolver.FragmentDuplicateParameterCode, note.Code);
        Assert.Equal(ImageDiagnosticSeverity.Warning, note.Severity);
        Assert.Contains("'page'", note.Message, StringComparison.Ordinal);
        Assert.Contains("'2'", note.Message, StringComparison.Ordinal);
        Assert.Equal(2, stub.Requests[1].Fragment.GetPositiveInt32("page"));
        Assert.NotNull(different);
    }

    /// <summary>
    ///     Test that aliases of one source are different names and are never reported as duplicates of each other.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_AliasParameters_AreNotDuplicates()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "a");
        var stub = new FragmentStubImageSource("stub", ["page", "slide"], [Ext]);
        var resolver = new FolderImageResolver(folder.Path, [stub]);

        // Act
        using var surface = resolver.Resolve("a.stub#page=1&slide=2", 10, 10);

        // Assert
        Assert.NotNull(surface);
        Assert.Empty(resolver.Diagnostics);
    }

    /// <summary>
    ///     Test that an unknown parameter is reported once, as unknown, even when it also repeats with different values.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_UnknownRepeatedParameter_IsOnlyReportedAsUnknown()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "a");
        var resolver = new FolderImageResolver(folder.Path, [new StubImageSource("stub", [Ext])]);

        // Act
        using var surface = resolver.Resolve("a.stub#x=1&x=2", 10, 10);

        // Assert
        Assert.Equal(FolderImageResolver.FragmentUnknownParameterCode, Assert.Single(resolver.Diagnostics).Code);
    }

    /// <summary>
    ///     Test that hostile parameter names and values cannot put control characters or unbounded text in a message.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_HostileParameterName_IsSanitizedInMessage()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "a");
        var resolver = new FolderImageResolver(folder.Path, [new StubImageSource("stub", [Ext])]);

        // Act
        using var surface = resolver.Resolve("a.stub#x%0Ay" + new string('q', 300) + "=1", 10, 10);

        // Assert
        var note = Assert.Single(resolver.Diagnostics);
        Assert.DoesNotContain('\n', note.Message);
        Assert.Contains("x?yqqq", note.Message, StringComparison.Ordinal);
        Assert.True(note.Message.Length < 400);
    }

    /// <summary>
    ///     Test that a source that declines a link (a page that does not exist) is an unsupported note, nothing is drawn,
    ///     the budget the source charged is given back, and the render carries on with the next link.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_SourceRefuses_UnsupportedNoteNothingDrawnBudgetRefunded()
    {
        // Arrange: a source that charges, then declines when asked for page 9
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "a");
        var stub = new FragmentStubImageSource(
            "stub",
            ["page"],
            [Ext],
            null,
            request =>
            {
                request.EnsureWithinBudget(10, 10);
                if (request.Fragment.GetPositiveInt32("page") == 9)
                {
                    throw new ImageSourceRefusedException(FolderImageResolver.PageOutOfRangeCode, "page 9 does not exist: the document has 2 pages");
                }

                return new ImageSourceResult(StubImageSource.Solid(10, 10, StubImageSource.Green));
            });
        var resolver = new FolderImageResolver(folder.Path, [stub], new FolderImageResolverOptions { MaxDecodedPixels = 150 });

        // Act
        var refused = resolver.Resolve("a.stub#page=9", 10, 10);
        var remainingAfterRefusal = resolver.RemainingPixels;
        using var next = resolver.Resolve("a.stub#page=1", 10, 10);

        // Assert
        Assert.Null(refused);
        Assert.Equal(150, remainingAfterRefusal);
        Assert.NotNull(next);
        var note = Assert.Single(resolver.Diagnostics);
        Assert.Equal(FolderImageResolver.PageOutOfRangeCode, note.Code);
        Assert.Equal(ImageDiagnosticSeverity.Unsupported, note.Severity);
        Assert.Equal("svg", note.Source);
        Assert.Contains("'a.stub#page=9'", note.Message, StringComparison.Ordinal);
        Assert.Contains("page 9 does not exist", note.Message, StringComparison.Ordinal);
        Assert.Equal("a.stub#page=1", Assert.Single(resolver.Embedded).Href);
    }

    /// <summary>
    ///     Test that an invalid value read by the source through the typed accessor is an unsupported note with the
    ///     invalid-fragment code.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_SourceReadsInvalidPage_IsInvalidFragmentNote()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "a");
        var stub = new FragmentStubImageSource(
            "stub",
            ["page"],
            [Ext],
            null,
            request =>
            {
                _ = request.Fragment.GetPositiveInt32("page");
                return new ImageSourceResult(StubImageSource.Solid(4, 4, StubImageSource.Green));
            });
        var resolver = new FolderImageResolver(folder.Path, [stub]);

        // Act
        var surface = resolver.Resolve("a.stub#page=zero", 10, 10);

        // Assert
        Assert.Null(surface);
        var note = Assert.Single(resolver.Diagnostics);
        Assert.Equal(FolderImageResolver.FragmentInvalidCode, note.Code);
        Assert.Equal(ImageDiagnosticSeverity.Unsupported, note.Severity);
    }

    /// <summary>
    ///     Test that the refusal of a nested file is a note whose origin names the embedding file, and that the refusal is
    ///     not wrapped as a failure of the embedding file.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_NestedRefusal_HasOriginOfEmbeddingFile()
    {
        // Arrange: outer.stub links inner.leaf#page=9, which declines
        using var folder = new ImageTestFolder();
        folder.WriteText("outer.stub", "inner.leaf#page=9");
        folder.WriteText("inner.leaf", "x");
        var leaf = new FragmentStubImageSource(
            "leaf",
            ["page"],
            [".leaf"],
            null,
            _ => throw new ImageSourceRefusedException(FolderImageResolver.PageOutOfRangeCode, "page 9 does not exist"));
        var outer = new StubImageSource(
            "outer",
            [Ext],
            null,
            request =>
            {
                Assert.Null(request.Resolver!.Resolve(System.Text.Encoding.UTF8.GetString(request.Content.Span), 10, 10));
                return new ImageSourceResult(StubImageSource.Solid(4, 4, StubImageSource.Green));
            });
        var resolver = new FolderImageResolver(folder.Path, [leaf, outer]);

        // Act
        using var surface = resolver.Resolve("outer.stub", 10, 10);

        // Assert
        Assert.NotNull(surface);
        var note = Assert.Single(resolver.Diagnostics);
        Assert.Equal(FolderImageResolver.PageOutOfRangeCode, note.Code);
        Assert.Equal(["outer.stub"], note.Origin);
    }

    /// <summary>
    ///     Test that a fragment over the limits is refused as an unsupported note before the source is called or the file
    ///     is counted.
    /// </summary>
    /// <param name="fragment">The over-limit fragment.</param>
    [Theory]
    [MemberData(nameof(OverLimitFragments))]
    public void FolderImageResolver_Resolve_FragmentOverLimits_IsInvalidAndSourceNotCalledAndNotCounted(string fragment)
    {
        // Arrange: only one image may be loaded, so counting the refused link would starve the second one
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "a");
        var stub = new FragmentStubImageSource("stub", ["page"], [Ext]);
        var resolver = new FolderImageResolver(folder.Path, [stub], new FolderImageResolverOptions { MaxImages = 1 });

        // Act
        var refused = resolver.Resolve("a.stub#" + fragment, 10, 10);
        using var next = resolver.Resolve("a.stub#page=1", 10, 10);

        // Assert
        Assert.Null(refused);
        Assert.NotNull(next);
        var note = Assert.Single(resolver.Diagnostics);
        Assert.Equal(FolderImageResolver.FragmentInvalidCode, note.Code);
        Assert.Equal(ImageDiagnosticSeverity.Unsupported, note.Severity);
        Assert.True(note.Message.Length < 400, "the hostile link must be cut in the message");
        Assert.Single(stub.Requests);
    }

    /// <summary>
    ///     Gets the fragments that are over the limits.
    /// </summary>
    /// <returns>The test data.</returns>
    public static TheoryData<string> OverLimitFragments()
    {
        return new TheoryData<string>
        {
            "page=" + new string('1', ImageFragment.MaxLength),
            string.Join('&', Enumerable.Range(0, ImageFragment.MaxParameters + 1).Select(i => "p" + i))
        };
    }

    /// <summary>
    ///     Test that a scheme-claimed source receives the link untouched, query included, plus the parsed fragment, and
    ///     that without the interface no warnings are made (the source owns its links).
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_SchemeLink_ReceivesRawHrefAndFragmentWithoutWarnings()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        var camera = new StubImageSource("camera", null, ["camera"]);
        var resolver = new FolderImageResolver(folder.Path, [camera]);

        // Act
        using var surface = resolver.Resolve("camera:front?fps=30#page=2&who=me", 10, 10);

        // Assert
        Assert.NotNull(surface);
        var request = Assert.Single(camera.Requests);
        Assert.Equal("camera:front?fps=30#page=2&who=me", request.Href);
        Assert.Equal("page=2&who=me", request.Fragment.Raw);
        Assert.Equal(2, request.Fragment.Parameters.Count);
        Assert.False(request.HasContent);
        Assert.Empty(resolver.Diagnostics);
    }

    /// <summary>
    ///     Test that a scheme-claimed source that implements the interface gets the unknown-parameter warnings for what it
    ///     does not declare.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_SchemeLinkWithInterface_WarnsAboutUnknownParameters()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        var camera = new FragmentStubImageSource("camera", ["page"], null, ["camera"]);
        var resolver = new FolderImageResolver(folder.Path, [camera]);

        // Act
        using var surface = resolver.Resolve("camera:front#page=2&who=me", 10, 10);

        // Assert
        Assert.NotNull(surface);
        var note = Assert.Single(resolver.Diagnostics);
        Assert.Equal(FolderImageResolver.FragmentUnknownParameterCode, note.Code);
        Assert.Contains("'who'", note.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a scheme-claimed link whose fragment is over the limits is refused before its source is called.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_SchemeLinkFragmentOverLimit_IsInvalidAndSourceNotCalled()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        var camera = new StubImageSource("camera", null, ["camera"]);
        var resolver = new FolderImageResolver(folder.Path, [camera]);

        // Act
        var surface = resolver.Resolve("camera:front#" + new string('x', ImageFragment.MaxLength + 1), 10, 10);

        // Assert
        Assert.Null(surface);
        Assert.Empty(camera.Requests);
        Assert.Equal(FolderImageResolver.FragmentInvalidCode, Assert.Single(resolver.Diagnostics).Code);
    }

    /// <summary>
    ///     Test that the notes a source returns keep their detail, location and page when the resolver forwards them.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_SourceNoteWithDetail_KeepsDetailLocationAndPage()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "a");
        var location = new ImageDiagnosticLocation("a.stub", 3, 4, "/x");
        var stub = new StubImageSource(
            "stub",
            [Ext],
            null,
            _ => new ImageSourceResult(StubImageSource.Solid(4, 4, StubImageSource.Green))
            {
                Diagnostics = [new ImageSourceDiagnostic("stub", ImageDiagnosticSeverity.Warning, "stub-warning", "careful") { Detail = "stub-detail", Location = location, Page = 2 }]
            });
        var resolver = new FolderImageResolver(folder.Path, [stub]);

        // Act
        using var surface = resolver.Resolve("a.stub", 10, 10);

        // Assert
        var note = Assert.Single(resolver.Diagnostics);
        Assert.Equal("a.stub: careful", note.Message);
        Assert.Equal("stub-detail", note.Detail);
        Assert.Equal(location, note.Location);
        Assert.Equal(2, note.Page);
        Assert.Equal(["a.stub"], note.Origin);
    }

    /// <summary>
    ///     Test that a direct render by path passes the caller's options to the source, with an empty fragment, while the
    ///     links of the file get no options.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Render_PathWithOptions_PassesOptionsToSourceOnly()
    {
        // Arrange: the outer file links to an inner one
        using var folder = new ImageTestFolder();
        folder.WriteText("outer.stub", "inner.leaf#page=2");
        folder.WriteText("inner.leaf", "x");
        var leaf = new FragmentStubImageSource("leaf", ["page"], [".leaf"]);
        var outer = new StubImageSource(
            "outer",
            [Ext],
            null,
            request =>
            {
                request.Resolver!.Resolve(System.Text.Encoding.UTF8.GetString(request.Content.Span), 10, 10)?.Dispose();
                return new ImageSourceResult(StubImageSource.Solid(4, 4, StubImageSource.Green));
            });
        var resolver = new FolderImageResolver(folder.Path, [leaf, outer]);
        var options = new TestOptions();

        // Act
        using var result = resolver.Render("outer.stub", null, null, options).Surface;

        // Assert
        Assert.Same(options, Assert.Single(outer.Requests).Options);
        Assert.Same(ImageFragment.Empty, outer.Requests[0].Fragment);
        Assert.Null(Assert.Single(leaf.Requests).Options);
        Assert.Equal(2, leaf.Requests[0].Fragment.GetPositiveInt32("page"));
    }

    /// <summary>
    ///     Test that a direct render of a stream passes the options, and that the overloads without options pass null.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Render_StreamWithAndWithoutOptions_PassesOptionsOrNull()
    {
        // Arrange
        var stub = new StubImageSource("stub", [Ext]);
        var resolver1 = new FolderImageResolver(null, [stub]);
        var resolver2 = new FolderImageResolver(null, [stub]);
        var options = new TestOptions();

        // Act
        using var with = resolver1.Render(new MemoryStream([1]), "x.stub", 8, 8, options).Surface;
        using var without = resolver2.Render(new MemoryStream([1]), "x.stub", 8, 8).Surface;

        // Assert
        Assert.Same(options, stub.Requests[0].Options);
        Assert.Null(stub.Requests[1].Options);
    }

    /// <summary>
    ///     Test that a direct render lets the refusal of a source propagate, as the exception the source threw, for a
    ///     path and for a stream.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Render_SourceRefuses_ExceptionPropagatesUnwrapped()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "a");
        var stub = new StubImageSource(
            "stub",
            [Ext],
            null,
            _ => throw new ImageSourceRefusedException(FolderImageResolver.PageOutOfRangeCode, "page 3 does not exist"));
        var byPath = new FolderImageResolver(folder.Path, [stub]);
        var byStream = new FolderImageResolver(folder.Path, [stub]);

        // Act
        var fromPath = Assert.Throws<ImageSourceRefusedException>(() => byPath.Render("a.stub"));
        var fromStream = Assert.Throws<ImageSourceRefusedException>(() => byStream.Render(new MemoryStream([1]), "a.stub"));

        // Assert
        Assert.Equal(FolderImageResolver.PageOutOfRangeCode, fromPath.Code);
        Assert.Equal("page 3 does not exist", fromStream.Message);
        Assert.Empty(byPath.Diagnostics);
    }

    /// <summary>
    ///     Test that a refusal given back by a direct render also refunds the budget the source charged.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Render_SourceRefusesAfterCharging_BudgetIsRefunded()
    {
        // Arrange
        var stub = new StubImageSource(
            "stub",
            [Ext],
            null,
            request =>
            {
                request.EnsureWithinBudget(5, 5);
                throw new ImageSourceRefusedException(FolderImageResolver.PageOutOfRangeCode, "no");
            });
        var resolver = new FolderImageResolver(null, [stub], new FolderImageResolverOptions { MaxDecodedPixels = 100 });

        // Act
        Assert.Throws<ImageSourceRefusedException>(() => resolver.Render(new MemoryStream([1]), "x.stub"));

        // Assert
        Assert.Equal(100, resolver.RemainingPixels);
    }

    /// <summary>
    ///     Test that <see cref="FolderImageResolver.Render(string, int?, int?)"/> without options is the same as passing
    ///     null options, and that a fragment in a direct render path is not interpreted.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Render_PathWithHash_IsNotInterpretedAsFragment()
    {
        // Arrange: a file whose name has a hash; a direct render takes the path literally
        using var folder = new ImageTestFolder();
        folder.WriteText("a#page=2.stub", "a");
        var stub = new FragmentStubImageSource("stub", ["page"], [Ext]);
        var resolver = new FolderImageResolver(folder.Path, [stub]);

        // Act
        using var result = resolver.Render("a#page=2.stub").Surface;

        // Assert
        var request = Assert.Single(stub.Requests);
        Assert.Same(ImageFragment.Empty, request.Fragment);
        Assert.Null(request.Options);
        Assert.Equal("a#page=2.stub", request.FileName);
    }
}

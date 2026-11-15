using DemaConsulting.CanvasNet.Files;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Positions;

namespace DemaConsulting.CanvasNet.Tests.ImageSources;

/// <summary>
///     Unit tests for the enriched resolution of <see cref="FolderImageResolver"/>
///     (<see cref="IImagePositionResolver.ResolveWithPositions"/>): the selection reaches the source, the positions of the
///     same render come back with the surface, and every limit of a plain resolution still applies.
/// </summary>
public class FolderImageResolverPositionTests
{
    /// <summary>The extension the stubs claim.</summary>
    private const string Ext = ".stub";

    /// <summary>
    ///     Creates a stub that draws a 10 by 10 picture and, when positions are asked for, reports the node <c>a</c> at the
    ///     center of its picture.
    /// </summary>
    /// <returns>The stub.</returns>
    private static StubImageSource ReportingStub()
    {
        return new StubImageSource("stub", [Ext], null, request => new ImageSourceResult(StubImageSource.Solid(10, 10, StubImageSource.Green))
        {
            Positions = request.Positions is null ? null : [new ImagePosition("node", "a") { Pixel = new PositionPoint(5, 5) }],
            PositionsTruncated = request.Positions is not null
        });
    }

    /// <summary>
    ///     Test that the selection reaches the source and that the positions and the truncation flag it reported come back
    ///     with the surface.
    /// </summary>
    [Fact]
    public void FolderImageResolver_ResolveWithPositions_ReturnsThePositionsOfTheSameRender()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("a" + Ext, "x");
        var stub = ReportingStub();
        var resolver = new FolderImageResolver(folder.Path, [stub]);
        var selection = PositionSelection.Parse(["node:a"]);

        // Act
        var resolution = resolver.ResolveWithPositions("a" + Ext, 10, 10, selection);
        using var surface = resolution?.Surface;

        // Assert
        Assert.NotNull(resolution);
        Assert.Same(selection, Assert.Single(stub.Requests).Positions);
        var position = Assert.Single(resolution.Positions!);
        Assert.Equal(new PositionPoint(5, 5), position.Pixel);
        Assert.True(resolution.PositionsTruncated);
        Assert.False(resolution.IsNaturalSize);
    }

    /// <summary>
    ///     Test that a plain resolution sends no selection, and that a source that ignores the selection gives no positions.
    /// </summary>
    [Fact]
    public void FolderImageResolver_ResolveWithPositions_SourceWithoutPositions_GivesNull()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("a" + Ext, "x");
        var stub = new StubImageSource("stub", [Ext]);
        var resolver = new FolderImageResolver(folder.Path, [stub]);

        // Act
        using var plain = resolver.Resolve("a" + Ext, 4, 4);
        var resolution = resolver.ResolveWithPositions("a" + Ext, 4, 4, PositionSelection.All);
        using var surface = resolution?.Surface;

        // Assert
        Assert.Null(stub.Requests[0].Positions);
        Assert.NotNull(resolution);
        Assert.Null(resolution.Positions);
        Assert.False(resolution.PositionsTruncated);
    }

    /// <summary>
    ///     Test that a link the file-access policy denies gives nothing and keeps its <c>image-access-denied</c> note.
    /// </summary>
    [Fact]
    public void FolderImageResolver_ResolveWithPositions_DeniedFile_GivesNullWithAccessDenied()
    {
        // Arrange: the file lies outside the only allowed root
        using var folder = new ImageTestFolder();
        folder.WriteText("site/page.txt", "x");
        folder.WriteText("outside" + Ext, "x");
        var resolver = new FolderImageResolver(
            folder.Combine("site"),
            [ReportingStub()],
            new FolderImageResolverOptions { FileAccess = FileAccessPolicy.UnderRoots(folder.Combine("site")) });

        // Act
        var resolution = resolver.ResolveWithPositions("../outside" + Ext, 10, 10, PositionSelection.All);

        // Assert
        Assert.Null(resolution);
        Assert.Contains(resolver.Diagnostics, d => d.Code == FolderImageResolver.AccessDeniedCode);
    }

    /// <summary>
    ///     Test that the image-count limit applies to an enriched resolution as to a plain one.
    /// </summary>
    [Fact]
    public void FolderImageResolver_ResolveWithPositions_OverTheImageCount_GivesNullWithUnresolved()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("a" + Ext, "x");
        var resolver = new FolderImageResolver(folder.Path, [ReportingStub()], new FolderImageResolverOptions { MaxImages = 0 });

        // Act
        var resolution = resolver.ResolveWithPositions("a" + Ext, 10, 10, PositionSelection.All);

        // Assert
        Assert.Null(resolution);
        Assert.Contains(resolver.Diagnostics, d => d.Code == FolderImageResolver.UnresolvedCode);
    }

    /// <summary>
    ///     Test that the resolver an embedded file receives is enriched too, that its selection reaches the nested source,
    ///     and that the selection is never passed on to the links of the nested file.
    /// </summary>
    [Fact]
    public void FolderImageResolver_ScopedResolver_ResolvesWithPositions()
    {
        // Arrange: an outer stub that asks its own resolver for the positions of an inner picture
        using var folder = new ImageTestFolder();
        folder.WriteText("outer.outer", "x");
        folder.WriteText("inner" + Ext, "x");
        var inner = ReportingStub();
        ImageResolution? seen = null;
        var outer = new StubImageSource("outer", [".outer"], null, request =>
        {
            var scoped = Assert.IsAssignableFrom<IImagePositionResolver>(request.Resolver);
            seen = scoped.ResolveWithPositions("inner" + Ext, 10, 10, PositionSelection.All);
            seen?.Surface.Dispose();
            return new ImageSourceResult(StubImageSource.Solid(4, 4, StubImageSource.Blue));
        });
        var resolver = new FolderImageResolver(folder.Path, [outer, inner]);

        // Act: the outer file is resolved plainly
        using var surface = resolver.Resolve("outer.outer", 4, 4);

        // Assert
        Assert.NotNull(surface);
        Assert.Null(Assert.Single(outer.Requests).Positions);
        Assert.NotNull(seen);
        Assert.Single(seen.Positions!);
    }

    /// <summary>
    ///     Test that a null selection is refused.
    /// </summary>
    [Fact]
    public void FolderImageResolver_ResolveWithPositions_NullSelection_Throws()
    {
        // Arrange
        var resolver = new FolderImageResolver(null, [ReportingStub()]);

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => resolver.ResolveWithPositions("a.png", 1, 1, null!));
    }
}

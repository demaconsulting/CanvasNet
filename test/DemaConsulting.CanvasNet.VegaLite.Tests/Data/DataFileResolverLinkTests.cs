using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Data;

/// <summary>
///     Unit tests for the link wording of <see cref="DemaConsulting.CanvasNet.VegaLite.Data.DataFileResolver"/> over
///     real symbolic links and junctions: the sandbox mechanics are proven in the core test project with an injected
///     link probe, so these tests only show end to end that a real link surfaces as a located Vega-Lite error. They skip
///     themselves only where the host cannot create links.
/// </summary>
public class DataFileResolverLinkTests
{
    /// <summary>The location the tests pass for the data path.</summary>
    private static readonly VegaLiteLocation Where = new("spec.vl.json", "/data/url", 3, 9);

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Validates with a real dangling symbolic link that it is rejected as a link while a truly missing file is not
    ///     found. The test skips only when the host cannot create file links.
    /// </summary>
    [Fact]
    public void DataFileResolver_Load_RealDanglingLinkIsRejectedOrCapabilitySkipped()
    {
        // Arrange: a link whose target does not exist
        using var directory = new DataTempDirectory();
        Directory.CreateDirectory(directory.Combine("root"));
        if (!DataLinkSupport.TryCreateFileLink(directory.Combine("root/dangling.csv"), directory.Combine("outside/nowhere.csv"), out var reason))
        {
            Assert.Skip("File links cannot be created in this environment: " + reason);
        }

        var resolver = new DataFileResolver(new VegaLiteOpenOptions { BaseDirectory = directory.Combine("root") }, VegaLiteLimits.Default, Ct);

        // Act
        var dangling = Assert.Throws<VegaLiteRenderException>(() => resolver.Load("dangling.csv", Where));
        var missing = Assert.Throws<VegaLiteRenderException>(() => resolver.Load("gone.csv", Where));

        // Assert
        Assert.Contains("passes through a link", dangling.Message, StringComparison.Ordinal);
        Assert.Contains("was not found", missing.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates with real links that a directory link, a directory link inside the root, a file link and (on
    ///     Windows, where a symbolic link needs a privilege) a junction are rejected with a located message naming the
    ///     link, that a base directory reached through a link is trusted, and that the real file beside them is read. The
    ///     test skips only when the host cannot create the links.
    /// </summary>
    [Fact]
    public void DataFileResolver_Load_RealLinksAreRejectedOrCapabilitySkipped()
    {
        // Arrange: a real file inside the root, a target outside the root, and the links to them
        using var directory = new DataTempDirectory();
        directory.Write("outside/target.csv", "outside");
        directory.Write("root/real/x.csv", "real");
        var outsideFile = directory.Write("outside/file.csv", "outside file");
        if (!DataLinkSupport.TryCreateDirectoryLink(directory.Combine("root/escape"), directory.Combine("outside"), out var reason))
        {
            Assert.Skip("Directory links cannot be created in this environment: " + reason);
        }

        Assert.True(DataLinkSupport.TryCreateDirectoryLink(directory.Combine("root/alias"), directory.Combine("root/real"), out reason), reason);
        var canLinkFiles = DataLinkSupport.TryCreateFileLink(directory.Combine("root/file-link.csv"), outsideFile, out _);
        var resolver = new DataFileResolver(new VegaLiteOpenOptions { BaseDirectory = directory.Combine("root") }, VegaLiteLimits.Default, Ct);

        // Act / Assert: a link that leaves the root and one that stays inside it are both rejected, located and without the host path
        var escaping = Assert.Throws<VegaLiteRenderException>(() => resolver.Load("escape/target.csv", Where));
        Assert.Contains("passes through a link ('escape')", escaping.Message, StringComparison.Ordinal);
        Assert.Equal(Where, escaping.Location);
        Assert.DoesNotContain(directory.Path, escaping.Message, StringComparison.Ordinal);
        Assert.Throws<VegaLiteRenderException>(() => resolver.Load("alias/x.csv", Where));

        // Assert: a file link is rejected whatever its target (only where the host can create file links)
        if (canLinkFiles)
        {
            Assert.Throws<VegaLiteRenderException>(() => resolver.Load("file-link.csv", Where));
        }

        // Assert: the real directory itself works, and so does a base directory that is itself reached through a link
        Assert.Equal("real", resolver.Load("real/x.csv", Where).Text);
        var viaLink = new DataFileResolver(new VegaLiteOpenOptions { BaseDirectory = directory.Combine("root/alias") }, VegaLiteLimits.Default, Ct);
        Assert.Equal("real", viaLink.Load("x.csv", Where).Text);
    }
}

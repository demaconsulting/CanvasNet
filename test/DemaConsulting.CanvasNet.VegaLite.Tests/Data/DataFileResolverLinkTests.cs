using DemaConsulting.CanvasNet.Files;
using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Data;

/// <summary>
///     Unit tests for links in <see cref="DemaConsulting.CanvasNet.VegaLite.Data.DataFileResolver"/> over real symbolic
///     links and junctions: the engine mechanics are proven in the core test project, so these tests only show end to end
///     that the default policy follows a real link, that the no-symlinks policy denies it with a located Vega-Lite error,
///     and that a dangling link is a missing file. Directory links are junctions on Windows (no privilege needed); the
///     tests skip themselves only where the host cannot create links.
/// </summary>
public class DataFileResolverLinkTests
{
    /// <summary>The location the tests pass for the data path.</summary>
    private static readonly VegaLiteLocation Where = new("spec.vl.json", "/data/url", 3, 9);

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Validates with a real dangling symbolic link that it is not found, like a truly missing file, under the default
    ///     policy and under no-symlinks. The test skips only when the host cannot create file links (Windows without the
    ///     privilege for symbolic links).
    /// </summary>
    [Fact]
    public void DataFileResolver_Load_RealDanglingLinkIsNotFoundOrCapabilitySkipped()
    {
        // Arrange: a link whose target does not exist
        using var directory = new DataTempDirectory();
        Directory.CreateDirectory(directory.Combine("root"));
        if (!DataLinkSupport.TryCreateFileLink(directory.Combine("root/dangling.csv"), directory.Combine("outside/nowhere.csv"), out var reason))
        {
            Assert.Skip("File symbolic links cannot be created in this environment (on Windows they need a privilege): " + reason);
        }

        foreach (var policy in new[] { FileAccessPolicy.AllowAll, FileAccessPolicy.NoSymlinks })
        {
            var resolver = new DataFileResolver(new VegaLiteOpenOptions { BaseDirectory = directory.Combine("root"), FileAccess = policy }, VegaLiteLimits.Default, Ct);

            // Act
            var dangling = Assert.Throws<VegaLiteRenderException>(() => resolver.Load("dangling.csv", Where));
            var missing = Assert.Throws<VegaLiteRenderException>(() => resolver.Load("gone.csv", Where));

            // Assert
            Assert.Contains("'dangling.csv' was not found", dangling.Message, StringComparison.Ordinal);
            Assert.Contains("was not found", missing.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     Validates with real links that, under the no-symlinks policy, a directory link, a directory link inside the
    ///     root and a file link are denied with a located message naming the policy and no host directory, that a base
    ///     directory reached through a link is trusted, and that the real file beside them is read. The test skips only
    ///     when the host cannot create the links (the file link only where file links can be created).
    /// </summary>
    [Fact]
    public void DataFileResolver_Load_RealLinksAreRejectedOrCapabilitySkipped()
    {
        // Arrange: a real file inside the root, a target outside the root, and the links to them
        using var directory = CreateLinks(out var canLinkFiles);
        var options = new VegaLiteOpenOptions { BaseDirectory = directory.Combine("root"), FileAccess = FileAccessPolicy.NoSymlinks };
        var resolver = new DataFileResolver(options, VegaLiteLimits.Default, Ct);

        // Act / Assert: a link that leaves the root and one that stays inside it are both denied, located and without the host path
        var escaping = Assert.Throws<VegaLiteRenderException>(() => resolver.Load("escape/target.csv", Where));
        Assert.Contains("The data file 'escape/target.csv' was denied by the file-access policy 'no-symlinks'", escaping.Message, StringComparison.Ordinal);
        Assert.Equal(Where, escaping.Location);
        Assert.DoesNotContain(directory.Path, escaping.Message, StringComparison.Ordinal);
        Assert.Throws<VegaLiteRenderException>(() => resolver.Load("alias/x.csv", Where));

        // Assert: a file link is denied whatever its target (only where the host can create file links)
        if (canLinkFiles)
        {
            Assert.Throws<VegaLiteRenderException>(() => resolver.Load("file-link.csv", Where));
        }

        // Assert: the real directory itself works, and so does a base directory that is itself reached through a link
        Assert.Equal("real", resolver.Load("real/x.csv", Where).Text);
        var linkedBase = new DataFileResolver(
            new VegaLiteOpenOptions { BaseDirectory = directory.Combine("root/alias"), FileAccess = FileAccessPolicy.NoSymlinks },
            VegaLiteLimits.Default,
            Ct);
        Assert.Equal("real", linkedBase.Load("x.csv", Where).Text);
    }

    /// <summary>
    ///     Validates with real links that the default policy follows a directory link that leaves the root, a directory
    ///     link inside it and a file link. The test skips only when the host cannot create the links (the file link only
    ///     where file links can be created).
    /// </summary>
    [Fact]
    public void DataFileResolver_Load_RealLinksAreFollowedByDefaultOrCapabilitySkipped()
    {
        // Arrange
        using var directory = CreateLinks(out var canLinkFiles);
        var resolver = new DataFileResolver(new VegaLiteOpenOptions { BaseDirectory = directory.Combine("root") }, VegaLiteLimits.Default, Ct);

        // Act / Assert
        Assert.Equal("outside", resolver.Load("escape/target.csv", Where).Text);
        Assert.Equal("real", resolver.Load("alias/x.csv", Where).Text);
        if (canLinkFiles)
        {
            Assert.Equal("outside file", resolver.Load("file-link.csv", Where).Text);
        }
    }

    /// <summary>
    ///     Creates <c>root/real/x.csv</c>, <c>outside/target.csv</c>, <c>outside/file.csv</c>, the directory links
    ///     <c>root/escape</c> (to <c>outside</c>) and <c>root/alias</c> (to <c>root/real</c>) and, where the host can, the
    ///     file link <c>root/file-link.csv</c> (to <c>outside/file.csv</c>); skips the test when directory links cannot
    ///     be created.
    /// </summary>
    /// <param name="canLinkFiles">Receives whether the file link was created.</param>
    /// <returns>The temporary directory.</returns>
    private static DataTempDirectory CreateLinks(out bool canLinkFiles)
    {
        var directory = new DataTempDirectory();
        directory.Write("outside/target.csv", "outside");
        directory.Write("root/real/x.csv", "real");
        var outsideFile = directory.Write("outside/file.csv", "outside file");
        if (!DataLinkSupport.TryCreateDirectoryLink(directory.Combine("root/escape"), directory.Combine("outside"), out var reason))
        {
            directory.Dispose();
            Assert.Skip("Directory links cannot be created in this environment: " + reason);
        }

        Assert.True(DataLinkSupport.TryCreateDirectoryLink(directory.Combine("root/alias"), directory.Combine("root/real"), out reason), reason);
        canLinkFiles = DataLinkSupport.TryCreateFileLink(directory.Combine("root/file-link.csv"), outsideFile, out _);
        return directory;
    }
}

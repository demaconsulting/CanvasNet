using System.Text;
using DemaConsulting.CanvasNet.Files;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Tests.Files;

namespace DemaConsulting.CanvasNet.Tests.ImageSources;

/// <summary>
///     Unit tests for the file-access policy of <see cref="FolderImageResolver"/>: the access-denied note, the policy a
///     request carries to its source (judged from the origin of the chain), cycles found through links by the real path,
///     and the option itself. Directory links are junctions on Windows, so no test needs a privilege.
/// </summary>
public class FolderImageResolverFileAccessTests
{
    /// <summary>The name of the extension the stubs claim.</summary>
    private const string Ext = ".stub";

    /// <summary>
    ///     Test that a denied link is a warning whose message names the link as written, the policy, its reason and its
    ///     hint but no host directory, whose detail holds the requested and real paths, and that the file never reaches
    ///     a source.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_DeniedLink_ReportsPolicyReasonHintAndDetailWithoutReading()
    {
        // Arrange: site/lib is a directory link to a folder outside the site; the policy allows only the site
        using var folder = new ImageTestFolder();
        var site = Directory.CreateDirectory(folder.Combine("site")).FullName;
        folder.WriteText("outside/a.stub", "x");
        var lib = Path.Combine(site, "lib");
        FileTestSupport.CreateDirectoryLinkOrSkip(lib, folder.Combine("outside"));
        var stub = new StubImageSource("stub", [Ext]);
        var resolver = new FolderImageResolver(site, [stub], new FolderImageResolverOptions { FileAccess = FileAccessPolicy.UnderRoots(site) });

        try
        {
            // Act
            var written = resolver.Resolve("../outside/a.stub", 10, 10);
            var linked = resolver.Resolve("lib/a.stub", 10, 10);

            // Assert: the path as written is denied before the open, the real path after it
            Assert.Null(written);
            Assert.Null(linked);
            Assert.Empty(stub.Requests);
            Assert.Equal(2, resolver.Diagnostics.Count);
            Assert.All(resolver.Diagnostics, note =>
            {
                Assert.Equal((ImageDiagnosticSeverity.Warning, FolderImageResolver.AccessDeniedCode), (note.Severity, note.Code));
                Assert.Contains("access denied by the file-access policy 'roots'", note.Message, StringComparison.Ordinal);
                Assert.Contains("Hint: add the folder the file is in to the allowed roots", note.Message, StringComparison.Ordinal);
                Assert.DoesNotContain(folder.Path, note.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("policy=roots", note.Detail, StringComparison.Ordinal);
            });
            var first = resolver.Diagnostics[0];
            Assert.Contains("'../outside/a.stub'", first.Message, StringComparison.Ordinal);
            Assert.Contains("the path as written is outside the allowed roots", first.Message, StringComparison.Ordinal);
            Assert.Contains("requested=" + folder.Combine("outside", "a.stub"), first.Detail, StringComparison.Ordinal);
            var second = resolver.Diagnostics[1];
            Assert.Contains("its real path is outside the allowed roots", second.Message, StringComparison.Ordinal);
            Assert.Contains("real=" + Path.Join(FileTestSupport.Canonical(folder.Combine("outside")), "a.stub"), second.Detail, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            FileTestSupport.RemoveDirectoryLink(lib);
        }
    }

    /// <summary>
    ///     Test that a file request carries the policy judged from the origin of the chain: a source that reads a file
    ///     of its own through a <see cref="FileReferenceResolver"/> based at <see cref="ImageSourceRequest.Directory"/>
    ///     is denied under no-symlinks when that folder was swapped for a link after the outer check, and reads the file
    ///     under the default policy.
    /// </summary>
    /// <param name="noSymlinks">Whether the resolver uses the no-symlinks policy (otherwise the default).</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FolderImageResolver_Resolve_FileRequest_CarriesThePolicyAnchoredAtTheChainOrigin(bool noSymlinks)
    {
        // Arrange: site/sub/model.stub is real when the link is checked; the source then swaps site/sub for a link to
        // a folder that holds data.txt and reads data.txt the way a format library reads an include
        using var folder = new ImageTestFolder();
        var site = Directory.CreateDirectory(folder.Combine("site")).FullName;
        folder.WriteText("site/sub/model.stub", "model");
        folder.WriteText("elsewhere/data.txt", "data");
        var sub = Path.Combine(site, "sub");
        var link = Path.Combine(site, "sub-link");
        FileTestSupport.CreateDirectoryLinkOrSkip(link, folder.Combine("elsewhere"));
        string? read = null;
        FileReferenceException? denied = null;
        var source = new StubImageSource("stub", [Ext], null, request =>
        {
            // The link is renamed into place, so the folder of the request now passes through it
            Directory.Move(sub, Path.Combine(site, "sub-old"));
            Directory.Move(link, sub);
            var files = new FileReferenceResolver(new FileReferenceOptions { BaseDirectory = request.Directory, FileAccess = request.FileAccess });
            try
            {
                read = Encoding.UTF8.GetString(files.Resolve("data.txt")!.Content.Span);
            }
            catch (FileReferenceException ex)
            {
                denied = ex;
            }

            return new ImageSourceResult(StubImageSource.Solid(4, 4, StubImageSource.Green));
        });
        var options = new FolderImageResolverOptions { FileAccess = noSymlinks ? FileAccessPolicy.NoSymlinks : FileAccessPolicy.AllowAll };
        var resolver = new FolderImageResolver(site, [source], options);

        try
        {
            // Act
            using var surface = resolver.Resolve("sub/model.stub", 10, 10);

            // Assert: the outer link was drawn; the nested read follows the policy of the outer chain
            Assert.NotNull(surface);
            Assert.Equal(options.FileAccess.Name, Assert.Single(source.Requests).FileAccess.Name);
            if (noSymlinks)
            {
                Assert.Null(read);
                Assert.Equal(FileReferenceFailureKind.AccessDenied, denied?.Kind);
            }
            else
            {
                Assert.Equal("data", read);
                Assert.Null(denied);
            }
        }
        finally
        {
            FileTestSupport.RemoveDirectoryLink(Directory.Exists(Path.Combine(site, "sub-old")) ? sub : link);
        }
    }

    /// <summary>
    ///     Test that a file that embeds itself through a directory link (another spelling of the same file) is a cycle
    ///     found by its real path and stops at once, under the default policy.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_LinkAliasCycle_StopsAtTheRealPath()
    {
        // Arrange: site/alias is a link to site itself, and a.stub embeds alias/a.stub
        using var folder = new ImageTestFolder();
        var site = Directory.CreateDirectory(folder.Combine("site")).FullName;
        folder.WriteText("site/a.stub", "alias/a.stub");
        var alias = Path.Combine(site, "alias");
        FileTestSupport.CreateDirectoryLinkOrSkip(alias, site);
        var chain = new StubImageSource("chain", [Ext], null, request =>
        {
            var next = Encoding.UTF8.GetString(request.Content.Span).Trim();
            request.Resolver!.Resolve(next, 10, 10)?.Dispose();
            return new ImageSourceResult(StubImageSource.Solid(4, 4, StubImageSource.Green));
        });
        var resolver = new FolderImageResolver(site, [chain]);

        try
        {
            // Act
            using var surface = resolver.Resolve("a.stub", 10, 10);

            // Assert: only the outer file was drawn; the alias is the same file and stops as a cycle
            Assert.NotNull(surface);
            Assert.Single(chain.Requests);
            var note = Assert.Single(resolver.Diagnostics);
            Assert.Equal(FolderImageResolver.UnresolvedCode, note.Code);
            Assert.Contains("cycle", note.Message, StringComparison.Ordinal);
        }
        finally
        {
            FileTestSupport.RemoveDirectoryLink(alias);
        }
    }

    /// <summary>
    ///     Test that a render of a stream and a scheme-claimed link carry the policy of the resolver in their requests,
    ///     and that the stream request judges a nested read from the folder of the resolver.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Render_Stream_RequestCarriesThePolicy()
    {
        // Arrange: a policy that records what it is asked; the stream source reads a file of its own from a sub folder
        using var folder = new ImageTestFolder();
        folder.WriteText("sub/data.txt", "data");
        var policy = new RecordingPolicy(_ => FileAccessDecision.Allow);
        var stream = new StubImageSource("stub", [Ext], null, request =>
        {
            var files = new FileReferenceResolver(new FileReferenceOptions
            {
                BaseDirectory = Path.Combine(request.Directory!, "sub"),
                FileAccess = request.FileAccess
            });
            files.Resolve("data.txt");
            return new ImageSourceResult(StubImageSource.Solid(4, 4, StubImageSource.Green));
        });
        var camera = new StubImageSource("camera", null, ["camera"]);
        var resolver = new FolderImageResolver(folder.Path, [stream, camera], new FolderImageResolverOptions { FileAccess = policy });

        // Act
        using var content = new MemoryStream("x"u8.ToArray());
        resolver.Render(content, "top.stub").Surface.Dispose();
        using var claimed = resolver.Resolve("camera:front", 10, 10);

        // Assert: the nested read was judged from the folder of the resolver; the scheme request got the policy itself
        Assert.Equal("recording", Assert.Single(stream.Requests).FileAccess.Name);
        Assert.Equal(Path.GetFullPath(folder.Path), Assert.Single(policy.Requests).BaseDirectory);
        Assert.Same(policy, Assert.Single(camera.Requests).FileAccess);
    }

    /// <summary>
    ///     Test that the file-access option of the resolver is the allow-all policy by default and refuses null.
    /// </summary>
    [Fact]
    public void FolderImageResolverOptions_FileAccess_DefaultsToAllowAllAndRejectsNull()
    {
        // Act
        var options = new FolderImageResolverOptions();
        var confined = options with { FileAccess = FileAccessPolicy.NoSymlinks };

        // Assert
        Assert.Same(FileAccessPolicy.AllowAll, options.FileAccess);
        Assert.Same(FileAccessPolicy.NoSymlinks, confined.FileAccess);
        Assert.Throws<ArgumentNullException>(() => new FolderImageResolverOptions { FileAccess = null! });
    }
}

using DemaConsulting.CanvasNet.Tool.PandocFilter;

namespace DemaConsulting.CanvasNet.Tool.Tests.PandocFilter;

/// <summary>
///     Unit tests for the FilterPaths unit: the Lua filter's path rules and the <c>pandoc.path</c> functions they use.
/// </summary>
public class FilterPathsTests
{
    /// <summary>
    ///     Test that collapsing removes <c>.</c>, applies <c>..</c>, keeps a rooted path at its root, and keeps a leading
    ///     <c>..</c> of a relative path; the separators are the platform's, as <c>pandoc.path.normalize</c> writes them.
    /// </summary>
    /// <param name="path">The path, with forward slashes.</param>
    /// <param name="expected">The collapsed path, with forward slashes.</param>
    [Theory]
    [InlineData("/a/b/../c", "/a/c")]
    [InlineData("/a/./b//c/", "/a/b/c")]
    [InlineData("/../../a", "/a")]
    [InlineData("../a/./b", "../a/b")]
    [InlineData("a/../../b", "../b")]
    [InlineData("a/b/..", "a")]
    public void FilterPaths_Collapse_DotSegments_AreResolvedLikeTheLuaFilter(string path, string expected)
    {
        // Act / Assert
        Assert.Equal(expected.Replace('/', FilterPaths.Separator), FilterPaths.Collapse(path));
    }

    /// <summary>
    ///     Test the Windows drive rules: a drive letter is upper-cased, <c>..</c> stops at the drive, and a path on another
    ///     drive has no relative path.
    /// </summary>
    [Fact]
    public void FilterPaths_Collapse_WindowsDrives_FollowSystemFilePath()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows path rules");

        // Act / Assert
        Assert.Equal(@"C:\y", FilterPaths.Collapse("c:/x/../../y"));
        Assert.True(FilterPaths.IsAbsolute(@"C:\x"));
        Assert.False(FilterPaths.IsAbsolute(@"\x"));
        Assert.False(FilterPaths.IsAbsolute("C:x"));
        Assert.Null(FilterPaths.RelativePath(@"C:\a", @"D:\b"));
        Assert.Equal(@"\\server\x", FilterPaths.Collapse(@"\\server\share\..\..\x"));
    }

    /// <summary>
    ///     Test the relative path from a folder, with forward slashes, and the label that falls back to the full path.
    /// </summary>
    [Fact]
    public void FilterPaths_RelativePath_SharedRoot_ClimbsWithForwardSlashes()
    {
        // Arrange: a folder and two targets under the temporary folder
        var root = FilterPaths.Collapse(Path.GetTempPath());
        var from = FilterPaths.Join([root, "a", "b"]);
        var target = FilterPaths.Join([root, "a", "c", "d.png"]);

        // Act / Assert
        Assert.Equal("../c/d.png", FilterPaths.RelativePath(from, target));
        Assert.Equal("c/d.png", FilterPaths.Label(FilterPaths.Join([root, "a"]), target));
    }

    /// <summary>
    ///     Test the <c>file:</c> URI rule of the tool: no host, percent-decoded, a drive after a slash.
    /// </summary>
    /// <param name="uri">The URI.</param>
    /// <param name="expected">The path, or <see langword="null"/>.</param>
    [Theory]
    [InlineData("file:///C:/x%20y.csv", "C:/x y.csv")]
    [InlineData("FILE:/data/a.csv", "/data/a.csv")]
    [InlineData("file:///a", "/a")]
    [InlineData("file://host/a", null)]
    [InlineData("file:///%2F%2Fserver/a", null)]
    [InlineData("files:///a", null)]
    public void FilterPaths_FileUriPath_HostlessUris_ArePaths(string uri, string? expected)
    {
        Assert.Equal(expected, FilterPaths.FileUriPath(uri));
    }

    /// <summary>
    ///     Test the remote rule of the tool: two leading slashes, a scheme of two or more characters other than file, or a
    ///     file URI with a host.
    /// </summary>
    /// <param name="url">The URL.</param>
    /// <param name="remote">Whether it is remote.</param>
    [Theory]
    [InlineData("https://example.com/a.csv", true)]
    [InlineData("  data:text/csv,a", true)]
    [InlineData("//server/a.csv", true)]
    [InlineData(@"\\server\a.csv", true)]
    [InlineData("file://host/a.csv", true)]
    [InlineData("file:///a.csv", false)]
    [InlineData("C:/a.csv", false)]
    [InlineData("data/a.csv", false)]
    public void FilterPaths_IsRemoteUrl_FollowsTheToolsRule(string url, bool remote)
    {
        Assert.Equal(remote, FilterPaths.IsRemoteUrl(url));
    }

    /// <summary>
    ///     Test the local data file rule: a backslash is a slash, a percent sign is kept, a fragment is cut from an image
    ///     link only, a file URI is its path, and a blank or remote URL is none.
    /// </summary>
    [Fact]
    public void FilterPaths_LocalDataFile_RelativeAbsoluteAndRemote_ResolveLikeTheTool()
    {
        // Arrange: a folder
        var folder = FilterPaths.Collapse(Path.Combine(Path.GetTempPath(), "spec"));
        var expected = (string name) => FilterPaths.Join([folder, .. name.Split('/')]);

        // Act / Assert
        Assert.Equal(expected("data/a.csv"), FilterPaths.LocalDataFile(folder, @"data\a.csv"));
        Assert.Equal(expected("a%20b.csv"), FilterPaths.LocalDataFile(folder, "a%20b.csv"));
        Assert.Equal(expected("p.png#x"), FilterPaths.LocalDataFile(folder, "p.png#x"));
        Assert.Equal(expected("p.png"), FilterPaths.LocalImageFile(folder, "p.png#page=2"));
        Assert.Equal(expected("x.csv"), FilterPaths.LocalDataFile(folder, "file:///" + folder.Replace('\\', '/').TrimStart('/') + "/x.csv"));
        Assert.Null(FilterPaths.LocalDataFile(folder, "  "));
        Assert.Null(FilterPaths.LocalDataFile(folder, "https://example.com/a.csv"));
    }

    /// <summary>
    ///     Test that a path is under a folder by whole segments, with the letter case ignored on Windows only.
    /// </summary>
    [Fact]
    public void FilterPaths_IsSameOrUnder_ComparesWholeSegments()
    {
        // Arrange: a folder
        var folder = FilterPaths.Collapse(Path.Combine(Path.GetTempPath(), "docs"));

        // Act / Assert
        Assert.True(FilterPaths.IsSameOrUnder(folder, folder + FilterPaths.Separator));
        Assert.True(FilterPaths.IsSameOrUnder(FilterPaths.Join([folder, "a", "b.png"]), folder));
        Assert.False(FilterPaths.IsSameOrUnder(folder + "2", folder));
        Assert.Equal(OperatingSystem.IsWindows(), FilterPaths.IsSameOrUnder(FilterPaths.Join([folder, "x"]), folder.ToUpperInvariant()));
    }

    /// <summary>
    ///     Test that a source is found in the resource path as written or percent-decoded, and not found otherwise.
    /// </summary>
    [Fact]
    public void FilterPaths_LocateSource_ResourcePath_FindsAsWrittenOrDecoded()
    {
        // Arrange: a file with a space in the second resource folder
        using var directory = new TempDirectory();
        Directory.CreateDirectory(directory.Combine("a"));
        Directory.CreateDirectory(directory.Combine("b"));
        File.WriteAllText(directory.Combine("b", "my flow.mmd"), "flowchart LR");
        var folders = new[] { "a", "b" }.Select(d => FilterPaths.Absolute(d, directory.Path)).ToList();

        // Act
        var decoded = FilterPaths.LocateSource("my%20flow.mmd", folders);
        var missing = FilterPaths.LocateSource("none.mmd", folders);

        // Assert
        Assert.Equal(FilterPaths.Absolute(Path.Combine("b", "my flow.mmd"), directory.Path), decoded);
        Assert.Null(missing);
    }

    /// <summary>
    ///     Test the smaller <c>pandoc.path</c> functions: folder, file name, stem, search path, and percent decoding.
    /// </summary>
    [Fact]
    public void FilterPaths_PathFunctions_FollowPandocPath()
    {
        // Act / Assert: folder and names
        Assert.Equal(".", FilterPaths.Directory("name.svg"));
        Assert.Equal("a", FilterPaths.Directory("a/b.svg"));
        Assert.Equal("b.svg", FilterPaths.FileName("a/b.svg"));
        Assert.Equal("chart.vl", FilterPaths.Stem("chart.vl.json"));
        Assert.Equal("é", FilterPaths.DecodePercent("%C3%A9"));
        Assert.Equal("100%", FilterPaths.DecodePercent("100%"));

        // Act / Assert: the search path of the platform (an empty entry is dropped on Windows, "." elsewhere)
        string[] expected = OperatingSystem.IsWindows() ? ["a", "b c"] : ["a", ".", "\"b c\""];
        Assert.Equal(expected, FilterPaths.SplitSearchPath(OperatingSystem.IsWindows() ? "a;;\"b c\"" : "a::\"b c\""));
    }
}

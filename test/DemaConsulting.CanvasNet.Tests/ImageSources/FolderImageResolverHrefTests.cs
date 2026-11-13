// cspell:ignore href hrefs fsecret
using DemaConsulting.CanvasNet.Files;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Tests.ImageSources;

/// <summary>
///     Unit tests for how <see cref="FolderImageResolver"/> reads a link as a URI reference: a query and a fragment are
///     cut off before percent-decoding, an encoded <c>#</c> or <c>?</c> stays a character of the file name, a link of a
///     claimed scheme is not interpreted, and a policy that confines links still refuses everything the former folder
///     sandbox refused.
/// </summary>
public class FolderImageResolverHrefTests
{
    /// <summary>The name of the extension the stub claims.</summary>
    private const string Ext = ".stub";

    /// <summary>
    ///     Test that a relative link with a query, a fragment, or both names the file before them, and that the source
    ///     still receives the link as written.
    /// </summary>
    /// <param name="href">The link.</param>
    /// <param name="expectedFile">The file name the link names.</param>
    [Theory]
    [InlineData("a.stub?cache=1", "a.stub")]
    [InlineData("a.stub#view", "a.stub")]
    [InlineData("a.stub?cache=1#view", "a.stub")]
    [InlineData("a.stub#view?not-a-query", "a.stub")]
    [InlineData("sub/b.STUB?v=2", "sub/b.STUB")]
    [InlineData("./a.stub?", "a.stub")]
    [InlineData("a.stub#", "a.stub")]
    [InlineData("my%20img.stub?x=%23%3F#f", "my img.stub")]
    [InlineData("a.stub#/../../outside", "a.stub")]
    [InlineData("a.stub?/../../outside", "a.stub")]
    public void FolderImageResolver_Resolve_RelativeLinkWithQueryOrFragment_NamesTheFile(string href, string expectedFile)
    {
        // Arrange: the files the links name
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "a");
        folder.WriteText("sub/b.STUB", "b");
        folder.WriteText("my img.stub", "m");
        var stub = new StubImageSource("stub", [Ext]);
        var resolver = new FolderImageResolver(folder.Path, [stub]);

        // Act
        using var surface = resolver.Resolve(href, 10, 10);

        // Assert: drawn; the only notes are about fragment parameters the stub does not declare; the source got the raw
        // link and the name of the file that was read
        Assert.NotNull(surface);
        Assert.All(resolver.Diagnostics, d => Assert.Equal(FolderImageResolver.FragmentUnknownParameterCode, d.Code));
        Assert.Equal(href.Contains("#", StringComparison.Ordinal) && !href.EndsWith('#') ? 1 : 0, resolver.Diagnostics.Count);
        var request = Assert.Single(stub.Requests);
        Assert.Equal(href, request.Href);
        Assert.Equal(expectedFile, request.FileName);
        Assert.Equal(href, resolver.Embedded[0].Href);
    }

    /// <summary>
    ///     Test that the query and fragment are cut off before the decoding: an encoded <c>%23</c> is a literal
    ///     <c>#</c> in the file name, so it names the file <c>a#b.stub</c>, while an unencoded <c>#</c> starts a fragment.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_EncodedHash_IsLiteralCharacterOfTheFileName()
    {
        // Arrange: a file whose name has a hash, and one that is what the unencoded link would name
        using var folder = new ImageTestFolder();
        folder.WriteText("a#b.stub", "hash");
        var stub = new StubImageSource("stub", [Ext]);
        var resolver = new FolderImageResolver(folder.Path, [stub]);

        // Act
        using var encoded = resolver.Resolve("a%23b.stub", 10, 10);
        using var encodedWithFragment = resolver.Resolve("a%23b.stub#frag", 10, 10);
        var literal = resolver.Resolve("a#b.stub", 10, 10);

        // Assert: the encoded links read a#b.stub; the literal one names "a" with a fragment, which nobody draws
        Assert.NotNull(encoded);
        Assert.NotNull(encodedWithFragment);
        Assert.Null(literal);
        Assert.Equal(["a#b.stub", "a#b.stub"], stub.Requests.Select(r => r.FileName));
        var note = Assert.Single(resolver.Diagnostics, d => d.Code == FolderImageResolver.UnresolvedCode);
        Assert.Contains("'a#b.stub'", note.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that an encoded <c>%3F</c> is a literal <c>?</c> of the file name and not the start of a query: where the
    ///     file system allows the character the file is read, and where it does not (Windows) the link is simply
    ///     unresolved, never confused with a query that names another file.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_EncodedQuestionMark_IsLiteralCharacterNotAQuery()
    {
        // Arrange: "a.stub" exists; "a?b.stub" can only exist where the file system allows a question mark
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "plain");
        var created = TryWrite(folder.Combine("a?b.stub"));
        var stub = new StubImageSource("stub", [Ext]);
        var resolver = new FolderImageResolver(folder.Path, [stub]);

        // Act
        var viaEncoding = resolver.Resolve("a%3Fb.stub", 10, 10);
        viaEncoding?.Dispose();

        // Assert: it never reads a.stub (the part before the question mark); it reads a?b.stub or is unresolved
        Assert.DoesNotContain(stub.Requests, r => r.FileName == "a.stub");
        if (created)
        {
            Assert.NotNull(viaEncoding);
            Assert.Equal("a?b.stub", Assert.Single(stub.Requests).FileName);
            Assert.Empty(resolver.Diagnostics);
        }
        else
        {
            Assert.Null(viaEncoding);
            Assert.Equal(FolderImageResolver.UnresolvedCode, Assert.Single(resolver.Diagnostics).Code);
        }
    }

    /// <summary>
    ///     Test that a <c>file:</c> URI inside the folder may carry a query and a fragment, and that an encoded hash in
    ///     it is still a literal character.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_FileUriWithQueryAndFragment_NamesTheFile()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("site/inside.stub", "x");
        folder.WriteText("site/in#side.stub", "x");
        var inside = new Uri(folder.Combine("site", "inside.stub")).AbsoluteUri;
        var hashed = new Uri(folder.Combine("site", "in#side.stub")).AbsoluteUri;
        var stub = new StubImageSource("stub", [Ext]);
        var resolver = new FolderImageResolver(folder.Combine("site"), [stub]);

        // Act
        using var withQuery = resolver.Resolve(inside + "?v=1", 10, 10);
        using var withFragment = resolver.Resolve(inside + "#part", 10, 10);
        using var withBoth = resolver.Resolve(inside + "?v=1#part", 10, 10);
        using var encodedHash = resolver.Resolve(hashed + "?v=1", 10, 10);

        // Assert
        Assert.NotNull(withQuery);
        Assert.NotNull(withFragment);
        Assert.NotNull(withBoth);
        Assert.NotNull(encodedHash);
        Assert.Equal(
            ["inside.stub", "inside.stub", "inside.stub", "in#side.stub"],
            stub.Requests.Select(r => r.FileName));
        Assert.All(resolver.Diagnostics, d => Assert.Equal(FolderImageResolver.FragmentUnknownParameterCode, d.Code));
    }

    /// <summary>
    ///     Test that, under a policy confined to the folder, a <c>file:</c> URI outside the folder is denied whatever query
    ///     or fragment follows it.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_FileUriOutsideFolderWithQuery_UnderRoots_IsStillDenied()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("outside.stub", "x");
        var site = Directory.CreateDirectory(folder.Combine("site")).FullName;
        var outside = new Uri(folder.Combine("outside.stub")).AbsoluteUri;
        var stub = new StubImageSource("stub", [Ext]);
        var resolver = new FolderImageResolver(site, [stub], new FolderImageResolverOptions { FileAccess = FileAccessPolicy.UnderRoots(site) });

        // Act
        var plain = resolver.Resolve(outside + "?v=1#x", 10, 10);
        var traversal = resolver.Resolve(new Uri(site).AbsoluteUri + "/../outside.stub?v=1", 10, 10);

        // Assert
        Assert.Null(plain);
        Assert.Null(traversal);
        Assert.Empty(stub.Requests);
        Assert.Equal(2, resolver.Diagnostics.Count(d => d.Code == FolderImageResolver.AccessDeniedCode));
    }

    /// <summary>
    ///     Test that a link that is nothing but a query or a fragment names no file and is unresolved.
    /// </summary>
    /// <param name="href">The link.</param>
    [Theory]
    [InlineData("?cache=1")]
    [InlineData("#view")]
    [InlineData("?")]
    [InlineData("#")]
    [InlineData("?a#b")]
    public void FolderImageResolver_Resolve_OnlyQueryOrFragment_IsUnresolved(string href)
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "x");
        var stub = new StubImageSource("stub", [Ext]);
        var resolver = new FolderImageResolver(folder.Path, [stub]);

        // Act
        var surface = resolver.Resolve(href, 10, 10);

        // Assert
        Assert.Null(surface);
        Assert.Empty(stub.Requests);
        var note = Assert.Single(resolver.Diagnostics);
        Assert.Equal(FolderImageResolver.UnresolvedCode, note.Code);
        Assert.Contains("the reference is empty", note.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that, under a policy confined to the folder, stripping the query and fragment opens no way out of it: an
    ///     escaping path stays refused with any suffix (denied by the policy, or unresolved where the text itself is
    ///     never allowed), a suffix cannot hide a segment, and the source is never called.
    /// </summary>
    /// <param name="href">The link.</param>
    [Theory]
    [InlineData("../secret.stub?x=1")]
    [InlineData("../secret.stub#frag")]
    [InlineData("..%2fsecret.stub")]
    [InlineData("..%2fsecret.stub?x")]
    [InlineData("%2e%2e/secret.stub#x")]
    [InlineData("%2e%2e%2fsecret.stub?x#y")]
    [InlineData("sub/..%2f..%2fsecret.stub#x")]
    [InlineData("..\\secret.stub?x")]
    [InlineData("/etc/secret.stub?x")]
    [InlineData("C:/secret.stub#x")]
    [InlineData("//host/share/secret.stub?x")]
    [InlineData("a.stub%00.png?x")]
    [InlineData("a.stub%00?x")]
    [InlineData("%00?x.stub")]
    [InlineData("a.stub::$DATA?x")]
    [InlineData("a.stub::$DATA#x")]
    [InlineData("../secret.stub%3Fa.stub")]
    [InlineData("..%2f%23.stub")]
    public void FolderImageResolver_Resolve_EscapingReferenceWithQueryOrFragment_UnderRoots_IsStillNotDrawn(string href)
    {
        // Arrange: a valid file one folder above the folder of the resolver, and files below it
        using var folder = new ImageTestFolder();
        var site = Directory.CreateDirectory(folder.Combine("site", "sub")).Parent!.FullName;
        folder.WriteText("secret.stub", "secret");
        var stub = new StubImageSource("stub", [Ext]);
        var resolver = new FolderImageResolver(site, [stub], new FolderImageResolverOptions { FileAccess = FileAccessPolicy.UnderRoots(site) });

        // Act
        var surface = resolver.Resolve(href, 10, 10);

        // Assert: nothing was drawn, the source was never called, and the one note is the warning naming the link
        Assert.Null(surface);
        Assert.Empty(stub.Requests);
        var note = Assert.Single(resolver.Diagnostics);
        Assert.Contains(note.Code, new[] { FolderImageResolver.UnresolvedCode, FolderImageResolver.AccessDeniedCode });
        Assert.Equal(ImageDiagnosticSeverity.Warning, note.Severity);
        Assert.Contains($"'{href}'", note.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that, under the default policy, a link to the parent folder with a query or a fragment names the file
    ///     before them and is drawn.
    /// </summary>
    /// <param name="href">The link.</param>
    [Theory]
    [InlineData("../secret.stub?x=1")]
    [InlineData("../secret.stub#frag")]
    [InlineData("..%2fsecret.stub?x")]
    [InlineData("%2e%2e%2fsecret.stub?x#y")]
    public void FolderImageResolver_Resolve_ParentReferenceWithQueryOrFragment_Default_NamesTheFile(string href)
    {
        // Arrange
        using var folder = new ImageTestFolder();
        Directory.CreateDirectory(folder.Combine("site"));
        folder.WriteText("secret.stub", "secret");
        var stub = new StubImageSource("stub", [Ext]);
        var resolver = new FolderImageResolver(folder.Combine("site"), [stub]);

        // Act
        using var surface = resolver.Resolve(href, 10, 10);

        // Assert: the only notes are about fragment parameters the stub does not declare
        Assert.NotNull(surface);
        Assert.All(resolver.Diagnostics, d => Assert.Equal(FolderImageResolver.FragmentUnknownParameterCode, d.Code));
        Assert.Equal("../secret.stub", Assert.Single(stub.Requests).FileName);
    }

    /// <summary>
    ///     Test that a link with a URI scheme claimed by a source is handed to it exactly as written, whatever
    ///     <c>?</c> or <c>#</c> it holds, and that a link whose query merely contains a colon is a path, not a scheme.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_SchemeLinkWithQueryAndFragment_IsPassedAsIs()
    {
        // Arrange: a scheme source, a file source, and a file
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "x");
        var camera = new StubImageSource("camera", null, ["camera"]);
        var files = new StubImageSource("stub", [Ext]);
        var resolver = new FolderImageResolver(folder.Path, [camera, files]);

        // Act
        using var first = resolver.Resolve("camera:front?fps=30#raw", 10, 10);
        using var second = resolver.Resolve("CAMERA:Rear?a=b#", 10, 10);
        using var path = resolver.Resolve("a.stub?next=camera:front", 10, 10);

        // Assert: the whole text reaches the scheme source; the third link is a file whose query holds a colon
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotNull(path);
        Assert.Equal(["camera:front?fps=30#raw", "CAMERA:Rear?a=b#"], camera.Requests.Select(r => r.Href));
        Assert.All(camera.Requests, r => Assert.False(r.HasContent));
        Assert.Equal("a.stub", Assert.Single(files.Requests).FileName);
        Assert.Empty(resolver.Diagnostics);
    }

    /// <summary>
    ///     Test that a link of an unclaimed scheme with a query or fragment is still a warning (it is never read as a file).
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_UnclaimedSchemeWithQuery_IsUnresolved()
    {
        // Arrange
        using var folder = new ImageTestFolder();
        folder.WriteText("a.stub", "x");
        var stub = new StubImageSource("stub", [Ext]);
        var resolver = new FolderImageResolver(folder.Path, [stub]);

        // Act
        var surface = resolver.Resolve("https://example.invalid/a.stub?x=1#y", 10, 10);

        // Assert
        Assert.Null(surface);
        Assert.Empty(stub.Requests);
        Assert.Equal(FolderImageResolver.UnresolvedCode, Assert.Single(resolver.Diagnostics).Code);
    }

    /// <summary>
    ///     Test that a link inside an embedded file resolves with its query and fragment cut off, relative to the folder of
    ///     that file.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_NestedLinkWithQueryAndFragment_ResolvesFromFolderOfEmbeddingFile()
    {
        // Arrange: sub/outer.stub holds a link to its sibling, with a query and a fragment
        using var folder = new ImageTestFolder();
        folder.WriteText("sub/outer.stub", "inner.leaf?v=1#top");
        folder.WriteText("sub/inner.leaf", "x");
        var leaf = new StubImageSource("leaf", [".leaf"]);
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

        // Act
        using var surface = resolver.Resolve("sub/outer.stub?x#y", 10, 10);

        // Assert
        Assert.NotNull(surface);
        Assert.Equal("sub/inner.leaf", Assert.Single(leaf.Requests).FileName);
        Assert.Equal("inner.leaf?v=1#top", Assert.Single(leaf.Requests).Href);
        Assert.All(resolver.Diagnostics, d => Assert.Equal(FolderImageResolver.FragmentUnknownParameterCode, d.Code));
    }

    /// <summary>
    ///     Test that the path a host classifies a link by is cut before it is decoded, as the resolver reads it.
    /// </summary>
    [Fact]
    public void FolderImageResolver_DecodeReference_CutsThenDecodes()
    {
        // Act / Assert: an encoded dot is decoded; an encoded # or ? stays part of the name; the real ones are cut off
        Assert.Equal("flow.mmd", FolderImageResolver.DecodeReference("flow%2Emmd#theme=dark"));
        Assert.Equal("a#b.mmd", FolderImageResolver.DecodeReference("a%23b.mmd?v=1#x"));
        Assert.Equal("sub/a b.png", FolderImageResolver.DecodeReference("sub/a%20b.png"));
        Assert.Equal("plain.svg", FolderImageResolver.DecodeReference("plain.svg"));
        Assert.Equal("bad%zz.mmd", FolderImageResolver.DecodeReference("bad%zz.mmd"));
    }

    /// <summary>
    ///     Writes a file whose name may be illegal on the current file system.
    /// </summary>
    /// <param name="path">The full path.</param>
    /// <returns><see langword="true"/> when the file could be created.</returns>
    private static bool TryWrite(string path)
    {
        try
        {
            File.WriteAllText(path, "question");
            return File.Exists(path);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

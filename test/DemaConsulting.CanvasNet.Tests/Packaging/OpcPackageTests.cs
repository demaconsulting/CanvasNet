using System.IO.Compression;
using System.Text;
using System.Xml;
using DemaConsulting.CanvasNet.Packaging;

namespace DemaConsulting.CanvasNet.Tests.Packaging;

// cspell:ignore pptx rels

/// <summary>
///     Unit tests for <see cref="OpcPackage"/> and <see cref="OpcFormatException"/>: opening an OPC
///     ZIP package within its size bounds, content-type resolution, relationship resolution, and
///     reading a part as XML or bytes. The packages are built in memory with synthetic part names and
///     content, so every test is deterministic and independent of any real document.
/// </summary>
public class OpcPackageTests
{
    private const string ContentTypesXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
          <Default Extension="xml" ContentType="application/xml" />
        </Types>
        """;

    private const string PackageRelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://example.com/relationships/main" Target="doc/main.xml" />
        </Relationships>
        """;

    private const string MainRelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://example.com/relationships/layout" Target="../layouts/layout1.xml" />
          <Relationship Id="rId2" Type="http://example.com/relationships/absolute" Target="/shared/absolute.xml" />
          <Relationship Id="rId3" Type="http://example.com/relationships/external" Target="https://example.com/x" TargetMode="External" />
          <Relationship Id="rId4" Type="http://example.com/relationships/dotted" Target="./sub/../page.xml" />
          <Relationship Id="rId5" Type="http://example.com/relationships/fragment" Target="../layouts/layout1.xml#bookmark" />
          <Relationship Id="rId6" Type="http://example.com/relationships/self" Target="#top" />
        </Relationships>
        """;

    /// <summary>Builds an in-memory ZIP whose entries are the given name and text pairs, written verbatim.</summary>
    private static MemoryStream BuildZip(params (string Name, string Content)[] entries)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream, new UTF8Encoding(false));
                writer.Write(content);
            }
        }

        stream.Position = 0;
        return stream;
    }

    /// <summary>Builds a minimal valid package with the given extra entries.</summary>
    private static MemoryStream BuildValidPackage(params (string Name, string Content)[] extra) =>
        BuildZip(
            [
                ("[Content_Types].xml", ContentTypesXml),
                ("_rels/.rels", PackageRelsXml),
                ("doc/main.xml", "<main/>"),
                ("doc/_rels/main.xml.rels", MainRelsXml),
                .. extra,
            ]);

    /// <summary>A stream that never ends: every read fills the whole buffer with zeros.</summary>
    private sealed class InfiniteZeroStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Array.Clear(buffer, offset, count);
            return count;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>A stream that can be neither read nor written.</summary>
    private sealed class UnreadableStream : MemoryStream
    {
        public override bool CanRead => false;
    }

    /// <summary>
    ///     A seekable stream of zeros that allocates nothing: it reports a fixed length and records how many bytes were read,
    ///     so a test can prove a size is judged from the length and how much memory the reader itself allocates.
    /// </summary>
    /// <param name="length">The length the stream reports and, unless <paramref name="endless"/>, supplies.</param>
    /// <param name="endless">Whether reads never end (the reported length then lies).</param>
    private sealed class ZeroStream(long length, bool endless = false) : Stream
    {
        public long BytesRead { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => length;

        public override long Position { get; set; }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var size = endless ? count : (int)Math.Min(count, length - Position);
            Array.Clear(buffer, offset, size);
            Position += size;
            BytesRead += size;
            return size;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>Proves a seekable stream longer than the package bound is rejected from its length, without reading it.</summary>
    [Fact]
    public void OpcPackage_Open_SeekableStreamOverBound_IsRejectedWithoutReading()
    {
        // Arrange
        using var stream = new ZeroStream(OpcPackage.MaxPackageBytes + 1);

        // Act / Assert
        var exception = Assert.Throws<OpcFormatException>(() => OpcPackage.Open(stream));
        Assert.Contains(OpcPackage.MaxPackageBytes.ToString(System.Globalization.CultureInfo.InvariantCulture), exception.Message);
        Assert.Equal(0, stream.BytesRead);
    }

    /// <summary>
    ///     Proves a seekable stream is buffered into one array of its own size: the memory allocated while opening
    ///     (here a non-ZIP stream, which is rejected afterwards) stays at the stream's length plus a small constant,
    ///     rather than the up to twice that a doubling buffer reaches.
    /// </summary>
    [Fact]
    public void OpcPackage_Open_SeekableStream_AllocatesNoMoreThanItsLength()
    {
        // Arrange: a length just above a power of two, where a doubling buffer would allocate far more than needed
        const long length = (16L * 1024 * 1024) + 12345;
        using var stream = new ZeroStream(length);

        // Act
        var before = GC.GetAllocatedBytesForCurrentThread();
        Assert.Throws<OpcFormatException>(() => OpcPackage.Open(stream));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert
        Assert.Equal(length, stream.BytesRead);
        Assert.True(allocated < length + (1024 * 1024), $"Allocated {allocated} bytes for {length}.");
    }

    /// <summary>
    ///     Proves the bounded reader never holds an array beyond the bound, whether or not the length is known, rejects as
    ///     soon as one byte more than the bound exists, and does not trust a lying length in either direction.
    /// </summary>
    [Fact]
    public void OpcPackage_ReadBounded_NeverAllocatesBeyondTheBound()
    {
        // Arrange: a bound that is not a power of two, so a doubling buffer would overshoot it
        const long max = 3_000_000;
        const string message = "too big";

        // Act / Assert: exactly at the bound, with an unknown, an exact, a too small and a too large expected length
        foreach (var expected in new long[] { 0, max, 10, long.MaxValue })
        {
            using var atBound = new ZeroStream(max);
            var segment = OpcPackage.ReadBounded(atBound, expected, max, message);
            Assert.Equal(max, segment.Count);
            Assert.True(segment.Array!.Length <= max, $"Array of {segment.Array.Length} for expected {expected}.");
        }

        // Act / Assert: one byte over the bound is rejected, after reading no more than the bound plus that byte
        foreach (var expected in new long[] { 0, max, 10, long.MaxValue })
        {
            using var overBound = new ZeroStream(max + 1);
            var exception = Assert.Throws<OpcFormatException>(() => OpcPackage.ReadBounded(overBound, expected, max, message));
            Assert.Equal(message, exception.Message);
            Assert.Equal(max + 1, overBound.BytesRead);
        }

        // Act / Assert: an endless stream is cut off at the same point
        using var endless = new ZeroStream(0, endless: true);
        Assert.Throws<OpcFormatException>(() => OpcPackage.ReadBounded(endless, 0, max, message));
        Assert.Equal(max + 1, endless.BytesRead);
    }

    /// <summary>
    ///     Proves a part is read into one array of its own size: the memory allocated while reading it stays at its
    ///     length plus a small constant, rather than a doubling buffer plus a copy.
    /// </summary>
    [Fact]
    public void OpcPackage_GetPartBytes_AllocatesNoMoreThanThePartLength()
    {
        // Arrange: a part just above a power of two
        const long length = (4L * 1024 * 1024) + 321;
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in new[] { ("[Content_Types].xml", ContentTypesXml), ("_rels/.rels", PackageRelsXml) })
            {
                using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }

            WriteZeroEntry(archive, "media/big.bin", length);
        }

        stream.Position = 0;
        using var package = OpcPackage.Open(stream);

        // Act
        var before = GC.GetAllocatedBytesForCurrentThread();
        var bytes = package.GetPartBytes("media/big.bin");
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert
        Assert.Equal(length, bytes.Length);
        Assert.True(allocated < length + (512 * 1024), $"Allocated {allocated} bytes for {length}.");
    }

    /// <summary>
    ///     Writes <paramref name="totalBytes"/> of zeros to a new entry in 64 KiB chunks. Zeros
    ///     compress extremely well, so the entry is a genuine zip-bomb shape: a tiny compressed entry
    ///     that inflates to many times its size.
    /// </summary>
    private static void WriteZeroEntry(ZipArchive archive, string name, long totalBytes)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        var chunk = new byte[65536];
        var remaining = totalBytes;
        while (remaining > 0)
        {
            var size = (int)Math.Min(chunk.Length, remaining);
            entryStream.Write(chunk, 0, size);
            remaining -= size;
        }
    }

    /// <summary>Proves a minimal valid package opens, and the open does not close the caller's stream.</summary>
    [Fact]
    public void OpcPackage_Open_MinimalValidPackage_OpensAndLeavesStreamOpen()
    {
        // Arrange
        using var stream = BuildValidPackage();

        // Act
        using var package = OpcPackage.Open(stream);

        // Assert: the package-level relationship resolves and the stream is still usable
        Assert.Equal("doc/main.xml", package.ResolveRelationship(string.Empty, "rId1"));
        Assert.True(stream.CanRead);
    }

    /// <summary>Proves a null stream is rejected with the parameter name.</summary>
    [Fact]
    public void OpcPackage_Open_NullStream_ThrowsArgumentNullException()
    {
        // Act / Assert
        var exception = Assert.Throws<ArgumentNullException>(() => OpcPackage.Open(null!));
        Assert.Equal("stream", exception.ParamName);
    }

    /// <summary>Proves a stream that cannot be read is rejected up front with the parameter name.</summary>
    [Fact]
    public void OpcPackage_Open_UnreadableStream_ThrowsArgumentException()
    {
        // Arrange
        using var stream = new UnreadableStream();

        // Act / Assert
        var exception = Assert.Throws<ArgumentException>(() => OpcPackage.Open(stream));
        Assert.Equal("stream", exception.ParamName);
    }

    /// <summary>Proves bytes that are not a ZIP archive are rejected with <see cref="OpcFormatException"/>.</summary>
    [Fact]
    public void OpcPackage_Open_NonZipStream_ThrowsOpcFormatException()
    {
        // Arrange
        using var stream = new MemoryStream([1, 2, 3, 4, 5, 6, 7, 8]);

        // Act / Assert
        Assert.Throws<OpcFormatException>(() => OpcPackage.Open(stream));
    }

    /// <summary>Proves a stream that never ends is cut off at the package bound instead of buffering forever.</summary>
    [Fact]
    public void OpcPackage_Open_NonTerminatingStream_ThrowsOpcFormatException()
    {
        // Arrange
        using var stream = new InfiniteZeroStream();

        // Act / Assert
        var exception = Assert.Throws<OpcFormatException>(() => OpcPackage.Open(stream));
        Assert.Contains(OpcPackage.MaxPackageBytes.ToString(System.Globalization.CultureInfo.InvariantCulture), exception.Message);
    }

    /// <summary>Proves a package without <c>[Content_Types].xml</c> is rejected.</summary>
    [Fact]
    public void OpcPackage_Open_MissingContentTypes_ThrowsOpcFormatException()
    {
        // Arrange
        using var stream = BuildZip(("_rels/.rels", PackageRelsXml));

        // Act / Assert
        var exception = Assert.Throws<OpcFormatException>(() => OpcPackage.Open(stream));
        Assert.Contains("[Content_Types].xml", exception.Message);
    }

    /// <summary>Proves a package without <c>_rels/.rels</c> is rejected.</summary>
    [Fact]
    public void OpcPackage_Open_MissingPackageRelationships_ThrowsOpcFormatException()
    {
        // Arrange
        using var stream = BuildZip(("[Content_Types].xml", ContentTypesXml));

        // Act / Assert
        var exception = Assert.Throws<OpcFormatException>(() => OpcPackage.Open(stream));
        Assert.Contains("_rels/.rels", exception.Message);
    }

    /// <summary>Proves part names are compared case-sensitively, so a wrongly cased reserved part is not accepted.</summary>
    [Fact]
    public void OpcPackage_Open_WrongCaseContentTypesPartName_ThrowsOpcFormatException()
    {
        // Arrange
        using var stream = BuildZip(("[content_types].xml", ContentTypesXml), ("_rels/.rels", PackageRelsXml));

        // Act / Assert
        Assert.Throws<OpcFormatException>(() => OpcPackage.Open(stream));
    }

    /// <summary>Proves two entries with an identical name are rejected as a malformed package.</summary>
    [Fact]
    public void OpcPackage_Open_DuplicateEntryNames_ThrowsOpcFormatException()
    {
        // Arrange: ZipArchive itself allows creating two entries with the same name
        using var stream = BuildZip(("[Content_Types].xml", ContentTypesXml), ("[Content_Types].xml", ContentTypesXml));

        // Act / Assert
        var exception = Assert.Throws<OpcFormatException>(() => OpcPackage.Open(stream));
        Assert.Contains("duplicate", exception.Message);
    }

    /// <summary>Proves parts whose names differ only by case are distinct parts, each with its own content type.</summary>
    [Fact]
    public void OpcPackage_ResolvePart_NamesDifferingOnlyByCase_AreDistinctParts()
    {
        // Arrange
        const string contentTypes =
            """
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/rels" />
              <Override PartName="/doc/Page.xml" ContentType="type/upper" />
              <Override PartName="/doc/page.xml" ContentType="type/lower" />
            </Types>
            """;
        using var stream = BuildZip(
            ("[Content_Types].xml", contentTypes),
            ("_rels/.rels", PackageRelsXml),
            ("doc/Page.xml", "<a/>"),
            ("doc/page.xml", "<b/>"));
        using var package = OpcPackage.Open(stream);

        // Act / Assert
        Assert.Equal("type/upper", package.ResolvePart("doc/Page.xml"));
        Assert.Equal("type/lower", package.ResolvePart("doc/page.xml"));
    }

    /// <summary>Proves an oversized XML part is rejected at the character bound instead of being parsed.</summary>
    [Fact]
    public void OpcPackage_Open_OversizedContentTypesPart_ThrowsOpcFormatException()
    {
        // Arrange: an oversized comment pushes the part past the character bound
        var padding = new string('x', OpcPackage.MaxXmlPartCharacters + 100_000);
        var oversized = $"<?xml version=\"1.0\"?><!--{padding}-->{ContentTypesXml.Replace("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>", string.Empty)}";
        using var stream = BuildZip(("[Content_Types].xml", oversized), ("_rels/.rels", PackageRelsXml));

        // Act / Assert
        var exception = Assert.Throws<OpcFormatException>(() => OpcPackage.Open(stream));
        Assert.IsType<XmlException>(exception.InnerException);
    }

    /// <summary>Proves an XML part that declares a DTD is rejected rather than processed.</summary>
    [Fact]
    public void OpcPackage_Open_ContentTypesWithDocumentType_ThrowsOpcFormatException()
    {
        // Arrange: an external-entity DTD, the classic XXE vehicle
        const string withDtd =
            """
            <?xml version="1.0"?>
            <!DOCTYPE Types [<!ENTITY xxe SYSTEM "file:///does/not/matter">]>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">&xxe;</Types>
            """;
        using var stream = BuildZip(("[Content_Types].xml", withDtd), ("_rels/.rels", PackageRelsXml));

        // Act / Assert
        var exception = Assert.Throws<OpcFormatException>(() => OpcPackage.Open(stream));
        Assert.IsType<XmlException>(exception.InnerException);
    }

    /// <summary>Proves malformed XML in a required part is rejected.</summary>
    [Fact]
    public void OpcPackage_Open_ContentTypesNotWellFormed_ThrowsOpcFormatException()
    {
        // Arrange
        using var stream = BuildZip(("[Content_Types].xml", "<Types"), ("_rels/.rels", PackageRelsXml));

        // Act / Assert
        var exception = Assert.Throws<OpcFormatException>(() => OpcPackage.Open(stream));
        Assert.Contains("not well-formed", exception.Message);
    }

    /// <summary>Proves a content-types part with the wrong root element is rejected.</summary>
    [Fact]
    public void OpcPackage_Open_ContentTypesWrongRoot_ThrowsOpcFormatException()
    {
        // Arrange
        using var stream = BuildZip(("[Content_Types].xml", "<Root/>"), ("_rels/.rels", PackageRelsXml));

        // Act / Assert
        Assert.Throws<OpcFormatException>(() => OpcPackage.Open(stream));
    }

    /// <summary>Proves a Default or Override element lacking a required attribute is rejected.</summary>
    [Fact]
    public void OpcPackage_Open_ContentTypeEntryMissingAttribute_ThrowsOpcFormatException()
    {
        // Arrange
        string[] elements =
        [
            """<Default ContentType="a/b" />""",
            """<Default Extension="xml" />""",
            """<Override ContentType="a/b" />""",
            """<Override PartName="/doc/main.xml" />""",
        ];

        foreach (var element in elements)
        {
            var contentTypes =
                $"""<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">{element}</Types>""";
            using var stream = BuildZip(("[Content_Types].xml", contentTypes), ("_rels/.rels", PackageRelsXml));

            // Act / Assert
            Assert.Throws<OpcFormatException>(() => OpcPackage.Open(stream));
        }
    }

    /// <summary>Proves a relationships part with the wrong root element is rejected.</summary>
    [Fact]
    public void OpcPackage_Open_RelationshipsWrongRoot_ThrowsOpcFormatException()
    {
        // Arrange
        using var stream = BuildZip(("[Content_Types].xml", ContentTypesXml), ("_rels/.rels", "<wrongRoot/>"));

        // Act / Assert
        Assert.Throws<OpcFormatException>(() => OpcPackage.Open(stream));
    }

    /// <summary>Proves a Relationship element lacking Id, Type or Target is rejected.</summary>
    [Fact]
    public void OpcPackage_Open_RelationshipMissingAttribute_ThrowsOpcFormatException()
    {
        // Arrange
        string[] elements =
        [
            """<Relationship Type="t" Target="a.xml" />""",
            """<Relationship Id="rId1" Target="a.xml" />""",
            """<Relationship Id="rId1" Type="t" />""",
        ];

        foreach (var element in elements)
        {
            var rels = $"""<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">{element}</Relationships>""";
            using var stream = BuildZip(("[Content_Types].xml", ContentTypesXml), ("_rels/.rels", rels));

            // Act / Assert
            Assert.Throws<OpcFormatException>(() => OpcPackage.Open(stream));
        }
    }

    /// <summary>Proves two relationships with the same Id are rejected instead of one silently winning.</summary>
    [Fact]
    public void OpcPackage_Open_DuplicateRelationshipId_ThrowsOpcFormatException()
    {
        // Arrange
        const string rels =
            """
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="t" Target="a.xml" />
              <Relationship Id="rId1" Type="t" Target="b.xml" />
            </Relationships>
            """;
        using var stream = BuildZip(("[Content_Types].xml", ContentTypesXml), ("_rels/.rels", rels));

        // Act / Assert
        var exception = Assert.Throws<OpcFormatException>(() => OpcPackage.Open(stream));
        Assert.Contains("duplicate", exception.Message);
    }

    /// <summary>Proves an Override content type takes precedence over the Default for the extension.</summary>
    [Fact]
    public void OpcPackage_ResolvePart_Override_TakesPrecedenceOverDefault()
    {
        // Arrange
        const string contentTypes =
            """
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/rels" />
              <Default Extension="xml" ContentType="application/xml" />
              <Override PartName="/doc/main.xml" ContentType="application/vnd.example.main+xml" />
            </Types>
            """;
        using var stream = BuildZip(
            ("[Content_Types].xml", contentTypes),
            ("_rels/.rels", PackageRelsXml),
            ("doc/main.xml", "<main/>"));
        using var package = OpcPackage.Open(stream);

        // Act / Assert: with and without the leading slash
        Assert.Equal("application/vnd.example.main+xml", package.ResolvePart("doc/main.xml"));
        Assert.Equal("application/vnd.example.main+xml", package.ResolvePart("/doc/main.xml"));
    }

    /// <summary>Proves a part without an override resolves through the (case-insensitive) extension default.</summary>
    [Fact]
    public void OpcPackage_ResolvePart_NoOverride_UsesDefaultExtension()
    {
        // Arrange
        using var stream = BuildValidPackage(("doc/shout.XML", "<s/>"));
        using var package = OpcPackage.Open(stream);

        // Act / Assert: the Default extension match ignores case
        Assert.Equal("application/xml", package.ResolvePart("doc/main.xml"));
        Assert.Equal("application/xml", package.ResolvePart("doc/shout.XML"));
    }

    /// <summary>Proves resolving a missing part, or one with no content type, is rejected.</summary>
    [Fact]
    public void OpcPackage_ResolvePart_UnknownPartOrNoContentType_ThrowsOpcFormatException()
    {
        // Arrange
        using var stream = BuildValidPackage(("doc/data.bin", "x"));
        using var package = OpcPackage.Open(stream);

        // Act / Assert
        Assert.Throws<OpcFormatException>(() => package.ResolvePart("doc/missing.xml"));
        var noType = Assert.Throws<OpcFormatException>(() => package.ResolvePart("doc/data.bin"));
        Assert.Contains("content type", noType.Message);
    }

    /// <summary>Proves relationship targets resolve relative to the source directory, from the root, and through dot segments.</summary>
    [Fact]
    public void OpcPackage_ResolveRelationship_RelativeAbsoluteAndDotTargets_ResolveToPartPaths()
    {
        // Arrange
        using var stream = BuildValidPackage();
        using var package = OpcPackage.Open(stream);

        // Act / Assert
        Assert.Equal("layouts/layout1.xml", package.ResolveRelationship("doc/main.xml", "rId1"));
        Assert.Equal("shared/absolute.xml", package.ResolveRelationship("doc/main.xml", "rId2"));
        Assert.Equal("doc/page.xml", package.ResolveRelationship("/doc/main.xml", "rId4"));
    }

    /// <summary>Proves a URI fragment is dropped from a target, and a fragment-only target names the source part itself.</summary>
    [Fact]
    public void OpcPackage_ResolveRelationship_FragmentTargets_DropTheFragment()
    {
        // Arrange
        using var stream = BuildValidPackage();
        using var package = OpcPackage.Open(stream);

        // Act / Assert
        Assert.Equal("layouts/layout1.xml", package.ResolveRelationship("doc/main.xml", "rId5"));
        Assert.Equal("doc/main.xml", package.ResolveRelationship("doc/main.xml", "rId6"));
        Assert.Equal("layouts/layout1.xml", package.TryResolveRelationshipById("doc/main.xml", "rId5"));
    }

    /// <summary>Proves a target that climbs above the package root is rejected, not clamped.</summary>
    [Fact]
    public void OpcPackage_ResolveRelationship_TargetEscapesPackageRoot_ThrowsOpcFormatException()
    {
        // Arrange
        const string rels =
            """
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="t" Target="../../outside.xml" />
            </Relationships>
            """;
        using var stream = BuildValidPackage(("one/_rels/part.xml.rels", rels), ("one/part.xml", "<p/>"));
        using var package = OpcPackage.Open(stream);

        // Act / Assert
        var exception = Assert.Throws<OpcFormatException>(() => package.ResolveRelationship("one/part.xml", "rId1"));
        Assert.Contains("escapes the package root", exception.Message);
    }

    /// <summary>Proves an unknown relationship identifier and an external target are rejected.</summary>
    [Fact]
    public void OpcPackage_ResolveRelationship_UnknownIdOrExternalTarget_ThrowsOpcFormatException()
    {
        // Arrange
        using var stream = BuildValidPackage();
        using var package = OpcPackage.Open(stream);

        // Act / Assert
        Assert.Throws<OpcFormatException>(() => package.ResolveRelationship("doc/main.xml", "rIdMissing"));
        var external = Assert.Throws<OpcFormatException>(() => package.ResolveRelationship("doc/main.xml", "rId3"));
        Assert.Contains("external", external.Message);
    }

    /// <summary>Proves a part with no relationships part has no relationships, and that is not an error for the Try forms.</summary>
    [Fact]
    public void OpcPackage_TryResolveRelationship_PartWithoutRelationships_ReturnsNull()
    {
        // Arrange
        using var stream = BuildValidPackage();
        using var package = OpcPackage.Open(stream);

        // Act / Assert
        Assert.Null(package.TryResolveRelationshipByType("doc/other.xml", "/layout"));
        Assert.Null(package.TryResolveRelationshipById("doc/other.xml", "rId1"));
    }

    /// <summary>Proves the type-suffix lookups match case-sensitively, skip external relationships, and report a missing type.</summary>
    [Fact]
    public void OpcPackage_ResolveRelationshipByType_SuffixMatch_ResolvesNonExternalTarget()
    {
        // Arrange
        using var stream = BuildValidPackage();
        using var package = OpcPackage.Open(stream);

        // Act / Assert
        Assert.Equal("doc/main.xml", package.ResolveRelationshipByType(string.Empty, "/main"));
        Assert.Equal("layouts/layout1.xml", package.TryResolveRelationshipByType("doc/main.xml", "/layout"));
        Assert.Null(package.TryResolveRelationshipByType("doc/main.xml", "/external"));
        Assert.Null(package.TryResolveRelationshipByType("doc/main.xml", "/LAYOUT"));
        var exception = Assert.Throws<OpcFormatException>(() => package.ResolveRelationshipByType("doc/main.xml", "/nothing"));
        Assert.Contains("/nothing", exception.Message);
    }

    /// <summary>Proves lookup by identifier resolves a non-external target and returns null for unknown or external ones.</summary>
    [Fact]
    public void OpcPackage_TryResolveRelationshipById_KnownUnknownAndExternal_ResolveOrReturnNull()
    {
        // Arrange
        using var stream = BuildValidPackage();
        using var package = OpcPackage.Open(stream);

        // Act / Assert
        Assert.Equal("layouts/layout1.xml", package.TryResolveRelationshipById("doc/main.xml", "rId1"));
        Assert.Null(package.TryResolveRelationshipById("doc/main.xml", "rIdMissing"));
        Assert.Null(package.TryResolveRelationshipById("doc/main.xml", "rId3"));
    }

    /// <summary>Proves the relationship type is reported by identifier, and an unknown identifier is rejected.</summary>
    [Fact]
    public void OpcPackage_GetRelationshipType_KnownAndUnknownId_ReturnsTypeOrThrows()
    {
        // Arrange
        using var stream = BuildValidPackage();
        using var package = OpcPackage.Open(stream);

        // Act / Assert
        Assert.Equal("http://example.com/relationships/layout", package.GetRelationshipType("doc/main.xml", "rId1"));
        Assert.Throws<OpcFormatException>(() => package.GetRelationshipType("doc/main.xml", "rIdMissing"));
    }

    /// <summary>Proves a malformed relationships part of a non-root part is reported when it is first used, not when opening.</summary>
    [Fact]
    public void OpcPackage_ResolveRelationship_MalformedPartRelationships_ThrowsOnFirstUse()
    {
        // Arrange
        using var stream = BuildValidPackage(("doc/bad.xml", "<b/>"), ("doc/_rels/bad.xml.rels", "<wrongRoot/>"));
        using var package = OpcPackage.Open(stream);

        // Act / Assert
        Assert.Throws<OpcFormatException>(() => package.ResolveRelationship("doc/bad.xml", "rId1"));
    }

    /// <summary>Proves a part is loaded as XML with and without a leading slash, and bad parts are rejected.</summary>
    [Fact]
    public void OpcPackage_LoadPartXmlRoot_PresentMissingAndMalformedParts_ReturnRootOrThrow()
    {
        // Arrange
        using var stream = BuildValidPackage(("doc/broken.xml", "<a><b></a>"), ("doc/dtd.xml", "<!DOCTYPE a [<!ENTITY e \"x\">]><a>&e;</a>"));
        using var package = OpcPackage.Open(stream);

        // Act / Assert
        Assert.Equal("main", package.LoadPartXmlRoot("doc/main.xml").Name.LocalName);
        Assert.Equal("main", package.LoadPartXmlRoot("/doc/main.xml").Name.LocalName);
        Assert.Throws<OpcFormatException>(() => package.LoadPartXmlRoot("doc/missing.xml"));
        Assert.Throws<OpcFormatException>(() => package.LoadPartXmlRoot("doc/broken.xml"));
        Assert.Throws<OpcFormatException>(() => package.LoadPartXmlRoot("doc/dtd.xml"));
    }

    /// <summary>Proves a binary part is returned byte for byte as a new array.</summary>
    [Fact]
    public void OpcPackage_GetPartBytes_WellFormedPart_ReturnsExactBytes()
    {
        // Arrange
        var expected = new byte[256];
        for (var i = 0; i < expected.Length; i++)
        {
            expected[i] = (byte)i;
        }

        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in new[]
                     {
                         ("[Content_Types].xml", ContentTypesXml),
                         ("_rels/.rels", PackageRelsXml),
                     })
            {
                using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }

            using var entryStream = archive.CreateEntry("media/image1.bin").Open();
            entryStream.Write(expected, 0, expected.Length);
        }

        stream.Position = 0;
        using var package = OpcPackage.Open(stream);

        // Act
        var first = package.GetPartBytes("media/image1.bin");
        var second = package.GetPartBytes("/media/image1.bin");

        // Assert
        Assert.Equal(expected, first);
        Assert.Equal(expected, second);
        Assert.NotSame(first, second);
        Assert.Throws<OpcFormatException>(() => package.GetPartBytes("media/missing.bin"));
    }

    /// <summary>
    ///     Proves a part whose decompressed content exceeds the part bound is rejected while it is
    ///     copied, rather than trusting its declared length or building an unbounded array.
    /// </summary>
    [Fact]
    public void OpcPackage_GetPartBytes_OversizedDecompressedEntry_ThrowsOpcFormatException()
    {
        // Arrange: a media part that inflates to one byte over the bound
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in new[]
                     {
                         ("[Content_Types].xml", ContentTypesXml),
                         ("_rels/.rels", PackageRelsXml),
                     })
            {
                using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }

            WriteZeroEntry(archive, "media/bomb.bin", OpcPackage.MaxPartBytes + 1);
        }

        stream.Position = 0;
        using var package = OpcPackage.Open(stream);

        // Act / Assert
        var exception = Assert.Throws<OpcFormatException>(() => package.GetPartBytes("media/bomb.bin"));
        Assert.Contains("decompression bomb", exception.Message);
    }

    /// <summary>
    ///     Proves a ZIP entry that declares a huge length but holds a few bytes does not force a large allocation: the
    ///     declared length is untrusted and pre-sizes the buffer only up to a small cap, and the bytes actually read are
    ///     what is returned.
    /// </summary>
    [Fact]
    public void OpcPackage_GetPartBytes_EntryDeclaringHugeLength_AllocatesLittleAndReturnsItsBytes()
    {
        // Arrange: a small part whose central-directory entry is patched to declare 200 MiB uncompressed
        byte[] expected = [1, 2, 3, 4, 5];
        var built = new MemoryStream();
        using (var archive = new ZipArchive(built, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in new[] { ("[Content_Types].xml", ContentTypesXml), ("_rels/.rels", PackageRelsXml) })
            {
                using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }

            using var entryStream = archive.CreateEntry("media/lie.bin").Open();
            entryStream.Write(expected, 0, expected.Length);
        }

        var zip = built.ToArray();
        PatchDeclaredLength(zip, "media/lie.bin", 200u * 1024 * 1024);
        using var package = OpcPackage.Open(new MemoryStream(zip));

        // Act
        var before = GC.GetAllocatedBytesForCurrentThread();
        var bytes = package.GetPartBytes("media/lie.bin");
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert: the 16 MiB pre-size cap plus a small constant, nowhere near the declared 200 MiB
        Assert.Equal(expected, bytes);
        Assert.True(allocated < (17L * 1024 * 1024), $"Allocated {allocated} bytes for a declared length of 200 MiB.");
    }

    /// <summary>Rewrites the uncompressed size the central directory declares for the named entry.</summary>
    private static void PatchDeclaredLength(byte[] zip, string entryName, uint declared)
    {
        var name = Encoding.UTF8.GetBytes(entryName);
        for (var i = 0; i + 46 + name.Length <= zip.Length; i++)
        {
            // Central directory header signature, then the entry name 46 bytes in
            if (zip[i] == 0x50 && zip[i + 1] == 0x4B && zip[i + 2] == 0x01 && zip[i + 3] == 0x02
                && zip.AsSpan(i + 46, name.Length).SequenceEqual(name))
            {
                BitConverter.GetBytes(declared).CopyTo(zip, i + 24);
                return;
            }
        }

        throw new InvalidOperationException($"No central directory entry named '{entryName}'.");
    }

    /// <summary>Proves part existence is tested by exact name, with or without the leading slash.</summary>
    [Fact]
    public void OpcPackage_ContainsPart_ExactAndDifferentCaseNames_ReportsExistence()
    {
        // Arrange
        using var stream = BuildValidPackage();
        using var package = OpcPackage.Open(stream);

        // Act / Assert
        Assert.True(package.ContainsPart("doc/main.xml"));
        Assert.True(package.ContainsPart("/doc/main.xml"));
        Assert.False(package.ContainsPart("doc/MAIN.xml"));
        Assert.False(package.ContainsPart("doc/other.xml"));
    }

    /// <summary>Proves normalization strips exactly one leading slash and validates its argument.</summary>
    [Fact]
    public void OpcPackage_NormalizePartPath_LeadingSlash_StripsOnlyOne()
    {
        // Act / Assert
        Assert.Equal("a/b.xml", OpcPackage.NormalizePartPath("/a/b.xml"));
        Assert.Equal("a/b.xml", OpcPackage.NormalizePartPath("a/b.xml"));
        Assert.Equal("/a.xml", OpcPackage.NormalizePartPath("//a.xml"));
        Assert.Equal(string.Empty, OpcPackage.NormalizePartPath(string.Empty));
        var exception = Assert.Throws<ArgumentNullException>(() => OpcPackage.NormalizePartPath(null!));
        Assert.Equal("partPath", exception.ParamName);
    }

    /// <summary>Proves every instance method rejects null arguments with the matching parameter name.</summary>
    [Fact]
    public void OpcPackage_Methods_NullArguments_ThrowArgumentNullExceptionWithParameterName()
    {
        // Arrange
        using var stream = BuildValidPackage();
        using var package = OpcPackage.Open(stream);

        // Act / Assert
        Assert.Equal("partPath", Assert.Throws<ArgumentNullException>(() => package.ContainsPart(null!)).ParamName);
        Assert.Equal("partPath", Assert.Throws<ArgumentNullException>(() => package.ResolvePart(null!)).ParamName);
        Assert.Equal("partPath", Assert.Throws<ArgumentNullException>(() => package.LoadPartXmlRoot(null!)).ParamName);
        Assert.Equal("partPath", Assert.Throws<ArgumentNullException>(() => package.GetPartBytes(null!)).ParamName);
        Assert.Equal("sourcePartPath", Assert.Throws<ArgumentNullException>(() => package.ResolveRelationship(null!, "rId1")).ParamName);
        Assert.Equal("relationshipId", Assert.Throws<ArgumentNullException>(() => package.ResolveRelationship(string.Empty, null!)).ParamName);
        Assert.Equal("relationshipId", Assert.Throws<ArgumentNullException>(() => package.GetRelationshipType(string.Empty, null!)).ParamName);
        Assert.Equal("relationshipId", Assert.Throws<ArgumentNullException>(() => package.TryResolveRelationshipById(string.Empty, null!)).ParamName);
        Assert.Equal("relationshipTypeSuffix", Assert.Throws<ArgumentNullException>(() => package.ResolveRelationshipByType(string.Empty, null!)).ParamName);
        Assert.Equal("relationshipTypeSuffix", Assert.Throws<ArgumentNullException>(() => package.TryResolveRelationshipByType(string.Empty, null!)).ParamName);
        Assert.Equal("sourcePartPath", Assert.Throws<ArgumentNullException>(() => package.TryResolveRelationshipByType(null!, "/x")).ParamName);
    }

    /// <summary>Proves disposing twice is harmless and every member then reports the disposed state.</summary>
    [Fact]
    public void OpcPackage_Dispose_CalledTwiceThenUsed_DoesNotThrowUntilUsed()
    {
        // Arrange
        using var stream = BuildValidPackage();
        var package = OpcPackage.Open(stream);

        // Act
        package.Dispose();
        var second = Record.Exception(package.Dispose);

        // Assert
        Assert.Null(second);
        Assert.Throws<ObjectDisposedException>(() => package.ContainsPart("doc/main.xml"));
        Assert.Throws<ObjectDisposedException>(() => package.ResolvePart("doc/main.xml"));
        Assert.Throws<ObjectDisposedException>(() => package.ResolveRelationship(string.Empty, "rId1"));
        Assert.Throws<ObjectDisposedException>(() => package.LoadPartXmlRoot("doc/main.xml"));
        Assert.Throws<ObjectDisposedException>(() => package.GetPartBytes("doc/main.xml"));
    }

    /// <summary>Proves the exception type carries its message and inner exception.</summary>
    [Fact]
    public void OpcFormatException_Constructors_CarryMessageAndInnerException()
    {
        // Arrange
        var inner = new InvalidOperationException("inner");

        // Act
        var plain = new OpcFormatException();
        var withMessage = new OpcFormatException("message");
        var withInner = new OpcFormatException("message", inner);

        // Assert
        Assert.NotNull(plain.Message);
        Assert.Equal("message", withMessage.Message);
        Assert.Equal("message", withInner.Message);
        Assert.Same(inner, withInner.InnerException);
    }
}

using System.Text;
using System.Xml;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Packaging;

namespace DemaConsulting.CanvasNet.Tests.Packaging;

// cspell:ignore lolz

/// <summary>
///     Unit tests for <see cref="SafeXml"/>: loading XML with no DTD processing, no external
///     resolution, and a character bound, from a stream, a string, and as a streaming reader.
/// </summary>
public class SafeXmlTests
{
    private const string ExternalEntityDocument =
        """
        <?xml version="1.0"?>
        <!DOCTYPE root [<!ENTITY xxe SYSTEM "file:///does/not/matter">]>
        <root>&xxe;</root>
        """;

    private const string ExpansionBombDocument =
        """
        <?xml version="1.0"?>
        <!DOCTYPE lolz [
          <!ENTITY lol "lol">
          <!ENTITY lol2 "&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;">
          <!ENTITY lol3 "&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;">
        ]>
        <lolz>&lol3;</lolz>
        """;

    /// <summary>Wraps text in a UTF-8 stream.</summary>
    private static MemoryStream Utf8(string text) => new(Encoding.UTF8.GetBytes(text));

    /// <summary>A readable-less stream.</summary>
    private sealed class UnreadableStream : MemoryStream
    {
        public override bool CanRead => false;
    }

    /// <summary>Proves a well-formed stream and a well-formed string load to the same tree, and the stream stays open.</summary>
    [Fact]
    public void SafeXml_Load_WellFormedStreamAndString_ReturnDocument()
    {
        // Arrange
        const string xml = "<a x=\"1\"><b>text</b></a>";
        using var stream = Utf8(xml);

        // Act
        var fromStream = SafeXml.Load(stream, 1000);
        var fromString = SafeXml.Load(xml, 1000);

        // Assert
        Assert.Equal("a", fromStream.Root!.Name.LocalName);
        Assert.Equal("text", (string?)fromStream.Root.Element("b"));
        Assert.Equal(fromStream.ToString(SaveOptions.DisableFormatting), fromString.ToString(SaveOptions.DisableFormatting));
        Assert.True(stream.CanRead);
    }

    /// <summary>Proves a DTD with an external entity is rejected rather than resolved, for stream and string input.</summary>
    [Fact]
    public void SafeXml_Load_ExternalEntityDocumentType_ThrowsXmlException()
    {
        // Arrange
        using var stream = Utf8(ExternalEntityDocument);

        // Act / Assert
        Assert.Throws<XmlException>(() => SafeXml.Load(stream, 100_000));
        Assert.Throws<XmlException>(() => SafeXml.Load(ExternalEntityDocument, 100_000));
    }

    /// <summary>Proves a "billion laughs" entity expansion is rejected because the DTD itself is.</summary>
    [Fact]
    public void SafeXml_Load_EntityExpansionDocument_ThrowsXmlException()
    {
        // Act / Assert
        Assert.Throws<XmlException>(() => SafeXml.Load(ExpansionBombDocument, 100_000));
        using var stream = Utf8(ExpansionBombDocument);
        Assert.Throws<XmlException>(() => SafeXml.Load(stream, 100_000));
    }

    /// <summary>Proves a document past the character bound is rejected, and one within it is not.</summary>
    [Fact]
    public void SafeXml_Load_DocumentLargerThanBound_ThrowsXmlException()
    {
        // Arrange
        var xml = "<a>" + new string('x', 500) + "</a>";
        using var stream = Utf8(xml);

        // Act / Assert
        Assert.Throws<XmlException>(() => SafeXml.Load(xml, 100));
        Assert.Throws<XmlException>(() => SafeXml.Load(stream, 100));
        Assert.Equal(500, SafeXml.Load(xml, 600).Root!.Value.Length);
    }

    /// <summary>Proves malformed XML surfaces as <see cref="XmlException"/> for the caller to translate.</summary>
    [Fact]
    public void SafeXml_Load_MalformedXml_ThrowsXmlException()
    {
        // Arrange
        using var stream = Utf8("<a><b></a>");

        // Act / Assert
        Assert.Throws<XmlException>(() => SafeXml.Load(stream, 1000));
        Assert.Throws<XmlException>(() => SafeXml.Load("<a", 1000));
    }

    /// <summary>Proves ordinary built-in entities and character references still load.</summary>
    [Fact]
    public void SafeXml_Load_BuiltInEntitiesAndCharacterReferences_Load()
    {
        // Arrange: far more references than the entity-expansion bound
        var xml = "<a>" + string.Concat(Enumerable.Repeat("&amp;&lt;&#65;", 3000)) + "</a>";

        // Act
        var document = SafeXml.Load(xml, 1_000_000);

        // Assert
        Assert.Equal(9000, document.Root!.Value.Length);
    }

    /// <summary>Proves a zero or negative bound is rejected, since zero would mean "unlimited" to the underlying reader.</summary>
    [Fact]
    public void SafeXml_Load_NonPositiveBound_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var stream = Utf8("<a/>");

        foreach (var maxCharacters in new[] { 0, -1, int.MinValue })
        {
            // Act / Assert
            Assert.Equal("maxCharacters", Assert.Throws<ArgumentOutOfRangeException>(() => SafeXml.Load(stream, maxCharacters)).ParamName);
            Assert.Equal("maxCharacters", Assert.Throws<ArgumentOutOfRangeException>(() => SafeXml.Load("<a/>", maxCharacters)).ParamName);
            Assert.Equal("maxCharacters", Assert.Throws<ArgumentOutOfRangeException>(() => SafeXml.CreateReader(stream, maxCharacters)).ParamName);
        }
    }

    /// <summary>Proves null and unreadable inputs are rejected up front with the parameter name.</summary>
    [Fact]
    public void SafeXml_Load_NullOrUnreadableInput_ThrowsWithParameterName()
    {
        // Arrange
        using var unreadable = new UnreadableStream();

        // Act / Assert
        Assert.Equal("stream", Assert.Throws<ArgumentNullException>(() => SafeXml.Load((Stream)null!, 10)).ParamName);
        Assert.Equal("xml", Assert.Throws<ArgumentNullException>(() => SafeXml.Load((string)null!, 10)).ParamName);
        Assert.Equal("stream", Assert.Throws<ArgumentNullException>(() => SafeXml.CreateReader(null!, 10)).ParamName);
        Assert.Equal("stream", Assert.Throws<ArgumentException>(() => SafeXml.Load(unreadable, 10)).ParamName);
        Assert.Equal("stream", Assert.Throws<ArgumentException>(() => SafeXml.CreateReader(unreadable, 10)).ParamName);
    }

    /// <summary>Proves the streaming reader reads the root start tag, is bounded and refuses a DTD, and leaves the stream open.</summary>
    [Fact]
    public void SafeXml_CreateReader_RootStartTag_IsBoundedAndRefusesDocumentType()
    {
        // Arrange
        using var stream = Utf8("<root a=\"1\"><child/></root>");

        // Act
        string? attribute;
        using (var reader = SafeXml.CreateReader(stream, 1000))
        {
            reader.MoveToContent();
            attribute = reader.GetAttribute("a");
        }

        // Assert
        Assert.Equal("1", attribute);
        Assert.True(stream.CanRead);

        using var oversized = Utf8("<root a=\"" + new string('x', 500) + "\"/>");
        using var bounded = SafeXml.CreateReader(oversized, 100);
        Assert.Throws<XmlException>(() => bounded.MoveToContent());

        using var withDtd = Utf8(ExternalEntityDocument);
        using var refusing = SafeXml.CreateReader(withDtd, 100_000);
        Assert.Throws<XmlException>(() => refusing.MoveToContent());
    }
}

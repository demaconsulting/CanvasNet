using System.Text;

namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     Unit tests for <see cref="MermaidDocument"/>: opening from a string, a stream and a file, the metadata, the limits
///     of reading, and the errors.
/// </summary>
public class MermaidDocumentTests
{
    /// <summary>
    ///     Validates that a flowchart is detected as the supported type, whether written <c>flowchart</c> or <c>graph</c>.
    /// </summary>
    [Fact]
    public void MermaidDocument_Parse_Flowchart_IsSupportedFlowchart()
    {
        // Arrange / Act
        var flowchart = MermaidDocument.Parse(MermaidTestHelpers.FlowchartText);
        var graph = MermaidDocument.Parse("graph TD\n  A-->B");

        // Assert
        Assert.Equal(("flowchart", true), (flowchart.DiagramType, flowchart.IsSupported));
        Assert.Equal(("flowchart", true), (graph.DiagramType, graph.IsSupported));
    }

    /// <summary>
    ///     Validates that a sequence diagram opens as a known type that is not drawn, without parsing its body.
    /// </summary>
    [Fact]
    public void MermaidDocument_Parse_SequenceDiagram_IsKnownButNotSupported()
    {
        // Arrange / Act
        var document = MermaidDocument.Parse("sequenceDiagram\n A->>B: hi\n this body is not parsed in this version ]]][[[");

        // Assert
        Assert.Equal("sequence", document.DiagramType);
        Assert.False(document.IsSupported);
        Assert.Equal((0, 0, 0), (document.NodeCount, document.EdgeCount, document.SubgraphCount));
        Assert.Null(document.Direction);
        Assert.Null(document.AccTitle);
    }

    /// <summary>
    ///     Validates that the frontmatter title becomes the document title.
    /// </summary>
    [Fact]
    public void MermaidDocument_Parse_FrontmatterTitle_IsTitle()
    {
        // Arrange / Act
        var document = MermaidDocument.Parse("---\ntitle: Order flow\n---\nflowchart LR\n A-->B");

        // Assert
        Assert.Equal("Order flow", document.Title);
    }

    /// <summary>
    ///     Validates that text without a diagram keyword fails with the unknown-diagram code, a location and a hint
    ///     naming the nearest keyword.
    /// </summary>
    [Fact]
    public void MermaidDocument_Parse_UnknownKeyword_ThrowsLocatedSyntaxException()
    {
        // Arrange
        var options = new MermaidOpenOptions { SourceName = "x.mmd" };

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse("%% note\n\n  Flowchart LR\n A-->B", options));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.UnknownDiagram, ex.Code);
        Assert.Equal(new MermaidLocation("x.mmd", 3, 3), ex.Location);
        Assert.Contains("'flowchart'", ex.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a stream is read as strict UTF-8 without its byte-order mark and left open.
    /// </summary>
    [Fact]
    public void MermaidDocument_Open_StreamWithBom_ParsesAndLeavesStreamOpen()
    {
        // Arrange
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(MermaidTestHelpers.SequenceText)).ToArray();
        using var stream = new MemoryStream(bytes);

        // Act
        var document = MermaidDocument.Open(stream, new MermaidOpenOptions { SourceName = "s.mmd" });

        // Assert
        Assert.Equal(("sequence", "s.mmd"), (document.DiagramType, document.SourceName));
        Assert.True(stream.CanRead);
    }

    /// <summary>
    ///     Validates that invalid UTF-8 is refused, not replaced.
    /// </summary>
    [Fact]
    public void MermaidDocument_Open_InvalidUtf8_ThrowsSyntaxException()
    {
        // Arrange
        using var stream = new MemoryStream([(byte)'g', (byte)'r', 0xC3, 0x28]);

        // Act / Assert
        var ex = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Open(stream));
        Assert.Contains("UTF-8", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a file is opened with its path as the source name.
    /// </summary>
    [Fact]
    public void MermaidDocument_Open_File_UsesPathAsSourceName()
    {
        // Arrange
        var path = MermaidTestHelpers.WriteTempFile(Encoding.UTF8.GetBytes("pie\n \"a\" : 1\n"));
        try
        {
            // Act
            var document = MermaidDocument.Open(path);

            // Assert
            Assert.Equal(("pie", path), (document.DiagramType, document.SourceName));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates the argument checks of the entry points.
    /// </summary>
    [Fact]
    public void MermaidDocument_EntryPoints_InvalidArguments_Throw()
    {
        // Arrange
        using var unreadable = new UnreadableStream();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => MermaidDocument.Parse(null!));
        Assert.Throws<ArgumentException>(() => MermaidDocument.Open(" "));
        Assert.Throws<ArgumentNullException>(() => MermaidDocument.Open((Stream)null!));
        Assert.Throws<ArgumentException>(() => MermaidDocument.Open(unreadable));
        Assert.Throws<FileNotFoundException>(() => MermaidDocument.Open(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.mmd")));
    }

    /// <summary>
    ///     Validates the size limit: text over <see cref="MermaidLimits.MaxTextSize"/> after comments are removed is an
    ///     <see cref="InvalidDataException"/>, while comments do not count.
    /// </summary>
    [Fact]
    public void MermaidDocument_Parse_OverMaxTextSize_ThrowsInvalidData()
    {
        // Arrange
        var options = new MermaidOpenOptions { Limits = new MermaidLimits { MaxTextSize = 30 } };
        var commented = "%% a comment line that is long enough\n" + MermaidTestHelpers.SequenceText;

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => MermaidDocument.Parse("sequenceDiagram\n" + new string('x', 40), options));
        Assert.Equal("sequence", MermaidDocument.Parse(commented, options).DiagramType);
    }

    /// <summary>
    ///     Validates that a stream that is certainly too long is refused while it is read.
    /// </summary>
    [Fact]
    public void MermaidDocument_Open_StreamOverBound_ThrowsInvalidData()
    {
        // Arrange: more bytes than three per permitted character can explain
        var options = new MermaidOpenOptions { Limits = new MermaidLimits { MaxTextSize = 10 } };
        using var stream = new MemoryStream(new byte[(10 * 4 * 3) + 100]);

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => MermaidDocument.Open(stream, options));
    }


    /// <summary>
    ///     A stream that cannot be read.
    /// </summary>
    private sealed class UnreadableStream : MemoryStream
    {
        /// <inheritdoc />
        public override bool CanRead => false;
    }
}
